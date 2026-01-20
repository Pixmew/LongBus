using System.Collections;
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

        [Header("Dynamic Spawning Rules")]
        [Tooltip("How close the bus needs to be to spawn mobs.")]
        [SerializeField] private float activationDistance = 40f;
        [Tooltip("How far the bus needs to be to remove mobs.")]
        [SerializeField] private float deactivationDistance = 60f;
        [Tooltip("Check distance every X seconds (Performance optimization).")]
        [SerializeField] private float checkInterval = 0.5f;

        [Header("Endless Mode Settings")]
        [Range(0f, 1f)][SerializeField] private float humanChance = 0.3f; // 30% chance
        [Range(0f, 1f)][SerializeField] private float zombieChance = 0.3f; // 30% chance

        // Internal State
        [SerializeField] internal List<GameObject> activeSpawns = new List<GameObject>();
        private Transform playerTransform;
        private bool isChunkActive = false;

        private void Start()
        {
            // Find the bus (Assuming the tag is "Player" or finding by type)
            BusController bus = FindObjectOfType<BusController>();
            if (bus != null)
            {
                playerTransform = bus.transform;
                // Start the efficient checking loop
                StartCoroutine(CheckDistanceRoutine());
            }
            else
            {
                Debug.LogWarning("ChunkSpawner could not find the Bus (BusController)!");
            }
        }

        private IEnumerator CheckDistanceRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(checkInterval);

            while (true)
            {
                if (playerTransform != null)
                {
                    float dist = Vector3.Distance(transform.position, playerTransform.position);

                    if (!isChunkActive && dist <= activationDistance)
                    {
                        ActivateChunk();
                    }
                    else if (isChunkActive && dist > deactivationDistance)
                    {
                        DeactivateChunk();
                    }
                }
                yield return wait;
            }
        }

        private void ActivateChunk()
        {
            isChunkActive = true;
            SpawnMobs();
        }

        private void DeactivateChunk()
        {
            isChunkActive = false;
            DespawnMobs();
        }

        private void SpawnMobs()
        {
            // If we somehow already have spawns, clear them first just in case
            if (activeSpawns.Count > 0) DespawnMobs();

            foreach (Transform point in spawnPoints)
            {
                float roll = Random.value;

                // We add Vector3.up to ensure they don't spawn inside the floor
                Vector3 spawnPos = point.position; // + Vector3.up * 0.5f; 

                if (roll < humanChance)
                {
                    GameObject obj = SpawnObject(humanPrefab.gameObject, spawnPos, point.rotation);
                    if (obj != null) obj.GetComponent<HumanAI>().Init();
                }
                else if (roll < humanChance + zombieChance)
                {
                    GameObject obj = SpawnObject(zombiePrefab.gameObject, spawnPos, point.rotation);
                    if (obj != null) obj.GetComponent<ZombieAI>().Init();
                }
                // Else: Slot remains empty
            }
        }

        private void DespawnMobs()
        {
            if (PoolManager.Instance != null)
            {
                foreach (GameObject spawn in activeSpawns)
                {
                    // Check null in case the player already ate/destroyed the mob
                    if (spawn != null && spawn.activeSelf) 
                    {
                        PoolManager.Instance.ReturnToPool(spawn);
                    }
                }
            }
            activeSpawns.Clear();
        }

        private GameObject SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;

            GameObject mob = PoolManager.Instance.SpawnFromPool(prefab, pos, rot, transform);
            // Parent to this chunk so they move with it (if chunks move)
            mob.transform.SetParent(this.transform); 
            
            activeSpawns.Add(mob);
            return mob;
        }

        // Cleanup if the chunk itself is destroyed
        void OnDestroy()
        {
            DespawnMobs();
        }
    }
}