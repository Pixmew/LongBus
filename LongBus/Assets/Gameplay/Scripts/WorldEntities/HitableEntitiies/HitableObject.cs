using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public class HitableObject : MonoBehaviour, HitableObjectsBase
    {
        [SerializeField] private List<Rigidbody> breakableObjects;
        void HitableObjectsBase.OnHit(Vector3 hitPosition)
        {
            foreach(Rigidbody rigidbody in breakableObjects)
            {
                rigidbody.isKinematic = false;
                rigidbody.gameObject.layer = LayerMask.NameToLayer("Default");
                rigidbody.AddExplosionForce(10, hitPosition , 5 , 1 , ForceMode.Impulse);
            }

        }
    }
}
