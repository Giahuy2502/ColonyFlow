using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class CanvasVictory : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Animator victoryEffectAnimator;
        [SerializeField] private VictoryStarFall victoryStarEffect;

        public override void Setup()
        {
            if (levelText != null && GameManager.Instance != null)
                levelText.text = $"Level {GameManager.Instance.DisplayLevelNumber} Complete";
        }

        public override void Open()
        {
            base.Open();
            RestartVictoryEffect();
        }

        public override void CloseDirectly()
        {
            ResetVictoryEffect();
            base.CloseDirectly();
        }

        public void NextLevelButton()
        {
            GameManager.Instance?.NextLevel();
        }

        public void ReplayButton()
        {
            GameManager.Instance?.ReplayLevel();
        }

        public void MainMenuButton()
        {
            GameManager.Instance?.GoToMainMenu();
        }

        private void RestartVictoryEffect()
        {
            RestartAnimator(victoryEffectAnimator);
            victoryStarEffect?.Play();
        }

        private void ResetVictoryEffect()
        {
            ResetAnimator(victoryEffectAnimator);
            victoryStarEffect?.StopAndClear();
        }

        private static void RestartAnimator(Animator target)
        {
            if (target == null)
                return;

            target.enabled = true;
            target.Rebind();
            target.Update(0f);
        }

        private static void ResetAnimator(Animator target)
        {
            if (target == null)
                return;

            target.Rebind();
            target.Update(0f);
            target.enabled = false;
        }
    }
}
