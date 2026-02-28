using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class ChunkSpawner : MonoBehaviour
    {
        [Header("Spawn Locations")]
        [Tooltip("Drag empty child GameObjects representing sidewalks/spawn spots here.")]
        public Transform[] spawnPoints;

        private void OnEnable()
        {
            if (MobManager.Instance != null && spawnPoints != null)
            {
                foreach (var point in spawnPoints)
                {
                    MobManager.Instance.RegisterSpawnPoint(point);
                }
            }
        }

        private void OnDisable()
        {
            if (MobManager.Instance != null && spawnPoints != null)
            {
                foreach (var point in spawnPoints)
                {
                    MobManager.Instance.UnregisterSpawnPoint(point);
                }
            }
        }
    }
}