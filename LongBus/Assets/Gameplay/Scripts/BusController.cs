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

        [Header("Jump & Physics")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float gravity = 30f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckOffset = 0.55f;

        [Header("Fake Collision & Friction")]
        [Tooltip("Layers the Head should physically collide with")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float headCollisionRadius = 1.0f;

        [Tooltip("0 = Ice (Easy), 1 = Sticky Glue (Impossible). Recommended: 0.7")]
        [Range(0f, 1f)]
        [SerializeField] private float wallGrindFriction = 0.7f;

        [Header("Game Over Rules")]
        [Tooltip("How long can the bus be stuck before Game Over?")]
        [SerializeField] private float maxStuckTime = 5f;
        private float currentStuckTimer = 0f;
        private Vector3 lastPosition;

        [Header("Visual Effects")]
        [SerializeField] private AnimationCurve dolphinPitchCurve = new AnimationCurve(new Keyframe(-1, 1), new Keyframe(0, 0), new Keyframe(1, -1));
        [SerializeField] private float maxPitchAngle = 45f;

        [Header("Body Settings")]
        [SerializeField] private GameObject busSegmentPrefab;
        [SerializeField] private float segmentGap = 1.5f;

        [Header("Collision Settings")]
        [SerializeField] private int safeSegmentCount = 4;
        internal int SegmentCount => busSegments.Count;
        private bool isDead = false;

        // --- Internal Variables ---
        private List<Vector3> pathPositions = new List<Vector3>();
        private List<Quaternion> pathRotations = new List<Quaternion>();
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

        // 1. Ensure AWAKE pre-fills the path (Crucial for spawning "behind")
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
            // We add fake history points BEHIND the bus so the initial segments have a track to sit on
            pathPositions.Add(transform.position);
            pathRotations.Add(transform.rotation);

            Vector3 backDir = -transform.forward;
            // Pre-fill about 15 units of history (enough for ~10 segments)
            for (int i = 1; i <= 50; i++)
            {
                // 0.2f fidelity gives us a smooth curve history
                pathPositions.Add(transform.position + (backDir * (i * 0.2f)));
                pathRotations.Add(transform.rotation);
            }

            lastPosition = transform.position;

            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                cameraController = Camera.main.transform.parent.GetComponent<CameraController>();
            }
        }

        void Start()
        {
            StartCoroutine(SpawnInitialSegments(2));
        }

        private void OnEnable() => inputActions.BusControlsActionMap.Enable();
        private void OnDisable() => inputActions.BusControlsActionMap.Disable();

        private void LateUpdate()
        {
            if (isDead) return;

            CheckGround();
            HandleHeadMovement();

            // Perform Logic Checks
            CheckIfStuck();
            RecordPath();
            MoveBodySegments();
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

            // 1. Rotation Logic
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

            // 2. Vertical Logic
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

            // 3. Velocity Calculation
            Vector3 forwardMovement = transform.forward;
            forwardMovement.y = 0;
            forwardMovement.Normalize();
            forwardMovement *= moveSpeed;

            Vector3 verticalMovement = Vector3.up * verticalVelocity;
            Vector3 finalVelocity = (forwardMovement + verticalMovement) * Time.deltaTime;

            // 4. Apply Collision Logic
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

            // CHECK 1: The Prediction Ray (SphereCast)
            // Good for detecting walls in front of us
            if (Physics.SphereCast(origin, headCollisionRadius, direction, out hit, distance, obstacleMask))
            {
                collisionDetected = true;
                collisionNormal = hit.normal;

                // Check if it's our own body
                BusBodySegment segment = hit.collider.GetComponent<BusBodySegment>();
                if (segment != null && segment.segmentIndex <= safeSegmentCount)
                {
                    collisionDetected = false; // Ignore safe segments
                }
            }

            // CHECK 2: The "Gap Filler" (Overlap Check)
            // If Raycast missed, check if the DESTINATION point is literally inside a collider.
            // This catches cases where we slipped through a gap.
            if (!collisionDetected)
            {
                Vector3 futurePos = origin + desiredMotion;
                Collider[] overlaps = Physics.OverlapSphere(futurePos, headCollisionRadius, obstacleMask);

                foreach (var col in overlaps)
                {
                    BusBodySegment segment = col.GetComponent<BusBodySegment>();
                    // If it's a body part and NOT a safe one
                    if (segment == null || segment.segmentIndex > safeSegmentCount)
                    {
                        collisionDetected = true;
                        // Since we are already "inside" or very close, push back opposite to movement
                        collisionNormal = -direction;
                        break;
                    }
                }
            }

            // --- RESOLUTION ---
            if (collisionDetected)
            {
                // 1. Project velocity along the wall (Standard slide)
                Vector3 slideMotion = Vector3.ProjectOnPlane(desiredMotion, collisionNormal);

                // 2. Apply Friction/Drag
                float grindFactor = 1f - wallGrindFriction;
                slideMotion *= grindFactor;

                // Apply the reduced movement
                transform.position += slideMotion;
            }
            else
            {
                // No collision? Move normally
                transform.position += desiredMotion;
            }
        }

        private System.Collections.IEnumerator SpawnInitialSegments(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AddBusSegment(true); // true = silent spawn (optional, see below)
                yield return new WaitForSeconds(0.15f); // "Pop... Pop..." effect
            }
        }

        #endregion

        #region Game Over Logic

        private void CheckIfStuck()
        {
            // Calculate how much we ACTUALLY moved this frame
            float distMoved = Vector3.Distance(transform.position, lastPosition);

            // Update last position for next frame
            lastPosition = transform.position;

            // Are we trying to move? (Input is active or speed is high)
            bool isTryingToMove = moveSpeed > 0;

            // If we are trying to move, but barely moving (less than 1 unit per second approx)
            if (isTryingToMove && distMoved < (2f * Time.deltaTime))
            {
                currentStuckTimer += Time.deltaTime;

                // Optional: Debug log to see the timer counting up
                // Debug.Log($"Stuck... {currentStuckTimer:F1}s");

                if (currentStuckTimer >= maxStuckTime)
                {
                    TriggerGameOver();
                }
            }
            else
            {
                // We are moving fine, reset timer
                currentStuckTimer = 0f;
            }
        }

        private void TriggerGameOver()
        {
            if (isDead) return;
            isDead = true;
            Debug.LogError("GAME OVER: Bus was stuck for too long!");

            // Stop everything
            moveSpeed = 0;
            enabled = false; // Disable this script

            // TODO: Show Game Over UI here
        }

        #endregion

        #region Path & Segments (Standard)
        // ... (Keep your existing RecordPath, MoveBodySegments, SetSegmentToPathDistance methods here)
        private void RecordPath()
        {
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
                    segment.position = Vector3.Lerp(pointA, pointB, t);
                    segment.rotation = Quaternion.Slerp(pathRotations[i], pathRotations[i + 1], t);
                    return i;
                }
                currentDistTraveled += distBetweenPoints;
            }
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

            // 1. Calculate Spawn Position (Behind the last segment)
            Vector3 backwardOffset = lastSegment.forward * segmentGap;
            Vector3 finalPos = lastSegment.position - backwardOffset;

            // 2. Instantiate
            GameObject newSegment = Instantiate(busSegmentPrefab, finalPos, lastSegment.rotation);
            newSegment.layer = LayerMask.NameToLayer("BusBody");

            // 3. Setup Segment Data
            BusBodySegment segScript = newSegment.GetComponent<BusBodySegment>();
            if (segScript != null)
            {
                segScript.segmentIndex = busSegments.Count;

                // --- DELEGATE ANIMATION ---
                // We tell the segment: "Do your entrance!"
                segScript.PlaySpawnAnimation();
            }

            // 4. Add to list
            busSegments.Add(newSegment.transform);
        }

        public Vector3 RemoveLastSegment()
        {
            // Don't remove if we are already at minimum size (Head + 1 Body)
            if (busSegments.Count <= 2) return Vector3.zero;

            // 1. Get the last segment
            int lastIndex = busSegments.Count - 1;
            Transform segmentToRemove = busSegments[lastIndex];
            Vector3 position = segmentToRemove.position;

            // 2. Remove it from the list immediately so the physics loop ignores it
            busSegments.RemoveAt(lastIndex);

            // 3. Smoothly animate it disappearing (Scale down to 0)
            // We unparent it so it stays behind while shrinking
            segmentToRemove.SetParent(null);
            segmentToRemove.DOScale(Vector3.zero, 0.3f)
                .OnComplete(() => Destroy(segmentToRemove.gameObject));

            return position;
        }

        public void CollectPassenger()
        {
            totalPassengersCollected++;
            if (totalPassengersCollected % passengersPerSegment == 0) AddBusSegment();
        }
        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckOffset + 0.2f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, headCollisionRadius);
        }
    }
}