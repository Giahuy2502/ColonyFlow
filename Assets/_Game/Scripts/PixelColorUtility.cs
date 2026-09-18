using UnityEngine;

namespace ColonyFlow
{
    public static class PixelColorUtility
    {
        public static Color ToUnityColor(PixelColor color)
        {
            switch (color)
            {
                case PixelColor.Blue: return new Color(0.15f, 0.4f, 1f);
                case PixelColor.White: return Color.white;
                case PixelColor.Purple: return new Color(0.65f, 0.2f, 0.9f);
                case PixelColor.Cyan: return Color.cyan;
                case PixelColor.Orange: return new Color(1f, 0.5f, 0.1f);
                case PixelColor.Red: return new Color(0.95f, 0.12f, 0.16f);
                case PixelColor.Green: return new Color(0.12f, 0.72f, 0.28f);
                case PixelColor.Yellow: return new Color(1f, 0.82f, 0.08f);
                case PixelColor.Black: return new Color(0.05f, 0.05f, 0.07f);
                default: return Color.magenta;
            }
        }
    }
}
