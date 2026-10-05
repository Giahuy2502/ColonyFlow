using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasMainMenu : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;

        public override void Setup()
        {
            if (levelText != null && GameManager.Instance != null)
            {
                levelText.text =GameManager.Instance.DisplayLevelNumber.ToString();
            }
                
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
