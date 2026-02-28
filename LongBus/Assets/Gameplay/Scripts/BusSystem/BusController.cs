using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

namespace PixmewStudios
{
    public class BusController : MonoBehaviour
    {
        [Header("Head Movement")]
        [SerializeField] internal float moveSpeed = 8f;
        [SerializeField] internal float normalmoveSpeed = 10f;
        [SerializeField] internal float boostedmoveSpeed = 12f;
        [SerializeField] internal float boostRampDuration = 0.2f;
        [SerializeField] private float rotationSpeed = 100f;

        [Header("Drift Settings")]
        [Tooltip("Low value = Ice/Drift (e.g. 2.0). High value = Grip (e.g. 15.0).")]
        [SerializeField] private float driftTraction = 3.5f; 
        private Vector3 currentVelocityDir; 
        [Tooltip("How much slip angle triggers the skidmarks?")]
        [SerializeField] private float driftVisualThreshold = 0.2f;
        [Tooltip("Assign the existing TrailRenderers attached to the bus wheels here.")]
        [SerializeField] private TrailRenderer[] skidmarkRenderers;
        
        // Skidmark state
        private bool isDriftingVisually = false;

        [Header("Jump & Physics")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float gravity = 30f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckOffset = 0.55f;

        [Header("Fake Collision & Friction")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float headCollisionRadius = 1.0f;
        [Range(0f, 1f)]
        [SerializeField] private float wallGrindFriction = 0.7f;

        [Header("Game Over Rules")]
        [SerializeField] private float maxStuckTime = 5f;
        private float currentStuckTimer = 0f;
        private Vector3 lastPosition;

        [Header("Visual Effects")]
        [SerializeField] private AnimationCurve dolphinPitchCurve = new AnimationCurve(new Keyframe(-1, 1), new Keyframe(0, 0), new Keyframe(1, -1));
        [SerializeField] private float maxPitchAngle = 45f;

        [Header("Body Settings")]
        [SerializeField] private GameObject busSegmentPrefab;
        [SerializeField] private float segmentGap = 1.5f;
        
        [Tooltip("If true, segments look at the path direction (Trailer style). If false, they copy the head's rotation (Snake style). True is better for Drifting.")]
        [SerializeField] private bool useTangentRotation = true; 

        [Header("Collision Settings")]
        [SerializeField] private int safeSegmentCount = 4;
        internal int SegmentCount => busSegments.Count;
        private bool isDead = false;

        // --- Internal Variables ---
        private List<Vector3> pathPositions = new List<Vector3>();
        private List<Quaternion> pathRotations = new List<Quaternion>(); // Kept for history, used if useTangentRotation is false
        private List<Transform> busSegments = new List<Transform>();

        private BusControls inputActions;
        private Vector2 moveInput;
        private bool isBoosting = false;
        private Tween speedTween;
        private CameraController cameraController;

        // Physics State
        private float verticalVelocity;
        private bool isGrounded;
        private RaycastHit groundHit;

        [Header("Game Rules")]
        [SerializeField] private int passengersPerSegment = 3;
        private int totalPassengersCollected = 0;

        private void Awake()
        {
            inputActions = new BusControls();
            inputActions.BusControlsActionMap.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            inputActions.BusControlsActionMap.Move.canceled += ctx => moveInput = Vector2.zero;
            inputActions.BusControlsActionMap.AddSegment.performed += _ => AddBusSegment();
            inputActions.BusControlsActionMap.BoostSpeed.performed += boostlogic => StartBoost();
            inputActions.BusControlsActionMap.BoostSpeed.canceled += boostlogic => StopBoost();
            inputActions.BusControlsActionMap.Jump.performed += jump => Jump();

            busSegments.Add(this.transform);

            // --- PRE-FILL HISTORY ---
            pathPositions.Add(transform.position);
            pathRotations.Add(transform.rotation);

            Vector3 backDir = -transform.forward;
            for (int i = 1; i <= 50; i++)
            {
                pathPositions.Add(transform.position + (backDir * (i * 0.2f)));
                pathRotations.Add(transform.rotation);
            }

            lastPosition = transform.position;
            currentVelocityDir = transform.forward;

            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                cameraController = Camera.main.transform.parent.GetComponent<CameraController>();
            }
        }

        void Start()
        {
            if (skidmarkRenderers != null)
            {
                foreach (var trail in skidmarkRenderers)
                {
                    if (trail != null) trail.emitting = false;
                }
            }

            StartCoroutine(SpawnInitialSegments(2));
        }

        private void OnEnable() => inputActions.BusControlsActionMap.Enable();
        private void OnDisable() => inputActions.BusControlsActionMap.Disable();

        private void LateUpdate()
        {
            if (isDead) return;

            CheckGround();
            HandleHeadMovement();

            CheckIfStuck();
            RecordPath();
            MoveBodySegments();
            
            HandleSkidmarks();
        }

        private void HandleSkidmarks()
        {
            if (skidmarkRenderers == null || skidmarkRenderers.Length == 0) return;

            // Calculate slip (difference between where we face horizontally and where we move)
            Vector3 flatForward = transform.forward;
            flatForward.y = 0;
            flatForward.Normalize();

            float moveSpeedMag = currentVelocityDir.magnitude * moveSpeed;
            float slipAngle = 1f - Vector3.Dot(flatForward, currentVelocityDir);

            // Turn on skidmarks if moving reasonably fast and slipping
            bool shouldDrift = isGrounded && moveSpeedMag > 2f && slipAngle > driftVisualThreshold;

            if (shouldDrift && !isDriftingVisually)
            {
                // Started drifting: Enable trail emission
                isDriftingVisually = true;
                foreach (var trail in skidmarkRenderers)
                {
                    if (trail != null) 
                    {
                        trail.emitting = true;
                    }
                }
            }
            else if (!shouldDrift && isDriftingVisually)
            {
                // Stopped drifting: Disable trail emission
                isDriftingVisually = false;
                foreach (var trail in skidmarkRenderers)
                {
                    if (trail != null) 
                    {
                        trail.emitting = false;
                    }
                }
            }
        }

        #region Movement & Collision

        private void CheckGround()
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, out groundHit, groundCheckOffset + 0.2f, groundLayer);
        }

