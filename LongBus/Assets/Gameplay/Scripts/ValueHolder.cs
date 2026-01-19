using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class ValueHolder : MonoBehaviour
    {
        private static ValueHolder instance;
        internal static ValueHolder Instance => instance;
        private ValueHolder() { }


        [SerializeField] internal LayerMask busLayer;
        [SerializeField] internal LayerMask humanLayer;
        [SerializeField] internal LayerMask zombieLayer;



        void Awake()
        {
            instance = this;
        }
    }
}
