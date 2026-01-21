using UnityEngine;
using System.Collections.Generic;

namespace PixmewStudios
{

    public class InfiniteTerrainSystem : MonoBehaviour
    {
        [Header("Seed Settings")]
        [Tooltip("Change this number to generate a completely different world.")]
        [SerializeField] private int worldSeed = 12345;

        [Header("Target Settings")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float chunkSize = 20f;
        [SerializeField] private int drawDistance = 3;

        [Header("Chunk Assets")]
        [Tooltip("Drag all your different environment chunk prefabs here (Roads, Buildings, Parks, etc.)")]
        [SerializeField] private GameObject[] chunkPrefabs;
        [SerializeField] private Transform worldContainer;

        // Dictionary tracks spawned chunks
        private Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int currentChunkCoord;

        private void Start()
        {
            if (playerTransform == null)
            {
                var bus = FindObjectOfType<BusController>();
                if (bus != null) playerTransform = bus.transform;
            }

            // Initial spawn
            UpdateChunks();
        }

        private void Update()
        {
            if (playerTransform == null) return;

            Vector2Int playerChunkCoord = GetChunkCoordinate(playerTransform.position);

            if (playerChunkCoord != currentChunkCoord)
            {
                currentChunkCoord = playerChunkCoord;
                UpdateChunks();
            }
        }

        private void UpdateChunks()
        {
            List<Vector2Int> coordsToKeep = new List<Vector2Int>();

            for (int x = -drawDistance; x <= drawDistance; x++)
            {
                for (int y = -drawDistance; y <= drawDistance; y++)
                {
                    Vector2Int viewOffset = new Vector2Int(x, y);
                    Vector2Int targetCoord = currentChunkCoord + viewOffset;
                    coordsToKeep.Add(targetCoord);

                    if (!activeChunks.ContainsKey(targetCoord))
                    {
                        SpawnChunk(targetCoord);
                    }
                }
            }

            // Clean up old chunks
            List<Vector2Int> coordsToRemove = new List<Vector2Int>();
            foreach (var kvp in activeChunks)
            {
                if (!coordsToKeep.Contains(kvp.Key))
                    coordsToRemove.Add(kvp.Key);
            }

            foreach (var coord in coordsToRemove)
            {
                Destroy(activeChunks[coord]);
                activeChunks.Remove(coord);
            }
        }

        private void SpawnChunk(Vector2Int coord)
        {
            if (chunkPrefabs.Length == 0) return;

            // --- DETERMINISTIC LOGIC STARTS HERE ---

            // 1. Create a unique hash based on the Seed and the Coordinates.
            // We use large prime numbers to mix the bits so (1,0) looks very different from (0,1).
            int coordinateHash = (worldSeed) ^ (coord.x * 73856093) ^ (coord.y * 19349663);
            
            // 2. Create a System.Random instance using that hash.
            // This ensures that every time we ask this specific "prng" for a number, it gives the same result.
            System.Random prng = new System.Random(coordinateHash);

            // 3. Pick a random prefab from the  list
            int prefabIndex = prng.Next(0, chunkPrefabs.Length);
            GameObject prefabToSpawn = chunkPrefabs[prefabIndex];

            // 4. Pick a random rotation (0, 90, 180, 270) to add variety
            // This prevents grid-like patterns from looking too obvious
            int rotations = prng.Next(0, 4);
            Quaternion rotation = Quaternion.Euler(0, rotations * 90, 0);

            // --- LOGIC ENDS ---

            Vector3 spawnPosition = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
            
            GameObject newChunk = Instantiate(prefabToSpawn, spawnPosition, rotation);
            if (worldContainer != null) newChunk.transform.SetParent(worldContainer);
            
            newChunk.name = $"Chunk_{coord.x}_{coord.y}";
            activeChunks.Add(coord, newChunk);
        }

        private Vector2Int GetChunkCoordinate(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / chunkSize);
            int y = Mathf.RoundToInt(position.z / chunkSize);
            return new Vector2Int(x, y);
        }
    }
}