        private void Jump()
        {
            if (isGrounded)
            {
                verticalVelocity = jumpForce;
                isGrounded = false;
                transform.position += Vector3.up * 0.1f;
            }
        }

        private void HandleHeadMovement()
        {
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;

            // Rotation Logic (Visual Only)
            Quaternion targetLookRotation = transform.rotation;
            if (moveDirection.magnitude >= 0.1f)
            {
                targetLookRotation = Quaternion.LookRotation(moveDirection);
            }

            float targetPitchAngle = 0f;
            if (!isGrounded)
            {
                float velocityRatio = Mathf.Clamp(verticalVelocity / jumpForce, -1.5f, 1.5f);
                targetPitchAngle = dolphinPitchCurve.Evaluate(velocityRatio) * maxPitchAngle;
            }

            Quaternion pitchRotation = Quaternion.Euler(targetPitchAngle, 0, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetLookRotation * pitchRotation, rotationSpeed * Time.deltaTime);

            // DRIFT LOGIC
            // We calculate drift, but we do NOT apply it to the transform.rotation directly
            // transform.rotation is "Where I am looking"
            // currentVelocityDir is "Where I am moving"
            
            Vector3 faceDir = transform.forward;
            faceDir.y = 0; 
            faceDir.Normalize();

            currentVelocityDir = Vector3.Lerp(currentVelocityDir, faceDir, driftTraction * Time.deltaTime);
            currentVelocityDir.Normalize();

            Vector3 forwardMovement = currentVelocityDir * moveSpeed;

            // Vertical Logic
            if (isGrounded && verticalVelocity <= 0)
            {
                verticalVelocity = 0;
                Vector3 snappedPos = transform.position;
                snappedPos.y = groundHit.point.y + groundCheckOffset;
                transform.position = snappedPos;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 verticalMovement = Vector3.up * verticalVelocity;
            Vector3 finalVelocity = (forwardMovement + verticalMovement) * Time.deltaTime;

            MoveWithGrindingPhysics(finalVelocity);
        }

        private void MoveWithGrindingPhysics(Vector3 desiredMotion)
        {
            if (desiredMotion.magnitude < 0.001f) return;

            Vector3 origin = transform.position;
            Vector3 direction = desiredMotion.normalized;
            float distance = desiredMotion.magnitude + 0.1f;

            RaycastHit hit;
            bool collisionDetected = false;
            Vector3 collisionNormal = Vector3.zero;

            if (Physics.SphereCast(origin, headCollisionRadius, direction, out hit, distance, obstacleMask))
            {
                collisionDetected = true;
                collisionNormal = hit.normal;

                BusBodySegment segment = hit.collider.GetComponent<BusBodySegment>();
                if (segment != null && segment.segmentIndex <= safeSegmentCount)
                {
                    collisionDetected = false; 
                }
            }

            if (!collisionDetected)
            {
                Vector3 futurePos = origin + desiredMotion;
                Collider[] overlaps = Physics.OverlapSphere(futurePos, headCollisionRadius, obstacleMask);

                foreach (var col in overlaps)
                {
                    BusBodySegment segment = col.GetComponent<BusBodySegment>();
                    if (segment == null || segment.segmentIndex > safeSegmentCount)
                    {
                        collisionDetected = true;
                        collisionNormal = -direction;
                        break;
                    }
                }
            }

            if (collisionDetected)
            {
                Vector3 slideMotion = Vector3.ProjectOnPlane(desiredMotion, collisionNormal);
                float grindFactor = 1f - wallGrindFriction;
                slideMotion *= grindFactor;
                
                if (slideMotion.magnitude > 0.001f)
                {
                     currentVelocityDir = slideMotion.normalized;
                     currentVelocityDir.y = 0;
                }

                transform.position += slideMotion;
            }
            else
            {
                transform.position += desiredMotion;
            }
        }

        private System.Collections.IEnumerator SpawnInitialSegments(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AddBusSegment(true);
                yield return new WaitForSeconds(0.15f);
            }
        }

