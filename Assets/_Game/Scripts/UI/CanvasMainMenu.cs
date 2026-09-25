using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasMainMenu : UICanvas
    {
        [SerializeField] private TextMeshProUGUI goldText;

        public override void Setup()
        {
            if (goldText != null && GameManager.Instance != null)
                goldText.text = $"Level {GameManager.Instance.DisplayLevelNumber}";
        }

        public void PlayButton()
        {
            GameManager.Instance?.PlayGame();
        }

        public void SettingButton()
        {
            GameManager.Instance?.OpenSettings();
        }

        public void ShopButton()
        {
            UIManager.Instance?.Open<CanvasShop>();
        }
    }
}
