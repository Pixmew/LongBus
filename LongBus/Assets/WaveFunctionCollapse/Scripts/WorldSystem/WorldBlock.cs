using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class WorldBlock : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] internal string moduleID;
        [Range(1, 100)] public int Weight = 1;

        [Header("Socket Definition")]
        public FaceType Top;
        public FaceType Bottom;
        public FaceType Left;
        public FaceType Right;
        public FaceType Front;
        public FaceType Back;

        [Tooltip("If true, only 1 rotation is generated. If false, 4 rotations are generated.")]
        public bool isSymmetric = false;
    }

    public enum FaceType
    {
        Ground,
        Air,
        Road,
        Concret,

    }
}