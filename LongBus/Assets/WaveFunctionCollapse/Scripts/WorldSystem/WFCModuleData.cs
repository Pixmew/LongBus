using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PixmewStudios
{
    [CreateAssetMenu(fileName = "WFCModuleData", menuName = "WFCModuleData", order = 0)]
    public class WFCModuleDataSO : ScriptableObject
    {
        [SerializeField] internal List<ModuleDefinition> Modules = new List<ModuleDefinition>();
    }

    [System.Serializable]
    public class ModuleDefinition
    {
        public string Name;
        public GameObject PrefabAsset;
        public Vector3 Rotation; 
        public AdjacencyList[] Adjacency = new AdjacencyList[6];
    }

    [System.Serializable]
    public class AdjacencyList
    {
        public List<int> ValidNeighbors = new List<int>();
    }
}