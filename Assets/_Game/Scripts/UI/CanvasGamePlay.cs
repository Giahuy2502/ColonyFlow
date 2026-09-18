using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasGamePlay : UICanvas
    {
        [SerializeField] private TextMeshProUGUI aliveText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private LoseNotification loseNotification;
        private bool doubleSpeed;

        public override void Setup()
        {
            if (aliveText != null && LevelManager.Instance != null)
                aliveText.text = $"Level {LevelManager.Instance.DisplayLevelNumber}";
            if (loseNotification != null)
            {
                loseNotification.Setup();
                loseNotification.gameObject.SetActive(false);
            }
            doubleSpeed = false;
            Time.timeScale = 1f;
            RefreshSpeedText();
        }

        public void SettingButton()
        {
            UIManager.Instance?.Open<CanvasSettings>();
        }

        public void SpeedButton()
        {
            doubleSpeed = !doubleSpeed;
            Time.timeScale = doubleSpeed ? 2f : 1f;
            RefreshSpeedText();
        }

        private void RefreshSpeedText()
        {
            if (speedText != null)
                speedText.text = doubleSpeed ? "2x" : "1x";
        }
    }
}
