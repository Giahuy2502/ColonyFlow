using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasSettings : UICanvas
    {
        [SerializeField] private GameObject gameplayButtons;
        [SerializeField] private TextMeshProUGUI musicButtonText;
        [SerializeField] private TextMeshProUGUI sfxButtonText;

        public override void Setup()
        {
            EnsureSoundControls();
            RefreshSoundLabels();
        }

        public override void Open()
        {
            if (gameplayButtons != null)
                gameplayButtons.SetActive(GameManager.Instance != null &&
                    GameManager.Instance.State == GameState.Paused);
            RefreshSoundLabels();
            base.Open();
        }

        public void MusicButton()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
                return;
            soundManager.SetMusicEnabled(!soundManager.IsMusicEnabled);
            RefreshSoundLabels();
        }

        public void SfxButton()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
                return;
            soundManager.SetSfxEnabled(!soundManager.IsSfxEnabled);
            RefreshSoundLabels();
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

        private void RefreshSoundLabels()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (musicButtonText != null)
                musicButtonText.text = soundManager == null || soundManager.IsMusicEnabled
                    ? "MUSIC: ON"
                    : "MUSIC: OFF";
            if (sfxButtonText != null)
                sfxButtonText.text = soundManager == null || soundManager.IsSfxEnabled
                    ? "SFX: ON"
                    : "SFX: OFF";
        }

        private void EnsureSoundControls()
        {
            if (musicButtonText != null && sfxButtonText != null)
                return;
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

            musicButtonText = CreateSoundButton(
                "Music", controls, new Vector2(-160f, 0f), MusicButton);
            sfxButtonText = CreateSoundButton(
                "SFX", controls, new Vector2(160f, 0f), SfxButton);
        }

        private static TextMeshProUGUI CreateSoundButton(string buttonName,
            Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(buttonName, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UIButtonSound));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(290f, 94f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(1f, 0.93f, 0.78f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);

            var labelObject = new GameObject("Label", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 30f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.12f, 0.22f, 0.38f, 0.97f);
            label.raycastTarget = false;
            return label;
        }
    }
}
