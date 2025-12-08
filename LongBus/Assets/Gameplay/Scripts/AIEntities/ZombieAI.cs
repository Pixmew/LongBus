using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

namespace PixmewStudios
{
    public class ZombieAI : AIBaseComponent
    {
        [SerializeField] private LayerMask zombiesearchLayer;
        internal override AIBaseComponent CheckForTarget()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRange, zombiesearchLayer);

            foreach(Collider collider in colliders)
            {
                if (collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent<AIBaseComponent>(out AIBaseComponent baseAI))
                {
                    if(baseAI is HumanAI human)
                    {
                        return baseAI;
                    }
                }
            }

            return null;
        }

        internal override void MoveTowards(Vector3 targetPosition)
        {
            base.MoveTowards(targetPosition);
        }
    }
}