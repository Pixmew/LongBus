using UnityEngine;

namespace PixmewStudios
{
    [RequireComponent(typeof(Collider))]
    public class DestructibleProp : MonoBehaviour
    {
        [Header("Destruction Settings")]
        [SerializeField] private float explosionForce = 15f;
        [SerializeField] private float explosionRadius = 5f;
        [SerializeField] private float upwardModifier = 1f;
        [SerializeField] private float destroyDelay = 3f;
        
        [Header("Effects")]
        [Tooltip("Optional particle system to spawn on impact (like dust/wood splinters)")]
        [SerializeField] private GameObject destructionParticlePrefab;
        [SerializeField] private int baseScoreValue = 10;
        
        private bool isDestroyed = false;
        private Rigidbody rb;
        private Collider col;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            
            // If the prop doesn't have a Rigidbody, add one but make it kinematic so it stays in place
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isDestroyed) return;

            // Check if the object colliding with us is the bus (head or segment)
            if (collision.rigidbody.GetComponentInParent<BusController>() != null || 
                collision.rigidbody.GetComponent<BusBodySegment>() != null)
            {
                Smash(collision.contacts[0].point, collision.gameObject.transform.position);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (isDestroyed) return;
            
            if (other.attachedRigidbody.GetComponent<BusController>() != null || 
                other.attachedRigidbody.GetComponent<BusBodySegment>() != null)
            {
                Smash(other.ClosestPoint(transform.position), other.transform.position);
            }
        }

        private void Smash(Vector3 impactPoint, Vector3 sourcePosition)
        {
            isDestroyed = true;
            
            // Enable physics
            rb.isKinematic = false;
            
            // Calculate a force pushing away from the bus and slightly up
            Vector3 forceDirection = (transform.position - sourcePosition).normalized;
            forceDirection.y = upwardModifier;
            
            rb.AddForce(forceDirection * explosionForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * explosionForce, ForceMode.Impulse);

            // Spawn Particles
            if (destructionParticlePrefab != null)
            {
                Instantiate(destructionParticlePrefab, impactPoint, Quaternion.identity);
            }

            // Award points (Assuming ScoreManager is added later, will safely null check)
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(baseScoreValue);
            }

            // Clean up later to save memory
            Destroy(gameObject, destroyDelay);
        }
    }
}
