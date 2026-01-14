using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

namespace PixmewStudios
{
    public class Grid : MonoBehaviour
    {
        [Header("Data Source")]
        public WFCModuleDataSO wfcData;
        [SerializeField] internal Vector3Int gridSize;
        [SerializeField] internal GridCell[,,] gridcells;
        [SerializeField] internal int cellSize = 1;
        [SerializeField] internal bool isGridInititlized = false;
        [SerializeField] internal Vector3 positionOffset;

        [SerializeField] internal WFCModuleData wFCModuleData;
        private Stack<Vector3Int> pointsToUpdate = new Stack<Vector3Int>();
        [SerializeField] private bool debugGizmos = false;
        [SerializeField] private bool spawnPrefabs = false;

        void Start()
        {

        }

        [ContextMenu("Generate")]
        internal void Generate()
        {
            if (wfcData != null)
            {
                // Turn the Save File into the Engine
                ConvertAssetToRuntimeData();

                // Pass the FAST data to the grid initialization
                // (Make sure InitializeGrid uses 'runtimeData' now)
                InitializeGrid();

                GenerateAll();
            }
        }


        // void Update()
        // {
        //     // SPACE: Step-by-step collapse (Debug)
        //     if (Input.GetKeyDown(KeyCode.Space))
        //     {
        //         Vector3Int target = GetLowestEntropyCell();
        //         if (target.x != -1) CollapseCell(target);

        //         SpawnLevel();
        //     }

        //     // ENTER: Run the whole simulation instantly & Spawn
        //     if (Input.GetKeyDown(KeyCode.Return))
        //     {
        //         GenerateAll();
        //     }
        // }

        private void ConvertAssetToRuntimeData()
        {
            // 1. Create the container
            wFCModuleData = new WFCModuleData();

            int count = wfcData.Modules.Count;
            wFCModuleData.names = new string[count];
            wFCModuleData.Prefabs = new GameObject[count];
            wFCModuleData.rules = new int[count][][];

            // 2. Loop through the ScriptableObject lists
            for (int i = 0; i < count; i++)
            {
                ModuleDefinition def = wfcData.Modules[i];

                // Copy simple data
                wFCModuleData.names[i] = def.Name;
                wFCModuleData.Prefabs[i] = def.PrefabAsset;
                // Note: You might want to handle rotation here too by instantiating a dummy or storing it separately

                // Initialize the 6 directions
                wFCModuleData.rules[i] = new int[6][];

                for (int dir = 0; dir < 6; dir++)
                {
                    // CONVERSION: List<int> -> int[]
                    // This turns the slow list into the fast array for the solver
                    wFCModuleData.rules[i][dir] = def.Adjacency[dir].ValidNeighbors.ToArray();
                }
            }

            Debug.Log("Optimized Data Structure Built!");
        }

        // A simple coroutine to run the loop until finished
        void GenerateAll()
        {
            while (true)
            {
                Vector3Int target = GetLowestEntropyCell();

                // If no valid target found, we are done or failed
                if (target.x == -1)
                {
                    Debug.Log("invalid");
                    break;
                }

                Debug.Log("collapsed");
                CollapseCell(target);
            }

            Debug.Log("Generation Finished. Spawning meshes...");
            SpawnLevel();
        }

        internal void SetRules(WFCModuleData data)
        {
            this.wFCModuleData = data;
            InitializeGrid();
        }

