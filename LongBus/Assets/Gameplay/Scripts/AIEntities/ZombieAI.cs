using System;
using UnityEngine;

namespace PixmewStudios
{
    public class ZombieAI : AIBaseComponent
    {
        [Header("Zombie Specific")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 3f;
        [SerializeField] private LayerMask humanLayer; // Set this to Layer 6 (Human)
        [SerializeField] private LayerMask busLayer;
        [SerializeField] private Animator animator;
        [SerializeField] private bool isDead;

        private float currentRunSpeed;

        private Transform busTransform;

        internal override void Init()
        {
            base.Init();
            currentRunSpeed = runSpeed;
            if (busTransform == null)
            {
                var bus = FindObjectOfType<BusController>();
                if (bus != null) busTransform = bus.transform;
            }
        }

        internal void InitWithSpeed(float speedMultiplier)
        {
            base.Init();
            currentRunSpeed = runSpeed * speedMultiplier;
            if (busTransform == null)
            {
                var bus = FindObjectOfType<BusController>();
                if (bus != null) busTransform = bus.transform;
            }
        }

        [Header("Latching")]
        [SerializeField] private float latchDistance = 1.5f;
        private bool isLatched = false;

        protected override void Think()
        {
            if (isLatched) return;

            Transform target = FindTarget();

            if (target != null)
            {
                // If the target is a BusBodySegment (or head) and we're close enough, try latching
                if (target == busTransform || target.GetComponentInParent<BusBodySegment>() != null)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    if (dist <= latchDistance)
                    {
                        Debug.Log("Latching!");
                        TryLatch(target);
                        return;
                    }
                }

                // CHASE: Tell the BaseAI where the target is right now.
                MoveTowards(target.position, currentRunSpeed);
                animator.SetTrigger("Run");
                animator.SetInteger("RunType", UnityEngine.Random.Range(0, 2));
            }
            else
            {
                // IDLE: pick random spots.
                Wander(walkSpeed);
                if (wanderTimer <= 0)
                {
                    animator.SetTrigger("Idle");
                }
                else
                {
                    animator.SetTrigger("Walk");
                }
            }
        }

        private void TryLatch(Transform targetSegment)
        {
            //BusBodySegment segment = targetSegment.GetComponent<BusBodySegment>();
            BusBodySegment segment = targetSegment.GetComponentInParent<BusBodySegment>();
Debug.Log(targetSegment.name);
            if (segment != null)
            {
                Debug.Log("Latch..............");
                isLatched = true;
                DisablePhysics(); // Stop gravity and base movement
                
                segment.AddLatchedZombie(this);
                
                // Play a clinging animation (fallback to idle/walk if cling doesn't exist)
                animator.SetTrigger("Die"); 
            }
        }

        private Transform FindTarget()
        {
            Transform bestTarget = null;
            float closestDist = Mathf.Infinity;

            // Combine Human layer with the BusBody layer
            int searchMask = humanLayer.value | busLayer;

            // 1. Check for nearby targets (Humans and Bus Segments)
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, searchMask);
            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = hit.transform;
                }
            }

            // // 2. Fallback to main bus transform (in case head isn't on BusBody layer)
            // if (busTransform != null)
            // {
            //     float busDist = Vector3.Distance(transform.position, busTransform.position);
            //     // If the bus head is closer than the nearest target AND within detection range, chase it
            //     if (busDist < closestDist && busDist <= detectionRadius)
            //     {
            //         bestTarget = busTransform;
            //     }
            // }

            return bestTarget;
        }

        internal void Death(Vector3 hitpoint)
        {
            if (isDead) return;
            isDead = true;
            animator.SetTrigger("Die");
            rigidbody.isKinematic = false;
            this.enabled = false;
            rigidbody.AddExplosionForce(10, hitpoint, 5f , 1 , ForceMode.Impulse);
            RefrenceHolder.Instance.cameraController.TriggerShake(0.2f , 0.2f);
            
            // Score Integration
            if (ScoreManager.Instance != null)
            {
                bool drifting = false;
                if (busTransform != null)
                {
                    BusController bus = busTransform.GetComponent<BusController>();
                    if (bus != null) drifting = bus.IsDrifting;
                }
                
                ScoreManager.Instance.AddScore(5, drifting);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
        }
    }
}