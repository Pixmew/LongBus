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

        protected override void Think()
        {
            Transform target = FindNearestHuman();

            if (target != null)
            {
                // CHASE: Just tell the BaseAI where the human is right now.
                // Since we use vector math, this is super cheap to call every frame.
                MoveTowards(target.position, runSpeed);
                animator.SetTrigger("Run");
                animator.SetInteger("RunType", UnityEngine.Random.Range(0, 2));
            }
            else
            {
                // IDLE: No humans? Just pick random spots.
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

        private Transform FindNearestHuman()
        {
            // Simple overlap check to find nearby colliders on the "Human" layer
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, humanLayer);

            Transform bestTarget = null;
            float closestDist = Mathf.Infinity;

            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = hit.transform;
                }
            }
            return bestTarget;
        }

        internal void Death(Vector3 hitpoint)
        {
            if (isDead) return;
            isDead = true;

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