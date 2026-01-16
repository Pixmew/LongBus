using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class RefrenceHolder : MonoBehaviour
    {
        private static RefrenceHolder instance;
        internal static RefrenceHolder Instance => instance;
        private RefrenceHolder() { }



        [SerializeField] internal ProgressHandler gameProgressHandler;


        void Awake()
        {
            instance = this;
        }
    }
}