        #endregion

        #region Game Over Logic

        private void CheckIfStuck()
        {
            float distMoved = Vector3.Distance(transform.position, lastPosition);
            lastPosition = transform.position;
            bool isTryingToMove = moveSpeed > 0;

            if (isTryingToMove && distMoved < (2f * Time.deltaTime))
            {
                currentStuckTimer += Time.deltaTime;
                if (currentStuckTimer >= maxStuckTime)
                {
                    TriggerGameOver();
                }
            }
            else
            {
                currentStuckTimer = 0f;
            }
        }

        private void TriggerGameOver()
        {
            if (isDead) return;
            isDead = true;
            Debug.LogError("GAME OVER: Bus was stuck for too long!");
            moveSpeed = 0;
            enabled = false; 
        }

        #endregion

        #region Path & Segments (UPDATED)
        private void RecordPath()
        {
            // We still record position and rotation, but we might ignore rotation later
            float distSqr = (pathPositions.Count > 0) ? (pathPositions[0] - transform.position).sqrMagnitude : 1f;
            if (distSqr > 0.0001f)
            {
                pathPositions.Insert(0, transform.position);
                pathRotations.Insert(0, transform.rotation);
            }
        }

        private void MoveBodySegments()
        {
            float totalDistanceNeeded = 0f;
            int maxIndexUsed = 0;
            for (int i = 1; i < busSegments.Count; i++)
            {
                totalDistanceNeeded += segmentGap;
                int usedIndex = SetSegmentToPathDistance(busSegments[i], totalDistanceNeeded);
                if (usedIndex > maxIndexUsed) maxIndexUsed = usedIndex;
            }
            int trimThreshold = maxIndexUsed + 5;
            if (pathPositions.Count > trimThreshold)
            {
                pathPositions.RemoveRange(trimThreshold, pathPositions.Count - trimThreshold);
                pathRotations.RemoveRange(trimThreshold, pathRotations.Count - trimThreshold);
            }
        }

