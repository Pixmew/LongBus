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
        }

        internal void OnHitZombie(ZombieAI zombieAI)
        {
            Debug.Log("Zombie HIT");
            zombieAI.Death(transform.position);
        }

        internal void OnHitHuman(HumanAI humanAI)
        {
            humanAI.GetSaved(GetComponent<BusController>());
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawSphere(transform.position, 1);
        }
    }
}
