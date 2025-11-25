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

        [Header("Visual Effects")]
        [Tooltip("Defines the Ease: X-Axis is normalized velocity (-1 is falling fast, 1 is jumping up). Y-Axis is the Pitch influence.")]
        [SerializeField] private AnimationCurve dolphinPitchCurve = new AnimationCurve(new Keyframe(-1, 1), new Keyframe(0, 0), new Keyframe(1, -1));

        [Tooltip("The maximum angle (in degrees) the nose will pitch up or down.")]
        [SerializeField] private float maxPitchAngle = 45f;

        [Header("Body Settings")]
        [SerializeField] private GameObject busSegmentPrefab;
        [SerializeField] private float segmentGap = 1.5f;

        [Header("Collision Settings")]
        [Tooltip("How many segments behind the head are safe to touch? (Prevents self-collision on tight turns)")]
        [SerializeField] private int safeSegmentCount = 4;
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
        [Tooltip("How many passengers need to be collected to get 1 new bus segment?")]
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
            pathPositions.Add(transform.position);
            pathRotations.Add(transform.rotation);

            // Safety check for Camera
            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                cameraController = Camera.main.transform.parent.GetComponent<CameraController>();
            }
        }

        private void OnEnable() => inputActions.BusControlsActionMap.Enable();
        private void OnDisable() => inputActions.BusControlsActionMap.Disable();

        private void LateUpdate()
        {
            CheckGround();
            HandleHeadMovement();
            RecordPath();
            MoveBodySegments();
        }

        #region Movement & Physics

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

            // --- 1. CALCULATE ROTATIONS ---

            // A. Look Direction (Y-Axis)
            Quaternion targetLookRotation = transform.rotation;
            if (moveDirection.magnitude >= 0.1f)
            {
                targetLookRotation = Quaternion.LookRotation(moveDirection);
            }

            // C. Dolphin Pitch (X-Axis) - EDITED FOR CURVE CONTROL
            float targetPitchAngle = 0f;

            if (!isGrounded)
            {
                // 1. Normalize velocity. 
                // +1 means we are moving up at full Jump Force. 
                // -1 means we are falling down at full Jump Force speed (or faster).
                float velocityRatio = Mathf.Clamp(verticalVelocity / jumpForce, -1.5f, 1.5f);

                // 2. Evaluate the curve. 
                // You define in Inspector how "Velocity Ratio" translates to "Pitch Percentage".
                float curveValue = dolphinPitchCurve.Evaluate(velocityRatio);

                // 3. Apply the max angle
                targetPitchAngle = curveValue * maxPitchAngle;
            }

            Quaternion pitchRotation = Quaternion.Euler(targetPitchAngle, 0, 0);

            // D. Combine All Rotations
            // Order: Look (Direction) -> Pitch (Up/Down)
            Quaternion finalTargetRotation = targetLookRotation * pitchRotation;

            // Smoothly rotate towards the final result
            transform.rotation = Quaternion.Slerp(transform.rotation, finalTargetRotation, rotationSpeed * Time.deltaTime);


            // --- 2. PHYSICS & GROUND SNAPPING ---
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

            // --- 3. APPLY MOVEMENT ---
            Vector3 forwardMovement = transform.forward;

            // Flatten forward vector so speed is consistent regardless of pitch
            forwardMovement.y = 0;
            forwardMovement.Normalize();
            forwardMovement *= moveSpeed;

            Vector3 verticalMovement = Vector3.up * verticalVelocity;

            transform.position += (forwardMovement + verticalMovement) * Time.deltaTime;
        }

        #endregion

        #region Path Logic

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
                int removeCount = pathPositions.Count - trimThreshold;
                pathPositions.RemoveRange(trimThreshold, removeCount);
                pathRotations.RemoveRange(trimThreshold, removeCount);
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

        #endregion

        #region Boost & Segments

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

        public void AddBusSegment()
        {
            if (busSegmentPrefab == null) return;
            Transform lastSegment = busSegments.Last();

            // Instantiate as normal
            GameObject newSegment = Instantiate(busSegmentPrefab, lastSegment.position, lastSegment.rotation);

            // 2. Add the identifier script
            BusBodySegment segID = newSegment.GetComponent<BusBodySegment>();
            segID.segmentIndex = busSegments.Count; // Assign index based on list count
                                                    // --- NEW LOGIC END ---

            busSegments.Add(newSegment.transform);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isDead) return;

            // Check if we hit a body segment
            BusBodySegment segmentHit = other.GetComponent<BusBodySegment>();

            if (segmentHit != null)
            {
                // Check if the segment is far enough down the chain to count as a crash
                // We don't want to crash into segment #1 or #2 just by turning slightly
                if (segmentHit.segmentIndex > safeSegmentCount)
                {
                    HandleCrash();
                }
            }
            // Note: You can add checks for "Obstacle" tags here too
        }

        private void HandleCrash()
        {
            isDead = true;
            moveSpeed = 0; // Stop immediately
            normalmoveSpeed = 0;
            boostedmoveSpeed = 0;

            // Kill any active tweens to prevent jitter
            transform.DOKill();

            Debug.Log("GAME OVER: Bus crashed into itself!");

            // Optional: Add a crash visual effect here (smoke, shake, explosion)
            if (cameraController != null) cameraController.TriggerShake(0.5f, 1f, 20);
        }
        public void CollectPassenger()
        {
            totalPassengersCollected++;

            // Check if we hit the threshold to grow
            // The modulus operator (%) checks if the remainder is 0
            if (totalPassengersCollected % passengersPerSegment == 0)
            {
                AddBusSegment();

                // Optional: Play a "Level Up" or "Grow" sound here
                Debug.Log("Bus Grew! Total Passengers: " + totalPassengersCollected);
            }
        }

        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckOffset + 0.2f));
            Gizmos.DrawSphere(transform.position + Vector3.down * groundCheckOffset, 0.1f);
        }
    }
}