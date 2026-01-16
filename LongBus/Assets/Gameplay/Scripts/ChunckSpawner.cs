using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class ChunkSpawner : MonoBehaviour
    {
        [Header("Mob Prefabs")]
        [SerializeField] private GameObject humanPrefab;
        [SerializeField] private GameObject zombiePrefab;

        [Header("Spawn Locations")]
        [Tooltip("Drag empty child GameObjects representing sidewalks/spawn spots here.")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Endless Mode Settings")]
        [Range(0f, 1f)][SerializeField] private float humanChance = 0.3f; // 30% chance
        [Range(0f, 1f)][SerializeField] private float zombieChance = 0.3f; // 30% chance

        [SerializeField] internal List<GameObject> spawns;
        // remaining 40% will be empty

        private void Start()
        {
            SpawnMobs();
        }

        private void SpawnMobs()
        {
            foreach (Transform point in spawnPoints)
            {
                // Roll a single random number between 0.0 and 1.0
                float roll = Random.value;

                // Logic: 0.0 --[Human]-- 0.3 --[Zombie]-- 0.6 --[Empty]-- 1.0

                if (roll < humanChance)
                {
                    // Spawn Human
                    SpawnObject(humanPrefab, point);
                }
                else if (roll < humanChance + zombieChance)
                {
                    // Spawn Zombie
                    SpawnObject(zombiePrefab, point);
                }
                // Else: Do nothing (leave empty)
            }
        }

        private void SpawnObject(GameObject prefab, Transform point)
        {
            if (prefab == null) return;
            if (spawns == null)
            {
                spawns = new List<GameObject>();
            }

            // Instantiate and parent to the Chunk so it gets destroyed automatically
            // when InfiniteTerrainSystem removes the chunk.
            GameObject mob = PoolManager.Instance.SpawnFromPool(prefab, point.position, point.rotation, transform);
            mob.transform.SetParent(this.transform);
            spawns.Add(mob);
        }

        void OnDestroy()
        {
            if (PoolManager.Instance != null)
            {
                foreach (GameObject spawn in spawns)
                {
                    if (spawn != null)
                    {
                        PoolManager.Instance.ReturnToPool(spawn);
                    }
                }
            }
        }
    }
}