using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class LoseNotification : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI enemyText;

        public void Setup()
        {
            if (enemyText != null && LevelManager.Instance != null)
                enemyText.text = $"Level {LevelManager.Instance.DisplayLevelNumber}";
        }

        public void UpdateEnemyText(string value)
        {
            if (enemyText != null)
                enemyText.text = value;
        }
    }
}
