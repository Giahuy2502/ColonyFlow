using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class GameplayFeedback : MonoBehaviour
    {
        [SerializeField] private Transform boardVisualRoot;
        [SerializeField] private Transform victoryConfettiRoot;
        [SerializeField] private ParticleSystem victoryConfetti;
        [Header("Victory")]
        [SerializeField, Min(0.01f)] private float boardPulseDuration = 0.6f;
        [SerializeField, Min(1f)] private float boardPulseScale = 1.045f;
        [SerializeField, Min(0)] private int victoryConfettiCount = 80;
        [SerializeField, Min(0.01f)] private float victoryParticleLifetime = 0.6f;
        [SerializeField, Min(0f)] private float victoryParticleSpeed = 1.8f;
        [SerializeField, Min(0.001f)] private float victoryParticleSize = 0.06f;
        [SerializeField, Min(0f)] private float victoryParticleGravity = 0.55f;
        [SerializeField] private Vector3 victoryEmitterSize = new Vector3(4f, 0.1f, 0.25f);

        private Vector3 boardRestingScale;
        private float pulseTime;
        private bool isPulsing;

        public static GameplayFeedback Instance { get; private set; }
        public float VictoryDuration => boardPulseDuration;

        private void Awake()
        {
            Instance = this;
            EnsureVictoryConfetti();
            if (boardVisualRoot != null)
                boardRestingScale = boardVisualRoot.localScale;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!isPulsing || boardVisualRoot == null)
                return;

            pulseTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(pulseTime / boardPulseDuration);
            float wave = Mathf.Sin(progress * Mathf.PI);
            boardVisualRoot.localScale = boardRestingScale * Mathf.Lerp(1f, boardPulseScale, wave);
            if (progress < 1f)
                return;

            boardVisualRoot.localScale = boardRestingScale;
            isPulsing = false;
        }

        public void PlayVictoryCelebration()
        {
            ResetFeedback();
            if (boardVisualRoot != null)
            {
                boardRestingScale = boardVisualRoot.localScale;
                isPulsing = true;
            }
            if (victoryConfetti != null)
            {
                victoryConfetti.Play(true);
                if (victoryConfettiCount > 0)
                    victoryConfetti.Emit(victoryConfettiCount);
            }
        }

        public void ResetFeedback()
        {
            isPulsing = false;
            pulseTime = 0f;
            if (boardVisualRoot != null && boardRestingScale != Vector3.zero)
                boardVisualRoot.localScale = boardRestingScale;
            if (victoryConfetti != null)
                victoryConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void EnsureVictoryConfetti()
        {
            if (victoryConfetti == null && victoryConfettiRoot != null)
            {
                victoryConfetti = victoryConfettiRoot.GetComponent<ParticleSystem>();
                if (victoryConfetti == null)
                    victoryConfetti = victoryConfettiRoot.gameObject.AddComponent<ParticleSystem>();
            }

            if (victoryConfetti != null)
            {
                var main = victoryConfetti.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Mathf.Max(victoryConfettiCount, 1);
                main.startLifetime = victoryParticleLifetime;
                main.startSpeed = victoryParticleSpeed;
                main.startSize = victoryParticleSize;
                main.gravityModifier = victoryParticleGravity;
                var emission = victoryConfetti.emission;
                emission.enabled = false;
                var shape = victoryConfetti.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = victoryEmitterSize;
            }
        }
    }
}
