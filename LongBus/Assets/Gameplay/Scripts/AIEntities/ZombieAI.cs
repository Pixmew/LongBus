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

        protected override void Think()
        {
            Transform target = FindTarget();

            if (target != null)
            {
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

        private Transform FindTarget()
        {
            Transform bestTarget = null;
            float closestDist = Mathf.Infinity;

            // 1. Check for nearby humans
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, humanLayer);
            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = hit.transform;
                }
            }

            // 2. Check for the bus
            if (busTransform != null)
            {
                float busDist = Vector3.Distance(transform.position, busTransform.position);
                // If the bus is closer than the nearest human AND within detection range, chase it
                if (busDist < closestDist && busDist <= detectionRadius)
                {
                    bestTarget = busTransform;
                }
            }

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
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
        }
    }
}