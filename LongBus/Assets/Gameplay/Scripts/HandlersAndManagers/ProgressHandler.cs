using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace PixmewStudios
{
    public class ProgressHandler : MonoBehaviour
    {
        [Header("Refrences")]
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _progressText;

        [Header("Settings")]
        [SerializeField] private int maxProgressLevel = -1; // below and equal zero max progress levels are infinite 
        [SerializeField] private Vector2 progressMinMax = new Vector2(0, 1);
        [SerializeField] private float progressMinMaxMultiplier = 1; // This creates new minmax values for progress if no overrides are avaliable
        [SerializeField] private List<Vector2> progressMinMaxOverrides = new List<Vector2>();


        [Header("Events")]
        internal Action OnProgressChanged;
        internal Action OnProgressLevelCompleted;
        internal Action OnReachMaxProgressLevel;

        private int _currentProgresLevel = 0;
        private float _targetProgress = 0;
        private float _extraProgress = 0;

        void Start()
        {
            Init();
        }

        internal void Init()
        {
            Init(_currentProgresLevel, GetMinMaxProgress(_currentProgresLevel), 0);
        }

        internal void Init(int progressLevel, Vector2 progressMinMax, float extraProgress)
        {
            Debug.Log($"progressLevel: {progressLevel}, progressMinMax: {progressMinMax}, extraProgress: {extraProgress}");
            this._currentProgresLevel = progressLevel;
            this.progressMinMax = progressMinMax;

            _progressSlider.minValue = progressMinMax.x;
            _progressSlider.maxValue = progressMinMax.y;

            _progressSlider.value = progressMinMax.x;

            _targetProgress = _progressSlider.minValue + extraProgress;
            _extraProgress = 0;

            UpdateProgressVisuals();
        }

        void Update()
        {
            if (_progressSlider.value != _targetProgress || (_progressSlider.value == _targetProgress && _extraProgress > 0))
            {
                _progressSlider.value = Mathf.Lerp(_progressSlider.value, _targetProgress, 0.1f);
                UpdateProgressVisuals();
                if (Mathf.Abs(_progressSlider.value - _progressSlider.maxValue) < 0.01f)
                {
                    _progressSlider.value = _progressSlider.maxValue;
                    if (maxProgressLevel <= 0 || _currentProgresLevel <= maxProgressLevel)
                    {
                        IncreaseProgressLevel();
                        OnProgressLevelCompleted?.Invoke();
                    }
                }
                OnProgressChanged?.Invoke();
            }
        }

        internal void AddProgress(float progress)
        {
            Debug.Log(_targetProgress + " " + progress);
            _targetProgress = _targetProgress + progress;

            if (_targetProgress >= _progressSlider.maxValue - 0.01f)
            {
                _extraProgress += _targetProgress - _progressSlider.maxValue;
                _targetProgress = _progressSlider.maxValue;
            }
        }

        internal void UpdateProgressVisuals()
        {
            _progressText.text = "LVL: " + (_currentProgresLevel + 1) + "  " + ((int)_targetProgress).ToString() + " / " + _progressSlider.maxValue.ToString();
        }

        internal void IncreaseProgressLevel()
        {
            _currentProgresLevel++;
            if (maxProgressLevel > 0 && _currentProgresLevel >= maxProgressLevel)
            {
                OnReachMaxProgressLevel?.Invoke();
                return;
            }



            Init(_currentProgresLevel, GetMinMaxProgress(_currentProgresLevel), _extraProgress);
        }

        internal Vector2 GetMinMaxProgress(int progressLevel)
        {
            Vector2 tempMinMaxProgress;
            if (progressMinMaxOverrides.Count > progressLevel)
            {
                tempMinMaxProgress = progressMinMaxOverrides[progressLevel];
            }
            else
            {
                tempMinMaxProgress = progressMinMax * progressMinMaxMultiplier;
            }
            return tempMinMaxProgress;
        }
    }
}
