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
            if (aliveText != null && GameManager.Instance != null)
                aliveText.text = $"Level {GameManager.Instance.DisplayLevelNumber}";
            if (loseNotification != null)
            {
                loseNotification.Setup();
                loseNotification.gameObject.SetActive(false);
            }
            doubleSpeed = GameManager.Instance != null && GameManager.Instance.IsDoubleSpeed;
            RefreshSpeedText();
        }

        public void SettingButton()
        {
            GameManager.Instance?.OpenSettings();
        }

        public void SpeedButton()
        {
            GameManager.Instance?.ToggleGameSpeed();
            doubleSpeed = GameManager.Instance != null && GameManager.Instance.IsDoubleSpeed;
            RefreshSpeedText();
        }

        private void RefreshSpeedText()
        {
            if (speedText != null)
                speedText.text = doubleSpeed ? "2x" : "1x";
        }
    }
}
