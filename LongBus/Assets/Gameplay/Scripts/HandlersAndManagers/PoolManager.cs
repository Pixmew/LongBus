using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance;

        // Key = Prefab Name, Value = Queue of inactive objects
        private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            string key = prefab.name;

            // 1. Create a queue for this prefab if it doesn't exist yet
            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary.Add(key, new Queue<GameObject>());
            }

            GameObject objectToSpawn;

            // 2. Check if we have an inactive object waiting
            if (poolDictionary[key].Count > 0)
            {
                objectToSpawn = poolDictionary[key].Dequeue();
            }
            else
            {
                // 3. If queue is empty, create a new one (expand the pool)
                objectToSpawn = Instantiate(prefab);
                objectToSpawn.name = prefab.name; // Keep name consistent
            }

            // 4. Setup the object
            objectToSpawn.transform.SetParent(parent);
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            objectToSpawn.SetActive(true);

            return objectToSpawn;
        }

        public void ReturnToPool(GameObject obj)
        {
            // 1. Disable it
            obj.SetActive(false);
            
            // 2. Unparent it (so it doesn't get destroyed if the parent chunk dies)
            obj.transform.SetParent(transform); 

            // 3. Add back to queue
            string key = obj.name; // This name must match the prefab name set in SpawnFromPool
            
            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary.Add(key, new Queue<GameObject>());
            }

            poolDictionary[key].Enqueue(obj);
        }
    }
}