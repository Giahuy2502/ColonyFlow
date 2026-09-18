using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasGamePlay : UICanvas
    {
        [SerializeField] private TextMeshProUGUI aliveText;
        [SerializeField] private LoseNotification loseNotification;

        public override void Setup()
        {
            if (aliveText != null && LevelManager.Instance != null)
                aliveText.text = $"Level {LevelManager.Instance.DisplayLevelNumber}";
            if (loseNotification != null)
            {
                loseNotification.Setup();
                loseNotification.gameObject.SetActive(false);
            }
        }

        public void SettingButton()
        {
            UIManager.Instance?.Open<CanvasSettings>();
        }
    }
}
