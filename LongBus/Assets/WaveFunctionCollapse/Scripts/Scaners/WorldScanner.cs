using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixmewStudios
{
    public class WorldScanner : MonoBehaviour
    {
        [SerializeField] internal float gridcellsize = 1;
        public WFCModuleDataSO targetAsset;

#if UNITY_EDITOR
        [ContextMenu("ScanAndBakeData")]
        public void BakeToAsset()
        {
            if (targetAsset == null)
            {
                Debug.LogError("Please assign a WFCData asset to the scanner!");
                return;
            }

            targetAsset.Modules.Clear();

            WorldBlock[] allTiles = GetComponentsInChildren<WorldBlock>();
            Dictionary<Vector3Int, WorldBlock> worldMap = new Dictionary<Vector3Int, WorldBlock>();

            foreach (var t in allTiles)
            {
                Vector3Int gridPos = new Vector3Int(
                    Mathf.RoundToInt(t.transform.position.x / gridcellsize),
                    Mathf.RoundToInt(t.transform.position.y / gridcellsize),
                    Mathf.RoundToInt(t.transform.position.z / gridcellsize)
                );
                if (!worldMap.ContainsKey(gridPos)) worldMap.Add(gridPos, t);
            }

            Dictionary<string, ModuleDefinition> uniqueModules = new Dictionary<string, ModuleDefinition>();
            int currentID = 0;

            foreach (var kvp in worldMap)
            {
                WorldBlock tile = kvp.Value;
                if (!uniqueModules.ContainsKey(tile.moduleID + "_" + (int)tile.transform.eulerAngles.y / 90))
                {
                    ModuleDefinition newMod = new ModuleDefinition();
                    newMod.Name = tile.moduleID + "_" + (int)tile.transform.eulerAngles.y / 90;
                    newMod.Rotation = tile.transform.rotation.eulerAngles;

                    newMod.PrefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(tile.gameObject);

                    for (int i = 0; i < 6; i++) newMod.Adjacency[i] = new AdjacencyList();

                    uniqueModules.Add(tile.moduleID + "_" + (int)tile.transform.eulerAngles.y / 90, newMod);
                    targetAsset.Modules.Add(newMod);
                    currentID++;
                }
            }

            // Map Name -> Index for faster lookup
            Dictionary<string, int> nameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < targetAsset.Modules.Count; i++)
            {
                Debug.Log(targetAsset.Modules[i].Name);
                nameToIndex[targetAsset.Modules[i].Name] = i;
            }

            // 4. LEARN NEIGHBORS
            foreach (var kvp in worldMap)
            {
                Vector3Int pos = kvp.Key;
                string currentName = kvp.Value.moduleID + "_" + (int)kvp.Value.transform.eulerAngles.y / 90;
                int currentIdx = nameToIndex[currentName];

                for (int dir = 0; dir < 6; dir++)
                {
                    Vector3Int neighborPos = pos + Grid.GetDirection(dir);
                    if (worldMap.ContainsKey(neighborPos))
                    {
                        string neighborName = worldMap[neighborPos].moduleID + "_" + (int)worldMap[neighborPos].transform.eulerAngles.y / 90;
                        Debug.Log(neighborName);
                        int neighborIdx = nameToIndex[neighborName];

                        // Add connection if not already there
                        List<int> neighbors = targetAsset.Modules[currentIdx].Adjacency[dir].ValidNeighbors;
                        if (!neighbors.Contains(neighborIdx))
                        {
                            neighbors.Add(neighborIdx);
                        }
                    }
                }
            }

            // 5. SAVE TO DISK
            EditorUtility.SetDirty(targetAsset); // Tell Unity the file changed
            AssetDatabase.SaveAssets(); // Force write to disk
            Debug.Log($"Successfully Baked {targetAsset.Modules.Count} modules to {targetAsset.name}!");
        }
#endif

        internal Vector3Int GetGridPos(Vector3 position)
        {
            Vector3Int blockCoordinate = new Vector3Int();

            blockCoordinate.x = (int)(position.x / gridcellsize);
            blockCoordinate.y = (int)(position.y / gridcellsize);
            blockCoordinate.z = (int)(position.z / gridcellsize);

            return blockCoordinate;
        }
    }

    public class WFCModuleData
    {
        [SerializeField] internal string[] names;
        public GameObject[] Prefabs;
        [SerializeField] internal int[][][] rules;
    }
}
