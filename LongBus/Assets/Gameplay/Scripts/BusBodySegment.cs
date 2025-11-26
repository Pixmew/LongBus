using UnityEngine;
using DG.Tweening;

namespace PixmewStudios
{
    public class BusBodySegment : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Assign the Child Mesh object here. We animate this so the Root can stay on the path.")]
        [SerializeField] internal Transform visuals;
        [SerializeField] internal int segmentIndex; 

        internal void PlaySpawnAnimation(float dropHeight = 10f)
        {
            if (visuals == null)
            {
                Debug.LogWarning($"BusBodySegment {name}: Visuals Transform is missing!");
                return;
            }

            // 1. Safety: Disable Collider on the ROOT so the head doesn't crash into it
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 2. Setup Initial State (Local Position)
            // The Root is already at the correct path position. 
            // We move the Visuals UP relative to that.
            visuals.localPosition = new Vector3(0, dropHeight, 0);
            visuals.localScale = Vector3.zero;

            // 3. Create Sequence
            Sequence spawnSeq = DOTween.Sequence();

            // A. Fall to Local Zero (The Root's position)
            spawnSeq.Append(visuals.DOLocalMove(Vector3.zero, 0.6f).SetEase(Ease.OutBounce));

            // B. Scale Up (Happens during fall)
            spawnSeq.Join(visuals.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));

            // C. On Landing
            spawnSeq.AppendCallback(() => {
                // Re-enable collision
                if (col != null) col.enabled = true;

                // Squash Effect
                visuals.DOPunchScale(new Vector3(0.3f, -0.3f, 0.3f), 0.4f, 10, 1);
            });
        }
    }
}