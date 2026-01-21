using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace PixmewStudios
{
    public class BusHitBehaviour : MonoBehaviour
    {
        internal event Action<ZombieAI> onHitZombie;
        internal event Action<HumanAI> onHitHuman;
        internal event Action<HitableObjectsBase> onHitHitableObject;

        private bool initilized;

        private void Start()
        {
            StartCoroutine(Init());
        }

        IEnumerator Init()
        {
            yield return new WaitForSeconds(1);
            onHitHuman += OnHitHuman;
            onHitZombie += OnHitZombie;
            onHitHitableObject += OnHitHitableObject;
            initilized = true;
        }

        void LateUpdate()
        {
            if (!initilized) return;
            CheckForHit();
        }

        void CheckForHit()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 1, ValueHolder.Instance.humanLayer);

            foreach (var col in colliders)
            {
                if (col.attachedRigidbody != null)
                {
                    onHitHuman?.Invoke(col.attachedRigidbody.GetComponent<HumanAI>());
                }
            }

            colliders = Physics.OverlapSphere(transform.position, 1, ValueHolder.Instance.zombieLayer);

            foreach (var col in colliders)
            {
                if (col.attachedRigidbody)
                {
                    onHitZombie?.Invoke(col.attachedRigidbody.GetComponent<ZombieAI>());
                }
            }

            colliders = Physics.OverlapSphere(transform.position, 1, ValueHolder.Instance.hitableLayer);

            foreach (var col in colliders)
            {
                if (col.attachedRigidbody)
                {
                    onHitHitableObject?.Invoke(col.attachedRigidbody.transform.parent.GetComponent<HitableObjectsBase>());
                }
            }
        }

        internal void OnHitZombie(ZombieAI zombieAI)
        {
            zombieAI.Death(transform.position);
        }

        internal void OnHitHuman(HumanAI humanAI)
        {
            humanAI.GetSaved(GetComponent<BusController>());
        }

        internal void OnHitHitableObject(HitableObjectsBase hitableObject)
        {
            if (hitableObject == null)
            {
                return;
            }
            Debug.Log("HIT");
            hitableObject.OnHit(transform.position);
        }



        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawSphere(transform.position, 1f);
        }
    }
}
