using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(fileName = "PixelColorPalette", menuName = "Colony Flow/Pixel Color Palette")]
    public sealed class PixelColorUtility : ScriptableObject
    {
        [Header("Pixel Colors")]
        [SerializeField] private Color blue = new Color(0.22f, 0.48f, 0.92f);
        [SerializeField] private Color white = new Color(0.96f, 0.95f, 0.90f);
        [SerializeField] private Color purple = new Color(0.66f, 0.30f, 0.78f);
        [SerializeField] private Color cyan = new Color(0.20f, 0.75f, 0.90f);
        [SerializeField] private Color orange = new Color(1f, 0.62f, 0.20f);
        [SerializeField] private Color red = new Color(0.93f, 0.22f, 0.28f);
        [SerializeField] private Color green = new Color(0.22f, 0.72f, 0.35f);
        [SerializeField] private Color yellow = new Color(1f, 0.78f, 0.20f);
        [SerializeField] private Color black = new Color(0.18f, 0.18f, 0.22f);

        private static PixelColorUtility defaultPalette;

        internal static void SetDefault(PixelColorUtility palette)
        {
            defaultPalette = palette;
        }

        public Color GetColor(PixelColor color)
        {
            switch (color)
            {
                case PixelColor.Blue: return blue;
                case PixelColor.White: return white;
                case PixelColor.Purple: return purple;
                case PixelColor.Cyan: return cyan;
                case PixelColor.Orange: return orange;
                case PixelColor.Red: return red;
                case PixelColor.Green: return green;
                case PixelColor.Yellow: return yellow;
                case PixelColor.Black: return black;
                default: return Color.magenta;
            }
        }

        // Kept as a static entry point so existing renderers and prefabs do not
        // need their own copy of the shared palette reference.
        public static Color ToUnityColor(PixelColor color)
        {
            if (defaultPalette == null)
            {
                Debug.LogError("GameData does not have a Pixel Color Palette assigned.");
                return Color.magenta;
            }

            return defaultPalette.GetColor(color);
        }
    }
}
