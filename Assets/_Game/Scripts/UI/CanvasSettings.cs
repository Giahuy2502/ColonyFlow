using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasSettings : UICanvas
    {
        [SerializeField] private GameObject gameplayButtons;
        

        public override void Setup()
        {
            EnsureSoundControls();
        }

        public override void Open()
        {
            if (gameplayButtons != null)
                gameplayButtons.SetActive(GameManager.Instance != null &&
                    GameManager.Instance.State == GameState.Paused);
            base.Open();
        }

        public void MusicButton()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
                return;
            soundManager.SetMusicEnabled(!soundManager.IsMusicEnabled);
            
        }

        public void SfxButton()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
                return;
            soundManager.SetSfxEnabled(!soundManager.IsSfxEnabled);
        }

        public void ContinueButton()
        {
            GameManager.Instance?.CloseSettings();
        }

        public void ExitButton()
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

        
        private void EnsureSoundControls()
        {
           
            if (gameplayButtons == null || gameplayButtons.transform.parent == null)
                return;

            Transform card = gameplayButtons.transform.parent;
            var controlsObject = new GameObject("Audio Controls", typeof(RectTransform));
            RectTransform controls = controlsObject.GetComponent<RectTransform>();
            controls.SetParent(card, false);
            controls.anchorMin = new Vector2(0.5f, 0.5f);
            controls.anchorMax = new Vector2(0.5f, 0.5f);
            controls.pivot = new Vector2(0.5f, 0.5f);
            controls.anchoredPosition = new Vector2(0f, 80f);
            controls.sizeDelta = new Vector2(650f, 120f);

            
        }
        
    }
}
