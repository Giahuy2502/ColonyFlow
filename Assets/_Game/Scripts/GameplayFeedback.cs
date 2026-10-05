using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class GameplayFeedback : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip victoryAnimationClip;
        [SerializeField] private Transform victoryConfettiRoot;
        [SerializeField] private ParticleSystem victoryConfetti;

        private const string IdleAnim = "Idle";
        private const string VictoryAnim = "Victory";
        private const float FallbackVictoryDuration = 0.6f;
        private string animName;

        public static GameplayFeedback Instance { get; private set; }
        public float VictoryDuration => victoryAnimationClip != null
            ? victoryAnimationClip.length
            : FallbackVictoryDuration;

        private void Awake()
        {
            Instance = this;
            EnsureVictoryConfetti();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayVictoryCelebration()
        {
            ResetFeedback();
            ChangeAnim(VictoryAnim);
            if (victoryConfetti != null)
                victoryConfetti.Play(true);
        }

        public void ResetFeedback()
        {
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            animName = null;
            ChangeAnim(IdleAnim);
            if (victoryConfetti != null)
                victoryConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ChangeAnim(string anim)
        {
            if (animator == null || string.IsNullOrEmpty(anim) || animName == anim)
                return;

            if (!string.IsNullOrEmpty(animName))
                animator.ResetTrigger(animName);

            animName = anim;
            animator.SetTrigger(animName);
        }

        private void EnsureVictoryConfetti()
        {
            if (victoryConfetti == null && victoryConfettiRoot != null)
                victoryConfetti = victoryConfettiRoot.GetComponent<ParticleSystem>();
        }
    }
}
