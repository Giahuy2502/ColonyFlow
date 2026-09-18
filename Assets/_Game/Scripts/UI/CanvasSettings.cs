using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasSettings : UICanvas
    {
        [SerializeField] private GameObject gameplayButtons;

        public override void Open()
        {
            if (gameplayButtons != null)
                gameplayButtons.SetActive(true);
            Time.timeScale = 0f;
            base.Open();
        }

        public void ContinueButton()
        {
            CloseDirectly();
        }

        public void RetryButton()
        {
            Time.timeScale = 1f;
            LevelManager.Instance?.RestartLevel();
        }

        public void MainMenuButton()
        {
            Time.timeScale = 0f;
            UIManager.Instance?.CloseAll();
            UIManager.Instance?.Open<CanvasMainMenu>();
        }

        public override void CloseDirectly()
        {
            Time.timeScale = 1f;
            base.CloseDirectly();
        }
    }
}
