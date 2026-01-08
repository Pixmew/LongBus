using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixmewStudios
{
    public class SocketScanner : MonoBehaviour
    {
        public WFCModuleDataSO targetAsset;
        [Tooltip("Drag your WorldBlock PREFABS here, not scene objects")]
        public List<WorldBlock> inputPrefabs;

#if UNITY_EDITOR
        [ContextMenu("Bake Sockets")]
        public void BakeSockets()
        {
            if (targetAsset == null)
            {
                Debug.LogError("Target Asset is null!");
                return;
            }

            targetAsset.Modules.Clear();

            // 1. GENERATE ROTATED VARIANTS
            // We convert the input prefabs into a temporary list of all possible rotations
            List<TempModule> processedModules = new List<TempModule>();

            foreach (var block in inputPrefabs)
            {
                // Determine how many rotations to generate
                // Symmetric blocks (like a plain grass block) only need 1 rotation.
                // Asymmetric blocks (like a road turn) need 4 (0, 90, 180, 270).
                int rotations = block.isSymmetric ? 1 : 4;

                for (int i = 0; i < rotations; i++)
                {
                    TempModule mod = new TempModule();

                    // Naming Convention: Name_RotationIndex
                    mod.Name = block.moduleID + "_" + i;

                    // We save the original prefab reference
                    mod.Prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(block.gameObject);

                    // We calculate the rotation for the spawner
                    mod.YRotation = i * 90;

                    // KEY STEP: Calculate the sockets for this specific rotation
                    mod.Sockets = RotateSockets(block, i);

                    processedModules.Add(mod);
                }
            }

            // 2. CALCULATE ADJACENCY
            // Compare every module against every other module
            for (int i = 0; i < processedModules.Count; i++)
            {
                TempModule src = processedModules[i];
                ModuleDefinition def = new ModuleDefinition();

                def.Name = src.Name;
                def.PrefabAsset = src.Prefab;
                def.Rotation = new Vector3(0, src.YRotation, 0);

                // Initialize the 6 directions
                for (int k = 0; k < 6; k++) def.Adjacency[k] = new AdjacencyList();

                // Check against all potential neighbors (j)
                for (int j = 0; j < processedModules.Count; j++)
                {
                    TempModule target = processedModules[j];

                    // Check all 6 directions (0=Up, 1=Down, 2=Left, 3=Right, 4=Fwd, 5=Back)
                    for (int dir = 0; dir < 6; dir++)
                    {
                        // Logic: 
                        // My 'Right' socket must match Neighbor's 'Left' socket.
                        FaceType mySocket = src.Sockets[dir];
                        FaceType neighborSocket = target.Sockets[GetOppositeDir(dir)];

                        if (mySocket == neighborSocket)
                        {
                            // It's a match! Add to valid neighbors.
                            def.Adjacency[dir].ValidNeighbors.Add(j);
                        }
                    }
                }

                targetAsset.Modules.Add(def);
            }

            EditorUtility.SetDirty(targetAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Socket Bake Complete! Generated {targetAsset.Modules.Count} modules.");
        }

        // --- HELPER LOGIC ---

        private class TempModule
        {
            public string Name;
            public GameObject Prefab;
            public int YRotation;
            public FaceType[] Sockets = new FaceType[6];
        }

        // Rotates the socket definitions based on Y-axis rotation
        private FaceType[] RotateSockets(WorldBlock b, int rotationSteps)
        {
            // Initial Order matches Grid.cs GetDirection:
            // 0=Up, 1=Down, 2=Left, 3=Right, 4=Fwd, 5=Back
            FaceType[] current = { b.Top, b.Bottom, b.Left, b.Right, b.Front, b.Back };

            for (int i = 0; i < rotationSteps; i++)
            {
                FaceType[] next = new FaceType[6];
                next[0] = current[0]; // Up stays Up
                next[1] = current[1]; // Down stays Down

                // Rotation Logic (Clockwise):
                // Left(2) becomes Fwd(4)
                // Fwd(4) becomes Right(3)
                // Right(3) becomes Back(5)
                // Back(5) becomes Left(2)

                next[4] = current[2];
                next[3] = current[4];
                next[5] = current[3];
                next[2] = current[5];

                current = next;
            }
            return current;
        }

        private int GetOppositeDir(int dir)
{
    // 0=Up, 1=Down, 2=Left, 3=Right, 4=Fwd, 5=Back
    if (dir == 0) return 1; if (dir == 1) return 0;
    if (dir == 2) return 3; if (dir == 3) return 2;
    
    // FIX IS HERE:
    if (dir == 4) return 5; 
    return 4; // It used to say 'return 5' here too!
}
#endif
    }
}