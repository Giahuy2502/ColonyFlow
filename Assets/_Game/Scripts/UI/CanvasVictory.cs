using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasVictory : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;

        public override void Setup()
        {
            if (levelText != null && LevelManager.Instance != null)
                levelText.text = $"Level {LevelManager.Instance.DisplayLevelNumber} Complete";
        }

        public void NextLevelButton()
        {
            LevelManager.Instance?.NextLevel();
        }

        public void ReplayButton()
        {
            LevelManager.Instance?.RestartLevel();
        }

        public void MainMenuButton()
        {
            LevelManager.Instance?.NextLevel();
        }
    }
}