        internal void InitializeGrid()
        {
            //if (isGridInititlized) return;

            if (wFCModuleData == null) return;

            isGridInititlized = true;

            gridcells = new GridCell[gridSize.x, gridSize.y, gridSize.z];

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        gridcells[x, y, z] = new GridCell(wFCModuleData.names.Length);
                    }
                }
            }
        }

        internal Vector3Int GetLowestEntropyCell()
        {
            int lowestEntropy = int.MaxValue;
            Vector3Int lowestEntropyCellPosition = new Vector3Int(-1, -1, -1);

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        // Debug.Log(gridcells[x, y, z].isCollapsed);
                        // Debug.Log(gridcells[x, y, z].optionsCount);
                        if (gridcells[x, y, z].isCollapsed) continue;
                        if (gridcells[x, y, z].optionsCount <= 0) continue;
                        if (gridcells[x, y, z].optionsCount < lowestEntropy)
                        {
                            lowestEntropy = gridcells[x, y, z].optionsCount;
                            lowestEntropyCellPosition = new Vector3Int(x, y, z);
                            Debug.Log(lowestEntropyCellPosition + " " + lowestEntropy);
                        }
                    }
                }
            }
            return lowestEntropyCellPosition;
        }


        internal void CollapseCell(Vector3Int cellPosition)
        {
            if (!IsValidCell(cellPosition))
                return;

            GridCell cell = gridcells[cellPosition.x, cellPosition.y, cellPosition.z];

            int randomOption = cell.GetRandomAvaliableOption();

            if (randomOption == -1)
            {
                Debug.Log("No options left for cell at " + cellPosition);
                return;
            }

            cell.CollapseToID(randomOption);

            pointsToUpdate.Push(cellPosition);
            Propogate();
        }

        internal void Propogate()
        {
            while (pointsToUpdate.Count > 0)
            {
                Vector3Int cellPosition = pointsToUpdate.Pop();
                GridCell currentCell = gridcells[cellPosition.x, cellPosition.y, cellPosition.z];

                for (int dir = 0; dir < 6; dir++)
                {
                    Vector3Int neighbourCellPosition = cellPosition + GetDirection(dir);
                    if (IsValidCell(neighbourCellPosition))
                    {
                        GridCell neighbourCell = gridcells[neighbourCellPosition.x, neighbourCellPosition.y, neighbourCellPosition.z];

                        if (neighbourCell.isCollapsed) continue;

                        bool ischanged = Constraint(currentCell, neighbourCell, dir);

                        if (ischanged)
                        {
                            pointsToUpdate.Push(neighbourCellPosition);

                            if (neighbourCell.optionsCount <= 0)
                            {
                                Debug.Log("No options left for cell at " + neighbourCellPosition);
                            }
                        }
                    }
                }
            }
        }

        internal bool Constraint(GridCell currentCell, GridCell neighbourCell, int dir)
        {
            bool isChanged = false;

            bool[] PossibleNeighbours = new bool[wFCModuleData.names.Length];

            for (int i = 0; i < currentCell.options.Length; i++)
            {
                if (currentCell.options[i])
                {
                    int[] validNeighbours = wFCModuleData.rules[i][dir];

                    foreach (int validNeighbour in validNeighbours)
                    {
                        PossibleNeighbours[validNeighbour] = true;
                    }
                }
            }

            for (int i = 0; i < neighbourCell.options.Length; i++)
            {
                if (neighbourCell.options[i])
                {
                    if (!PossibleNeighbours[i])
                    {
                        neighbourCell.options[i] = false;
                        neighbourCell.optionsCount--;
                        isChanged = true;
                    }
                }
            }
            return isChanged;
        }

        internal bool IsValidCell(Vector3Int cellPosition)
        {
            bool isValid = true;

            if (cellPosition.x < 0 || cellPosition.x >= gridSize.x)
            {
                isValid = false;
            }
            if (cellPosition.y < 0 || cellPosition.y >= gridSize.y)
            {
                isValid = false;
            }
            if (cellPosition.z < 0 || cellPosition.z >= gridSize.z)
            {
                isValid = false;
            }

            return isValid;

        }

        public static Vector3Int GetDirection(int dir)
        {
            // Make sure this matches the order in your RuleScanner!
            // 0=Up, 1=Down, 2=Left, 3=Right, 4=Forward, 5=Back
            Vector3Int[] offsets = {
                 Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back
                };
            return offsets[dir];
        }


        public void SpawnLevel()
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        GridCell cell = gridcells[x, y, z];

                        if (cell.isCollapsed && cell.collapsedModuleID != -1)
                        {
                            Vector3 worldPos = transform.position +
                                               new Vector3(x * cellSize, y * cellSize, z * cellSize) + positionOffset;

                            // NEW: Read from ScriptableObject
                            ModuleDefinition module = wfcData.Modules[cell.collapsedModuleID];

                            if (!spawnPrefabs)
                            {
                                // Use the saved Prefab + Saved Rotation
                                Instantiate(module.PrefabAsset, worldPos, Quaternion.Euler(module.Rotation), transform);
                            }
                            else
                            {
                               GameObject moduleObject = PrefabUtility.InstantiatePrefab(module.PrefabAsset, transform) as GameObject;
                                moduleObject.transform.position = worldPos;
                                moduleObject.transform.eulerAngles = module.Rotation;
                            }
                        }
                    }
                }
            }


            void OnDrawGizmos()
            {
                if (!isGridInititlized || gridcells == null || !debugGizmos) return;

                // Loop through the grid dimensions
                for (int x = 0; x < gridSize.x; x++)
                {
                    for (int y = 0; y < gridSize.y; y++)
                    {
                        for (int z = 0; z < gridSize.z; z++)
                        {
                            // Math: Calculate World Position
                            // worldPos = transform.position + (indices * gap)
                            Vector3 worldPos = transform.position +
                                               new Vector3(x * cellSize, y * cellSize, z * cellSize) + positionOffset;

                            // Inside OnDrawGizmos loop:
                            GridCell cell = gridcells[x, y, z];

                            if (cell.isCollapsed)
                            {
                                Gizmos.color = Color.blue; // Blue = Solved
                                Gizmos.DrawCube(worldPos, Vector3.one * (cellSize * 0.9f));
                            }
                            else
                            {
                                Gizmos.color = Color.green; // Green = Still thinking
                                Gizmos.DrawWireCube(worldPos, Vector3.one * (cellSize * 0.9f));

                            }
                        }
                    }
                }
            }
        }
    }
}
