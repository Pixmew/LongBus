using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class ChunkSpawner : MonoBehaviour
    {
        [Header("Mob Prefabs")]
        [SerializeField] private HumanAI humanPrefab;
        [SerializeField] private ZombieAI zombiePrefab;

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
                float roll = Random.value;

                if (roll < humanChance)
                {
                    HumanAI human = SpawnObject(humanPrefab.gameObject, point).GetComponent<HumanAI>();
                    human.Init();
                }
                else if (roll < humanChance + zombieChance)
                {
                    ZombieAI zombie = SpawnObject(zombiePrefab.gameObject, point).GetComponent<ZombieAI>();
                    zombie.Init();
                }
                // Else: Do nothing (leave empty)
            }
        }

        private GameObject SpawnObject(GameObject prefab, Transform point)
        {
            if (prefab == null) return null;
            if (spawns == null)
            {
                spawns = new List<GameObject>();
            }

            GameObject mob = PoolManager.Instance.SpawnFromPool(prefab, point.position + Vector3.up * 0.5f, point.rotation, transform);
            mob.transform.SetParent(this.transform);
            spawns.Add(mob);
            return  mob;
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