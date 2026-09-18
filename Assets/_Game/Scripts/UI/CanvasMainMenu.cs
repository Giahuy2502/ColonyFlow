using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasMainMenu : UICanvas
    {
        [SerializeField] private TextMeshProUGUI goldText;

        public override void Setup()
        {
            if (goldText != null && LevelManager.Instance != null)
                goldText.text = $"Level {LevelManager.Instance.DisplayLevelNumber}";
        }

        public override void Open()
        {
            Time.timeScale = 0f;
            base.Open();
        }

        public void PlayButton()
        {
            Time.timeScale = 1f;
            Close(0f);
        }

        public void SettingButton()
        {
            UIManager.Instance?.Open<CanvasSettings>();
        }

        public void ShopButton()
        {
            UIManager.Instance?.Open<CanvasShop>();
        }
    }
}
