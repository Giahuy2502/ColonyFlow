namespace ColonyFlow
{
    public sealed class CanvasFail : UICanvas
    {
        public void RetryButton()
        {
            LevelManager.Instance?.RestartLevel();
        }

        public void MainMenuButton()
        {
            UIManager.Instance?.CloseAll();
            UIManager.Instance?.Open<CanvasMainMenu>();
        }
    }
}
