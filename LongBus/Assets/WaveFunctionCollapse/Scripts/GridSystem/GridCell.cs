using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class GridCell
{
    [SerializeField] internal bool[] options;
    [SerializeField] internal int optionsCount = 0;
    [SerializeField] internal int collapsedModuleID = -1;
    [SerializeField] internal bool isCollapsed = false;

    internal GridCell(int totalModules)
    {
        Debug.Log("totalModules: " + totalModules.ToString());
        optionsCount = totalModules;
        options = new bool[optionsCount];
        for (int i = 0; i < optionsCount; i++)
        {
            options[i] = true;
        }

        collapsedModuleID = -1;
    }

    internal void CollapseToID(int id)
    {
        collapsedModuleID = id;
        optionsCount = 1;
        isCollapsed = true;
        for (int i = 0; i < options.Length; i++)
        {
            if (i == id)
            {
                options[i] = true;
            }
            else
            {
                options[i] = false;
            }
        }
    }

    internal int GetRandomAvaliableOption()
    {
        List<int> validOptions = new List<int>();
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i])
            {
                validOptions.Add(i);
            }
        }

        if(validOptions.Count == 0) return -1;

        return validOptions[UnityEngine.Random.Range(0, validOptions.Count)];
    }
}   
