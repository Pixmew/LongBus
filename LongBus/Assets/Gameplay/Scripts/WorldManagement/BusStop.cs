using UnityEngine;
using System.Collections;
using DG.Tweening; // We use this for the flying animation

namespace PixmewStudios
{
    public class BusStop : MonoBehaviour
    {
        [Header("Passenger Settings")]
        [Tooltip("The visual prefab that represents a flying passenger.")]
        [SerializeField] private GameObject passengerVisualPrefab;
        
        [Tooltip("Minimum and Maximum passengers waiting at this stop.")]
        [SerializeField] private Vector2Int passengerCountRange = new Vector2Int(3, 8);
        
        [Header("Animation Settings")]
        [SerializeField] private float launchInterval = 0.1f; // Time between each passenger flying
        [SerializeField] private float jumpPower = 5f; // How high they arc
        [SerializeField] private float flightDuration = 0.5f; // How long they are in the air

        private bool hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            // Check if the object is the Bus
            BusController bus = other.GetComponentInParent<BusController>();
            
            if (bus != null)
            {
                hasTriggered = true;
                StartCoroutine(FeedPassengersToBus(bus));
            }
        }

        private IEnumerator FeedPassengersToBus(BusController bus)
        {
            // 1. Determine how many passengers are at this specific stop
            int count = Random.Range(passengerCountRange.x, passengerCountRange.y + 1);

            for (int i = 0; i < count; i++)
            {
                // 2. Spawn a visual passenger at the stop's position
                GameObject p = Instantiate(passengerVisualPrefab, transform.position, Quaternion.identity);

                // 3. Make them face the bus
                p.transform.LookAt(bus.transform);

                // 4. ANIMATION: Fly towards the bus!
                // We utilize DOTween's DOJump to create a nice arc.
                // We target the bus position.
                p.transform.DOJump(bus.transform.position, jumpPower, 1, flightDuration)
                    .SetEase(Ease.Linear)
                    .OnComplete(() => {
                        // This code runs when the passenger actually hits the bus
                        HandlePassengerArrival(bus, p);
                    });

                // Wait a tiny bit before launching the next one (creates a machine-gun feed effect)
                yield return new WaitForSeconds(launchInterval);
            }
            
            // Optional: Destroy the bus stop visuals after empty, or keep it empty.
            // Destroy(gameObject, flightDuration + 1f); 
        }

        private void HandlePassengerArrival(BusController bus, GameObject passengerVisual)
        {
            // 1. Logic: Tell the bus it collected someone
            bus.CollectPassenger();

            // 2. Visuals: Scale down to 0 to look like they went inside
            passengerVisual.transform.DOScale(Vector3.zero, 0.1f).OnComplete(() => {
                Destroy(passengerVisual);
            });
        }
    }
}