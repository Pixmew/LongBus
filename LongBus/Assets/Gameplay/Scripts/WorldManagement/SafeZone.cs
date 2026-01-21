using System.Collections;
using UnityEngine;
using DG.Tweening; // Needed for the flying animation

namespace PixmewStudios
{
    public class SafeZone : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Time in seconds between each passenger getting off.")]
        [SerializeField] private float unloadDelay = 0.15f;
        
        [Tooltip("The location where passengers walk/fly to.")]
        [SerializeField] private Transform dropOffPoint;

        [Header("Visuals")]
        [Tooltip("Prefab for the passenger flying out (optional).")]
        [SerializeField] private GameObject passengerVisualPrefab;

        private void OnTriggerEnter(Collider other)
        {
            // Check if the BUS (Head) entered the zone
            BusController bus = other.attachedRigidbody.GetComponent<BusController>();

            if (bus != null)
            {
                // Start the unloading sequence
                StartCoroutine(UnloadPassengersRoutine(bus));
            }
        }

        private IEnumerator UnloadPassengersRoutine(BusController bus)
        {
            // While the bus is larger than 2 segments (Head + 1 Body)...
            while (bus.SegmentCount > 2)
            {
                // 1. Remove the segment and get its position
                Vector3 exitPosition = bus.RemoveLastSegment();

                // 2. (Optional) Spawn a visual passenger flying to the destination
                if (passengerVisualPrefab != null && dropOffPoint != null)
                {
                    SpawnFlyingPassenger(exitPosition);
                }

                // 3. Wait before removing the next one (creates the smooth ripple effect)
                yield return new WaitForSeconds(unloadDelay);
            }
        }

        private void SpawnFlyingPassenger(Vector3 startPos)
        {
            GameObject p = Instantiate(passengerVisualPrefab, startPos, Quaternion.identity);
            
            // Make them jump from the bus to the drop-off point
            p.transform.DOJump(dropOffPoint.position, 3f, 1, 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    // Fade out or destroy when they arrive
                    Destroy(p, 0.1f);
                });
        }
    }
}