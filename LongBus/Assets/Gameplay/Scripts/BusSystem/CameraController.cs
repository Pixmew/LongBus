using UnityEngine;
using DG.Tweening;
using PixmewStudios; // We need this for all camera tweens

namespace PixmewStudios
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target Following")]
        [SerializeField] private BusController target;
        [SerializeField] private Vector3 offset = new Vector3(0, 20, -10);
        [Tooltip("How long it takes for the camera to catch up to the target's position.")]
        [SerializeField] private float followDuration = 0.5f;

        [Header("Dynamic Zoom")]
        [Tooltip("The speed at which the camera starts zooming out.")]
        [SerializeField] private float minSpeedForZoom = 5f;
        [Tooltip("The speed at which the camera is fully zoomed out.")]
        [SerializeField] private float maxSpeedForZoom = 15f;
        [Space]
        [SerializeField] private float normalOrthoSize = 10f;
        [SerializeField] private float boostOrthoSize = 15f;
        [Tooltip("How long it takes for the camera to zoom in or out.")]
        [SerializeField] private float zoomDuration = 1.0f;

        [SerializeField] private Camera mainCamera;
        private Tween currentShakeTween;

        private void LateUpdate()
        {
            if (target == null) return;

            HandleFollow();
            HandleZoom();
        }

        private void HandleFollow()
        {
            Vector3 desiredPosition = target.transform.position + offset;
            // Use DOTween to smoothly move the camera. It automatically handles smoothing.
            transform.DOMove(desiredPosition, followDuration);
        }

        private void HandleZoom()
        {
            float currentSpeed = target.moveSpeed;
            float speedPercent = Mathf.InverseLerp(minSpeedForZoom, maxSpeedForZoom, currentSpeed);
            float targetOrthoSize = Mathf.Lerp(normalOrthoSize, boostOrthoSize, speedPercent);

            // Use DOTween to smoothly animate the orthographic size.
            mainCamera.DOOrthoSize(targetOrthoSize, zoomDuration);
        }

        /// <summary>
        /// A clean, simple method to trigger a camera shake using DOTween's built-in shaker.
        /// </summary>
        /// <param name="duration">How long the shake should last.</param>
        /// <param name="strength">How intense the shake is. A value of 1 is moderate.</param>
        /// <param name="vibrato">How rapidly the camera shacks. 10 is a good starting point.</param>
        public void TriggerShake(float duration = 0.5f, float strength = 1.0f, int vibrato = 10)
        {
            // Kill any previous shake tween to avoid conflicts, then start a new one.
            mainCamera.DOComplete();
            mainCamera.DOShakePosition(duration, strength, vibrato);
        }


        public void StartContinuousShake(float duration = 0.05f, float strength = 0.5f, int vibrato = 100)
        {
            // If a shake is already running, don't start a new one
            if (currentShakeTween != null && currentShakeTween.IsActive() && currentShakeTween.IsPlaying())
            {
                return;
            }
            Debug.Log("shaking");
            
            // Start the shake with a very long duration and store the reference
            currentShakeTween = mainCamera.transform.DOShakePosition(
                duration, 
                strength, 
                vibrato , 100
            ).SetEase(Ease.InOutSine).SetLoops(-1) // Linear ease keeps the intensity constant
            .SetLink(gameObject);
        }

        /// <summary>
        /// Stops any continuous shake currently running and returns the camera to its non-shaken position.
        /// </summary>
        public void StopShake()
        {
            if (currentShakeTween != null)
            {
                currentShakeTween.Kill(true);
                currentShakeTween = null;
            }
        }
    }
}