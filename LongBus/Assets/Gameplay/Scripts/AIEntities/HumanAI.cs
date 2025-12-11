using UnityEngine;
using DG.Tweening; // Still needed for the "Fly to Bus" animation

namespace PixmewStudios
{
    public class HumanAI : AIBaseComponent
    {
        [Header("Human Specific")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 3f;
        [SerializeField] private LayerMask zombieLayer; // Set this to Layer 7 (Zombie)
        [SerializeField] private float runSpeedMultiplier = 1.5f;

        // We use this flag to stop logic once we are safe
        private bool isSafe = false;

        protected override void Think()
        {
            // If we are already flying to the bus, don't think about zombies
            if (isSafe) return;

            Transform threat = FindNearestZombie();

            if (threat != null)
            {
                // FLEE: Calculate a point in the opposite direction
                Vector3 directionAway = (transform.position - threat.position).normalized;
                Vector3 runTarget = transform.position + directionAway * 5f; // Run 5 meters away
                
                // Pass the "Run Speed" to override the default walk speed
                MoveTowards(runTarget, runSpeed);
            }
            else
            {
                // CALM: Just walk around normally
                Wander(walkSpeed);
            }
        }

        private Transform FindNearestZombie()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, zombieLayer);
            if (hits.Length > 0) return hits[0].transform;
            return null;
        }

        // --- BUS INTERACTION ---
        
        private void OnTriggerEnter(Collider other)
        {
            if (isSafe) return;

            // Check if the Bus (specifically the collector part) hit us
            BusController bus = other.GetComponentInParent<BusController>();
            
            if (bus != null)
            {
                GetSaved(bus);
            }
        }

        private void GetSaved(BusController bus)
        {
            isSafe = true;

            // 1. CRITICAL: Turn off the custom gravity/movement logic
            // This prevents the BaseAI from snapping the human back to the floor
            DisablePhysics(); 

            // 2. Logic: Grow the bus
            bus.CollectPassenger();

            // 3. Visuals: Fly into the bus
            // We can safely use DOTween here because physics is disabled
            transform.DOJump(bus.transform.position, 3f, 1, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => Destroy(gameObject));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0,1,0,0.5f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
        }
    }
}