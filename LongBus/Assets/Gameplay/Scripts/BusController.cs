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
        [SerializeField] private float gravity = 30f; // Snappier gravity for arcade feel
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("Distance from center of object to the floor. 0.5 for a 1m tall cube.")]
        [SerializeField] private float groundCheckOffset = 0.55f; 

        [Header("Visual Effects (Banking)")]
        [SerializeField] private float maxLeanAngle = 15f; 
        [SerializeField] private float leanSpeed = 5f;

        [Header("Body Settings")]
        [SerializeField] private GameObject busSegmentPrefab;
        [Tooltip("Distance between segments along the path.")]
        [SerializeField] private float segmentGap = 1.5f;
        
        // --- Internal Variables ---

        // Path History ("Breadcrumbs")
        private List<Vector3> pathPositions = new List<Vector3>();
        private List<Quaternion> pathRotations = new List<Quaternion>();

        private List<Transform> busSegments = new List<Transform>();
        private BusControls inputActions;
        private Vector2 moveInput;
        
        // Boost State
        private bool isBoosting = false;
        private Tween speedTween;
        private CameraController cameraController;

        // Physics State
        private float verticalVelocity;
        private bool isGrounded;
        private RaycastHit groundHit; // Stores exactly where the floor is

        private void Awake()
        {
            // Input Setup
            inputActions = new BusControls();
            inputActions.BusControlsActionMap.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            inputActions.BusControlsActionMap.Move.canceled += ctx => moveInput = Vector2.zero;
            inputActions.BusControlsActionMap.AddSegment.performed += _ => AddBusSegment();
            inputActions.BusControlsActionMap.BoostSpeed.performed += boostlogic => StartBoost();
            inputActions.BusControlsActionMap.BoostSpeed.canceled += boostlogic => StopBoost();
            inputActions.BusControlsActionMap.Jump.performed += jump => Jump();

            // Initialize List
            busSegments.Add(this.transform);
            
            // Initialize Path
            pathPositions.Add(transform.position);
            pathRotations.Add(transform.rotation);

            // Find Camera (Optional safety check)
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
            // Raycast down to find the floor. 
            // We add a small buffer (+0.2f) so we detect the ground slightly before hitting it.
            isGrounded = Physics.Raycast(transform.position, Vector3.down, out groundHit, groundCheckOffset + 0.2f, groundLayer);
        }

        private void Jump()
        {
            if (isGrounded)
            {
                verticalVelocity = jumpForce;
                isGrounded = false;
                
                // Nudge up slightly so we don't immediately snap back to ground in the next frame
                transform.position += Vector3.up * 0.1f;
            }
        }

        private void HandleHeadMovement()
        {
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;

            // --- 1. Rotation & Banking ---
            Quaternion targetLookRotation = transform.rotation;
            if (moveDirection.magnitude >= 0.1f)
            {
                targetLookRotation = Quaternion.LookRotation(moveDirection);
            }

            // Calculate Bank (Lean) Angle
            float targetLeanAngle = -moveInput.x * maxLeanAngle;
            Quaternion leanRotation = Quaternion.Euler(0, 0, targetLeanAngle);
            
            // Apply smoothed rotation
            Quaternion finalTargetRotation = targetLookRotation * leanRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, finalTargetRotation, leanSpeed * Time.fixedDeltaTime);

            // --- 2. Physics & Ground Snapping ---
            
            // If we are grounded and NOT moving upwards (jumping)
            if (isGrounded && verticalVelocity <= 0)
            {
                verticalVelocity = 0; 
                
                // SNAP to the floor height to prevent falling through or floating
                Vector3 snappedPos = transform.position;
                snappedPos.y = groundHit.point.y + groundCheckOffset;
                transform.position = snappedPos;
            }
            else
            {
                // Apply Gravity
                verticalVelocity -= gravity * Time.fixedDeltaTime;
            }

            // --- 3. Final Translation ---
            Vector3 forwardMovement = transform.forward;
            forwardMovement.y = 0; // Flatten forward vector so looking up/down doesn't affect speed
            forwardMovement.Normalize();
            forwardMovement *= moveSpeed;

            Vector3 verticalMovement = Vector3.up * verticalVelocity;

            transform.position += (forwardMovement + verticalMovement) * Time.fixedDeltaTime;
        }

        #endregion

        #region Path Logic (The Snake Effect)

        private void RecordPath()
        {
            // Optimization: Check squared distance to avoid expensive Sqrt calculations
            float distSqr = (pathPositions.Count > 0) ? (pathPositions[0] - transform.position).sqrMagnitude : 1f;
            
            // Only record if we moved slightly (prevents duplicate points when idle)
            if (distSqr > 0.0001f) 
            {
                pathPositions.Insert(0, transform.position);
                pathRotations.Insert(0, transform.rotation);
            }
        }

        private void MoveBodySegments()
        {
            float totalDistanceNeeded = 0f;
            int maxIndexUsed = 0; // Track how much history we actually need

            for (int i = 1; i < busSegments.Count; i++)
            {
                totalDistanceNeeded += segmentGap;
                
                // Move the segment and get back the index of the path it used
                int usedIndex = SetSegmentToPathDistance(busSegments[i], totalDistanceNeeded);
                
                if (usedIndex > maxIndexUsed) maxIndexUsed = usedIndex;
            }

            // --- Dynamic Cleanup ---
            // Remove history points that are older than the last segment needs
            // We keep a buffer of 5 points to prevent jitter
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

            // Search through the breadcrumbs to find where this segment belongs
            for (int i = 0; i < pathPositions.Count - 1; i++)
            {
                Vector3 pointA = pathPositions[i];
                Vector3 pointB = pathPositions[i + 1];
                
                float distBetweenPoints = Vector3.Distance(pointA, pointB);

                if (currentDistTraveled + distBetweenPoints >= requiredDist)
                {
                    // We found the spot between Point A and Point B
                    float remainingDist = requiredDist - currentDistTraveled;
                    float t = remainingDist / distBetweenPoints;

                    segment.position = Vector3.Lerp(pointA, pointB, t);
                    segment.rotation = Quaternion.Slerp(pathRotations[i], pathRotations[i + 1], t);
                    
                    return i; // Return the index used
                }

                currentDistTraveled += distBetweenPoints;
            }
            
            // Fallback: If path is too short (start of game), stack at the end
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
            speedTween = DOTween.To(() => moveSpeed, x => moveSpeed = x, boostedmoveSpeed, boostRampDuration)
                .SetEase(Ease.InOutQuad);
            
            if(cameraController != null) cameraController.StartContinuousShake();
        }

        private void StopBoost()
        {
            speedTween?.Kill();
            speedTween = DOTween.To(() => moveSpeed, x => moveSpeed = x, normalmoveSpeed, boostRampDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if(cameraController != null) cameraController.StopShake();
                    isBoosting = false;
                });
        }

        public void AddBusSegment()
        {
            if (busSegmentPrefab == null) return;

            Transform lastSegment = busSegments.Last();
            // Spawn at last segment position; path logic will correct it instantly in Update
            GameObject newSegment = Instantiate(busSegmentPrefab, lastSegment.position, lastSegment.rotation);
            busSegments.Add(newSegment.transform);
        }

        #endregion

        // Visual Debugging
        private void OnDrawGizmos()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckOffset + 0.2f));
            Gizmos.DrawSphere(transform.position + Vector3.down * groundCheckOffset, 0.1f);
        }
    }
}