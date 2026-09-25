using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasSettings : UICanvas
    {
        [SerializeField] private GameObject gameplayButtons;
        public override void Open()
        {
            if (gameplayButtons != null)
                gameplayButtons.SetActive(GameManager.Instance != null &&
                    GameManager.Instance.State == GameState.Paused);
            base.Open();
        }

        public void ContinueButton()
        {
            GameManager.Instance?.CloseSettings();
        }

        public void RetryButton()
        {
            GameManager.Instance?.RestartLevel();
        }

        public void MainMenuButton()
        {
            GameManager.Instance?.GoToMainMenu();
        }
    }
}
