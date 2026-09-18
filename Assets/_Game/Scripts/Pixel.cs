using UnityEngine;

namespace ColonyFlow
{
    public enum PixelState { Present, Reserved, Collected }

    [DisallowMultipleComponent]
    public sealed class Pixel : MonoBehaviour
    {
        [SerializeField] private Renderer visual;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;

        public Vector2Int Position { get; private set; }
        public PixelColor Color { get; private set; }
        public PixelState State { get; private set; }

        // Only the board changes logical state and updates its counters.
        internal void Initialize(PixelData data)
        {
            Position = data.Position;
            Color = data.Color;
            State = PixelState.Present;

            if (visual != null)
            {
                propertyBlock ??= new MaterialPropertyBlock();
                visual.GetPropertyBlock(propertyBlock);
                UnityEngine.Color tint = PixelColorUtility.ToUnityColor(Color);
                propertyBlock.SetColor(BaseColorId, tint);
                propertyBlock.SetColor(ColorId, tint);
                visual.SetPropertyBlock(propertyBlock);
            }

            gameObject.SetActive(true);
        }

        internal void MarkCollected()
        {
            State = PixelState.Collected;
            gameObject.SetActive(false);
        }

        internal void SetReserved(bool reserved)
        {
            if (State != PixelState.Collected)
                State = reserved ? PixelState.Reserved : PixelState.Present;
        }

    }
}
