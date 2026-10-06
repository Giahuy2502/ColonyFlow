using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasMainMenu : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Animator mainMenuAntAnimator;

        public override void Setup()
        {
            if (levelText != null && GameManager.Instance != null)
            {
                levelText.text = GameManager.Instance.DisplayLevelNumber.ToString();
            }
        }

        public override void Open()
        {
            base.Open();
            RestartAntAnimation();
        }

        public override void CloseDirectly()
        {
            ResetAntAnimation();
            base.CloseDirectly();
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

        private void RestartAntAnimation()
        {
            if (mainMenuAntAnimator == null)
                return;

            mainMenuAntAnimator.enabled = true;
            mainMenuAntAnimator.Rebind();
            mainMenuAntAnimator.Update(0f);
        }

        private void ResetAntAnimation()
        {
            if (mainMenuAntAnimator == null)
                return;

            mainMenuAntAnimator.Rebind();
            mainMenuAntAnimator.Update(0f);
            mainMenuAntAnimator.enabled = false;
        }
    }
}
