using UnityEngine;

namespace ColonyFlow
{
    public static class PixelColorUtility
    {
        public static Color ToUnityColor(PixelColor color)
        {
            switch (color)
            {
                case PixelColor.Blue: return new Color(0.22f, 0.48f, 0.92f);
                case PixelColor.White: return new Color(0.96f, 0.95f, 0.90f);
                case PixelColor.Purple: return new Color(0.66f, 0.30f, 0.78f);
                case PixelColor.Cyan: return new Color(0.20f, 0.75f, 0.90f);
                case PixelColor.Orange: return new Color(1f, 0.62f, 0.20f);
                case PixelColor.Red: return new Color(0.93f, 0.22f, 0.28f);
                case PixelColor.Green: return new Color(0.22f, 0.72f, 0.35f);
                case PixelColor.Yellow: return new Color(1f, 0.78f, 0.20f);
                case PixelColor.Black: return new Color(0.18f, 0.18f, 0.22f);
                default: return Color.magenta;
            }
        }
    }
}
