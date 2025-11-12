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
        [Tooltip("How much the bus tilts Left/Right when turning (Banking).")]
        [SerializeField] private float maxLeanAngle = 15f; 
        [SerializeField] private float leanSpeed = 5f;
        [Tooltip("How much the nose points Up/Down when jumping (Dolphin Effect).")]
        [SerializeField] private float dolphinPitchStrength = 3f; // New Variable

        [Header("Body Settings")]
        [SerializeField] private GameObject busSegmentPrefab;
        [SerializeField] private float segmentGap = 1.5f;
        
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

            var camTransform = Camera.main.transform.parent;
            if (camTransform != null) cameraController = camTransform.GetComponent<CameraController>();
        }

        private void OnEnable() => inputActions.BusControlsActionMap.Enable();
        private void OnDisable() => inputActions.BusControlsActionMap.Disable();

        private void FixedUpdate()
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

            // B. Banking (Z-Axis) - Leaning into turns
            float targetBankAngle = -moveInput.x * maxLeanAngle;
            Quaternion bankRotation = Quaternion.Euler(0, 0, targetBankAngle);

            // C. Dolphin Pitch (X-Axis) - Looking up/down based on jump
            float targetPitchAngle = 0f;
            if (!isGrounded)
            {
                // -verticalVelocity means: Positive Velocity (Going Up) = Negative Angle (Nose Up)
                targetPitchAngle = -verticalVelocity * dolphinPitchStrength;
                
                // Clamp it so we don't do a backflip (limit to 45 degrees up/down)
                targetPitchAngle = Mathf.Clamp(targetPitchAngle, -45f, 45f);
            }
            Quaternion pitchRotation = Quaternion.Euler(targetPitchAngle, 0, 0);

            // D. Combine All Rotations
            // Order: Look (Direction) -> Pitch (Up/Down) -> Bank (Tilt)
            Quaternion finalTargetRotation = targetLookRotation * pitchRotation * bankRotation;

            // Smoothly rotate towards the final result
            transform.rotation = Quaternion.Slerp(transform.rotation, finalTargetRotation, leanSpeed * Time.fixedDeltaTime);


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
                verticalVelocity -= gravity * Time.fixedDeltaTime;
            }

            // --- 3. APPLY MOVEMENT ---
            Vector3 forwardMovement = transform.forward;
            
            // IMPORTANT: When dolphin pitching, 'forward' points up/down. 
            // We must Flatten it for movement calculation so speed is consistent over ground.
            // However, keeping a little bit of the Y component makes the jump look like it follows the nose!
            // Let's flatten it completely for gameplay control stability:
            forwardMovement.y = 0; 
            
            forwardMovement.Normalize();
            forwardMovement *= moveSpeed;

            Vector3 verticalMovement = Vector3.up * verticalVelocity;

            transform.position += (forwardMovement + verticalMovement) * Time.fixedDeltaTime;
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
            if(cameraController != null) cameraController.StartContinuousShake();
        }

        private void StopBoost()
        {
            speedTween?.Kill();
            speedTween = DOTween.To(() => moveSpeed, x => moveSpeed = x, normalmoveSpeed, boostRampDuration).SetEase(Ease.OutQuad)
                .OnComplete(() => { if(cameraController != null) cameraController.StopShake(); isBoosting = false; });
        }

        public void AddBusSegment()
        {
            if (busSegmentPrefab == null) return;
            Transform lastSegment = busSegments.Last();
            GameObject newSegment = Instantiate(busSegmentPrefab, lastSegment.position, lastSegment.rotation);
            busSegments.Add(newSegment.transform);
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