using UnityEngine;
using DG.Tweening; // We still keep this for the 'Jump' effect!

namespace PixmewStudios
{
    public abstract class AIBaseComponent : MonoBehaviour
    {
        [Header("Movement Stats")]
        [SerializeField] protected float moveSpeed = 4f;
        [SerializeField] protected float rotationSpeed = 10f;
        [SerializeField] protected float detectionRadius = 10f;

        [Header("Physics")]
        [SerializeField] protected float gravity = 20f;
        [SerializeField] protected LayerMask groundLayer;
        [SerializeField] protected float groundOffset = 0.0f;

        [Header("Wander Settings")]
        [SerializeField] protected float wanderInterval = 3f;
        [SerializeField] protected float wanderRadius = 5f;
        [SerializeField] protected Rigidbody rigidbody;

        // Internal State
        protected Vector3 currentTarget;
        protected bool hasTarget = false;
        protected float wanderTimer;

        // Physics State
        protected float verticalVelocity;
        protected bool isGrounded;
        protected bool usePhysics = true; // Flag to disable movement when flying to bus

        internal virtual void Init()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

        protected virtual void Update()
        {
            if (!usePhysics) return; // Stop logic if we are being collected

            Think();       // 1. Decide where to go
            ApplyGravity(); // 2. Calculate Vertical force
            Move();        // 3. Apply Horizontal movement
        }

        protected abstract void Think();

        // --- 1. MOVEMENT LOGIC ---

        protected void MoveTowards(Vector3 targetPos, float speedOverride = -1)
        {
            currentTarget = targetPos;
            hasTarget = true;

            // Allow temporary speed boost (running)
            if (speedOverride > 0)
                moveSpeed = speedOverride; // Note: You might want to reset this later
        }

        private void Move()
        {
            if (!hasTarget) return;

            // A. ROTATION
            // Calculate direction to target (ignoring Y height)
            Vector3 direction = (currentTarget - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
            }

            // B. MOVEMENT
            // Move forward based on where we are facing
            // Check if we are close enough to stop jittering
            if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                 new Vector3(currentTarget.x, 0, currentTarget.z)) > 0.5f)
            {
                Vector3 moveVector = transform.forward * moveSpeed;
                moveVector.y = 0; // Keep horizontal movement separate from gravity
                transform.position += moveVector * Time.deltaTime;
            }
        }

        // --- 2. PHYSICS LOGIC ---

        private void ApplyGravity()
        {
            // Raycast down to find ground
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 1.0f; // Start ray from waist height

            // Check strictly downwards
            bool hitGround = Physics.Raycast(rayStart, Vector3.down, out hit, 1.2f, groundLayer);

            if (hitGround)
            {
                isGrounded = true;
                verticalVelocity = 0;

                // Snap to ground level so we don't float or sink
                Vector3 pos = transform.position;
                pos.y = hit.point.y + groundOffset;
                transform.position = pos;
            }
            else
            {
                isGrounded = false;
                // Apply Gravity
                verticalVelocity -= gravity * Time.deltaTime;
                transform.position += Vector3.up * verticalVelocity * Time.deltaTime;
            }
        }

        // --- 3. WANDER LOGIC ---

        protected void Wander(float speedOverride = -1)
        {
            wanderTimer -= Time.deltaTime;

            // Only pick a new spot if time is up OR we are very close to the old spot
            if (wanderTimer <= 0 || (hasTarget && Vector3.Distance(transform.position, currentTarget) < 1f))
            {
                Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
                Vector3 wanderPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

                MoveTowards(wanderPos , speedOverride);
                wanderTimer = wanderInterval;
            }
        }

        // --- 4. INTERACTION ---

        public void DisablePhysics()
        {
            usePhysics = false;
            hasTarget = false;

            // Disable collider
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            
            // Disable physics simulation to prevent anchoring the parent object!
            if (rigidbody != null) rigidbody.isKinematic = true;
        }
    }
}