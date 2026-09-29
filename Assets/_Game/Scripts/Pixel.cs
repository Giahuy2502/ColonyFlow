using System;
using UnityEngine;

namespace ColonyFlow
{
    public enum PixelState { Present, Collected }

    [DisallowMultipleComponent]
    public sealed class Pixel : GameUnit
    {
        [SerializeField] private Renderer visual;
        [Header("Collection Feedback")]
        [SerializeField, Min(0.01f)] private float collectDuration = 0.2f;
        [SerializeField, Min(1f)] private float collectPopScale = 1.12f;
        [SerializeField, Range(0.05f, 0.95f)] private float collectPopPoint = 0.35f;
        [SerializeField, Min(0f)] private float collectLift = 0.08f;
        [SerializeField] private Vector3 collectRotationEuler =
            new Vector3(-18.2455f, -19.032f, 6.5665f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;
        private Action<Pixel> collectedCallback;
        private Vector3 restingScale;
        private Vector3 collectStartPosition;
        private Quaternion restingRotation;
        private Quaternion collectTargetRotation;
        private float collectTime;
        private bool isCollecting;

        public Vector2Int Position { get; private set; }
        public PixelColor Color { get; private set; }
        public PixelState State { get; private set; }

        // Only the board changes logical state and updates its counters.
        internal void Initialize(PixelData data)
        {
            Initialize(data.Position, data.Color);
        }

        internal void Initialize(Vector2Int position, PixelColor color)
        {
            Position = position;
            Color = color;
            State = PixelState.Present;
            isCollecting = false;
            collectedCallback = null;
            collectTime = 0f;
            restingScale = TF.localScale;
            collectStartPosition = TF.localPosition;
            restingRotation = TF.localRotation;
            collectTargetRotation = restingRotation * Quaternion.Euler(collectRotationEuler);

            if (visual != null)
            {
                propertyBlock ??= new MaterialPropertyBlock();
                visual.GetPropertyBlock(propertyBlock);
                UnityEngine.Color tint = PixelColorUtility.ToUnityColor(Color);
                propertyBlock.SetColor(BaseColorId, tint);
                propertyBlock.SetColor(ColorId, tint);
                visual.SetPropertyBlock(propertyBlock);
            }
        }

        internal void PlayCollected(Action<Pixel> onCompleted)
        {
            State = PixelState.Collected;
            collectedCallback = onCompleted;
            collectStartPosition = TF.localPosition;
            restingScale = TF.localScale;
            restingRotation = TF.localRotation;
            collectTargetRotation = restingRotation * Quaternion.Euler(collectRotationEuler);
            collectTime = 0f;
            isCollecting = true;
        }

        internal void PrepareForPool()
        {
            isCollecting = false;
            collectedCallback = null;
            State = PixelState.Collected;
            TF.localPosition = collectStartPosition;
            TF.localScale = restingScale;
            TF.localRotation = restingRotation;
        }

        private void Update()
        {
            if (!isCollecting)
                return;

            collectTime += Time.deltaTime;
            float progress = Mathf.Clamp01(collectTime / collectDuration);
            float scaleFactor;
            if (progress < collectPopPoint)
                scaleFactor = Mathf.Lerp(1f, collectPopScale, progress / collectPopPoint);
            else
                scaleFactor = Mathf.Lerp(collectPopScale, 0f,
                    (progress - collectPopPoint) / (1f - collectPopPoint));

            TF.localScale = restingScale * scaleFactor;
            TF.localPosition = collectStartPosition + Vector3.up * (collectLift * progress);
            TF.localRotation = Quaternion.Slerp(
                restingRotation, collectTargetRotation, progress);
            if (progress < 1f)
                return;

            isCollecting = false;
            Action<Pixel> callback = collectedCallback;
            collectedCallback = null;
            callback?.Invoke(this);
        }

    }
}
