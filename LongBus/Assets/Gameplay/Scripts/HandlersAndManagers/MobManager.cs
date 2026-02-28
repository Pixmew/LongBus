using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace PixmewStudios
{
    public class MobManager : MonoBehaviour
    {
        public static MobManager Instance;

        [Header("Mob Prefabs")]
        [SerializeField] private HumanAI humanPrefab;
        [SerializeField] private ZombieAI zombiePrefab;

        [Header("Ambient Spawning Settings")]
        [Tooltip("Maximum amount of mobs allowed at once.")]
        [SerializeField] private int maxActiveMobs = 30;
        [Tooltip("Spawn distance radius ranges.")]
        [SerializeField] private float minSpawnDistance = 25f;
        [SerializeField] private float maxSpawnDistance = 45f;
        [Tooltip("Despawn distance if bus goes too far.")]
        [SerializeField] private float despawnDistance = 60f;
        [SerializeField] private float humanChance = 0.5f;
        [SerializeField] private float ambientSpawnInterval = 1f;

        [Header("Wave Spawning Settings")]
        [Tooltip("How often a wave spawns (seconds).")]
        [SerializeField] private float waveInterval = 30f;
        [Tooltip("Base cluster size on level 1.")]
        [SerializeField] private int baseWaveZombieCount = 5;

        private Transform playerTransform;
        private HashSet<Transform> availableSpawnPoints = new HashSet<Transform>();
        private List<GameObject> activeMobs = new List<GameObject>();

        private float ambientSpawnTimer = 0f;
        private float waveTimer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            BusController bus = FindObjectOfType<BusController>();
            if (bus != null)
            {
                playerTransform = bus.transform;
            }
            else
            {
                Debug.LogWarning("MobManager could not find the Bus (BusController)!");
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            HandleDespawning();

            ambientSpawnTimer += Time.deltaTime;
            if (ambientSpawnTimer >= ambientSpawnInterval)
            {
                ambientSpawnTimer = 0f;
                HandleAmbientSpawning();
            }

            int currentLevel = RefrenceHolder.Instance.gameProgressHandler.CurrentProgressLevel;
            // Reduce interval by 2 seconds per level, clamping to a minimum of 10 seconds.
            float currentWaveInterval = Mathf.Max(10f, waveInterval - (currentLevel * 2f));

            waveTimer += Time.deltaTime;
            if (waveTimer >= currentWaveInterval)
            {
                waveTimer = 0f;
                TriggerZombieWave();
            }
        }

        public void RegisterSpawnPoint(Transform spawnPoint)
        {
            if (!availableSpawnPoints.Contains(spawnPoint))
            {
                availableSpawnPoints.Add(spawnPoint);
            }
        }

        public void UnregisterSpawnPoint(Transform spawnPoint)
        {
            if (availableSpawnPoints.Contains(spawnPoint))
            {
                availableSpawnPoints.Remove(spawnPoint);
            }
        }

        private void HandleAmbientSpawning()
        {
            if (activeMobs.Count >= maxActiveMobs || availableSpawnPoints.Count == 0) return;

            // Find valid spawn points within the donut ring (minDistance to maxDistance)
            List<Transform> validPoints = new List<Transform>();
            foreach (Transform pt in availableSpawnPoints)
            {
                float dist = Vector3.Distance(pt.position, playerTransform.position);
                if (dist >= minSpawnDistance && dist <= maxSpawnDistance)
                {
                    validPoints.Add(pt);
                }
            }

            if (validPoints.Count == 0) return;

            Transform chosenPoint = validPoints[Random.Range(0, validPoints.Count)];
            float roll = Random.value;
            GameObject prefabToSpawn = (roll < humanChance) ? humanPrefab.gameObject : zombiePrefab.gameObject;

            SpawnMob(prefabToSpawn, chosenPoint.position, chosenPoint.rotation);
        }

        private void TriggerZombieWave()
        {
            int currentLevel = RefrenceHolder.Instance.gameProgressHandler.CurrentProgressLevel;
            // E.g., Level 1 -> 5 zombies, Level 2 -> 7 zombies, Level 3 -> 9 zombies
            int targetWaveCount = baseWaveZombieCount + (currentLevel * 2);

            // Find valid spawn points slightly ahead of the player if possible, or just within max distance
            List<Transform> wavePoints = new List<Transform>();
            foreach (Transform pt in availableSpawnPoints)
            {
                float dist = Vector3.Distance(pt.position, playerTransform.position);
                if (dist >= minSpawnDistance && dist <= maxSpawnDistance)
                {
                    wavePoints.Add(pt);
                }
            }

            if (wavePoints.Count == 0) return;
            
            // Try to spawn a cluster at one spot or spread them out slightly
            Transform waveCenter = wavePoints[Random.Range(0, wavePoints.Count)];

            int spawnedCount = 0;
            float speedMultiplier = 1f + (currentLevel * 0.15f); // 15% faster per level

            // Spawn multiple zombies at the wave center over multiple nearby points
            foreach(Transform pt in wavePoints.OrderBy(p => Vector3.Distance(p.position, waveCenter.position)))
            {
                if (spawnedCount >= targetWaveCount) break;
                if (activeMobs.Count >= maxActiveMobs) break; // Don't exceed total max count by too much

                GameObject zombieObj = SpawnMob(zombiePrefab.gameObject, pt.position, pt.rotation);
                if (zombieObj != null)
                {
                    ZombieAI ai = zombieObj.GetComponent<ZombieAI>();
                    if (ai != null)
                    {
                        ai.InitWithSpeed(speedMultiplier); // Pass speed multiplier
                    }
                }
                
                spawnedCount++;
            }
        }

        private GameObject SpawnMob(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;

            GameObject mob = PoolManager.Instance.SpawnFromPool(prefab, pos, rot, null); // Don't parent to chunk to prevent death on chunk destroy
            activeMobs.Add(mob);

            var aiBase = mob.GetComponent<AIBaseComponent>();
            if (aiBase != null)
            {
                if (aiBase is HumanAI humanAI) humanAI.Init();
                else if (aiBase is ZombieAI zombieAI) zombieAI.Init();
            }

            return mob;
        }

        private void HandleDespawning()
        {
            for (int i = activeMobs.Count - 1; i >= 0; i--)
            {
                GameObject mob = activeMobs[i];
                if (mob == null || !mob.activeSelf)
                {
                    activeMobs.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(mob.transform.position, playerTransform.position);
                if (dist > despawnDistance)
                {
                    PoolManager.Instance.ReturnToPool(mob);
                    activeMobs.RemoveAt(i);
                }
            }
        }
    }
}
