namespace ColonyFlow
{
    public sealed class CanvasFail : UICanvas
    {
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