        private int SetSegmentToPathDistance(Transform segment, float requiredDist)
        {
            float currentDistTraveled = 0f;
            for (int i = 0; i < pathPositions.Count - 1; i++)
            {
                Vector3 pointA = pathPositions[i];
                Vector3 pointB = pathPositions[i + 1];
                float distBetweenPoints = Vector3.Distance(pointA, pointB);

                if (currentDistTraveled + distBetweenPoints >= requiredDist)
                {
                    float remainingDist = requiredDist - currentDistTraveled;
                    float t = remainingDist / distBetweenPoints;

                    // 1. POSITION (Standard)
                    segment.position = Vector3.Lerp(pointA, pointB, t);

                    // 2. ROTATION (The Fix)
                    if (useTangentRotation)
                    {
                        // Look at the point slightly ahead in history (towards the head)
                        // pointA is newer (closer to head), pointB is older
                        Vector3 lookDir = (pointA - pointB).normalized;
                        if (lookDir != Vector3.zero)
                        {
                            Quaternion lookRot = Quaternion.LookRotation(lookDir);
                            // Smoothly rotate towards the path direction
                            segment.rotation = Quaternion.Slerp(segment.rotation, lookRot, 15f * Time.deltaTime);
                        }
                    }
                    else
                    {
                        // Old "Snake" style - precise copy of head's rotation
                        segment.rotation = Quaternion.Slerp(pathRotations[i], pathRotations[i + 1], t);
                    }

                    return i;
                }
                currentDistTraveled += distBetweenPoints;
            }
            
            // Tail fallback
            if (pathPositions.Count > 0)
            {
                segment.position = pathPositions.Last();
                segment.rotation = pathRotations.Last();
                return pathPositions.Count - 1;
            }
            return 0;
        }

        private void StartBoost()
        {
            if (isBoosting) return;
            isBoosting = true;
            speedTween?.Kill();
            speedTween = DOTween.To(() => moveSpeed, x => moveSpeed = x, boostedmoveSpeed, boostRampDuration).SetEase(Ease.InOutQuad);
            if (cameraController != null) cameraController.StartContinuousShake();
        }

        private void StopBoost()
        {
            speedTween?.Kill();
            speedTween = DOTween.To(() => moveSpeed, x => moveSpeed = x, normalmoveSpeed, boostRampDuration).SetEase(Ease.OutQuad)
                .OnComplete(() => { if (cameraController != null) cameraController.StopShake(); isBoosting = false; });
        }

        public void AddBusSegment(bool isInitial = false)
        {
            if (busSegmentPrefab == null) return;
            Transform lastSegment = busSegments.Last();

            // Default spawn behind
            Vector3 backwardOffset = lastSegment.forward * segmentGap;
            Vector3 finalPos = lastSegment.position - backwardOffset;

            GameObject newSegment = Instantiate(busSegmentPrefab, finalPos, lastSegment.rotation);
            newSegment.layer = LayerMask.NameToLayer("BusBody");

            BusBodySegment segScript = newSegment.GetComponent<BusBodySegment>();
            if (segScript != null)
            {
                segScript.segmentIndex = busSegments.Count;
                segScript.PlaySpawnAnimation();
            }

            busSegments.Add(newSegment.transform);
        }

        public Vector3 RemoveLastSegment()
        {
            if (busSegments.Count <= 2) return Vector3.zero;
            int lastIndex = busSegments.Count - 1;
            Transform segmentToRemove = busSegments[lastIndex];
            Vector3 position = segmentToRemove.position;
            busSegments.RemoveAt(lastIndex);
            segmentToRemove.SetParent(null);
            segmentToRemove.DOScale(Vector3.zero, 0.3f)
                .OnComplete(() => Destroy(segmentToRemove.gameObject));
            return position;
        }

        public void CollectPassenger()
        {
            totalPassengersCollected++;
            if (totalPassengersCollected % passengersPerSegment == 0) AddBusSegment();
            RefrenceHolder.Instance.gameProgressHandler.AddProgress(2);
        }
        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckOffset + 0.2f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, headCollisionRadius);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, currentVelocityDir * 3f);
        }
    }
}