using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

namespace PixmewStudios
{
    public class AIBaseComponent : MonoBehaviour
    {
        [SerializeField] internal AIType aiType;
        [SerializeField] protected float _detectionRange = 2;
        [SerializeField] protected float _moveSpeed = 10;
        [SerializeField] protected bool isWandaring;
        protected Tween wanderingTween;


        internal void StartWandering()
        {
            isWandaring = true;
            wanderingTween = transform.DOMove(new Vector3(Random.Range(-_detectionRange, _detectionRange), Random.Range(-_detectionRange, _detectionRange)), _moveSpeed);
            wanderingTween.OnComplete(() =>
            {
                StartWandering();
            });
        }

        internal virtual AIBaseComponent CheckForTarget()
        {
            return null;
            //TODO Detection Base Logic
        }

        internal virtual void MoveTowards(Vector3 targetPosition)
        {
            isWandaring = false;
            wanderingTween?.Kill();
            //TODO moveTowards Position
        }
    }

    public enum AIType
    {
        ZombieAI,
        HumanAI,
    }
}