using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasVictory : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;

        public override void Setup()
        {
            if (levelText != null && GameManager.Instance != null)
                levelText.text = $"Level {GameManager.Instance.DisplayLevelNumber} Complete";
        }

        public void NextLevelButton()
        {
            GameManager.Instance?.NextLevel();
        }

        public void ReplayButton()
        {
            GameManager.Instance?.ReplayLevel();
        }

        public void MainMenuButton()
        {
            GameManager.Instance?.GoToMainMenu();
        }
    }
}
