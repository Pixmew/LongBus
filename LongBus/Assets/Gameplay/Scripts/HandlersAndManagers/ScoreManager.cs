using UnityEngine;

namespace PixmewStudios
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Score Values")]
        public int currentScore = 0;
        public int currentCombo = 1;
        public float comboTimer = 0f;
        [SerializeField] private float maxComboTime = 3f;

        [Header("Boost Meter")]
        public float currentBoostAmount = 0f;
        public float maxBoostAmount = 100f;
        // How much boost is consumed per second while boosting
        [SerializeField] private float boostDrainRate = 25f; 

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Combo drops if we don't smash anything for a while
            if (currentCombo > 1)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0)
                {
                    currentCombo = 1; // Reset combo
                }
            }
        }

        public void AddScore(int basePoints, bool isDrifting = false)
        {
            int pointsToAdd = basePoints;
            
            if (isDrifting)
            {
                currentCombo++; // Increase multiplier if drifting
            }
            
            pointsToAdd *= currentCombo;
            currentScore += pointsToAdd;
            
            comboTimer = maxComboTime; // Reset combo countdown
            
            // Gain boost energy based on the raw score added
            AddBoost(pointsToAdd * 0.5f);
            
            // Debug.Log($"Score: {currentScore} | Combo: x{currentCombo}");
        }

        public void AddBoost(float amount)
        {
            currentBoostAmount = Mathf.Clamp(currentBoostAmount + amount, 0, maxBoostAmount);
        }

        public bool ConsumeBoost(float deltaTime)
        {
            if (currentBoostAmount > 0)
            {
                currentBoostAmount -= boostDrainRate * deltaTime;
                return true;
            }
            return false;
        }

        public bool CanBoost()
        {
            // Example: Only allow starting a boost if we have at least 20% meter
            return currentBoostAmount > (maxBoostAmount * 0.2f);
        }
    }
}
