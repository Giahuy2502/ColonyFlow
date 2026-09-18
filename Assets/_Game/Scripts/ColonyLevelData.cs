using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum GeneratedPixelPattern : byte
    {
        Watermelon,
        FilledBoard
    }

    [CreateAssetMenu(fileName = "Level_", menuName = "Colony Flow/Level Data")]
    public sealed class ColonyLevelData : ScriptableObject
    {
        [SerializeField, Range(5, 100)] private int width = 20;
        [SerializeField, Range(5, 100)] private int height = 20;
        [SerializeField, Min(1f)] private float boardWorldSize = 4.4f;
        [SerializeField] private GeneratedPixelPattern pattern = GeneratedPixelPattern.Watermelon;
        [SerializeField] private int seed = 512;
        [SerializeField, Min(1)] private int trayCapacity = 5;
        [SerializeField, Range(1, 10)] private int columnCount = 5;
        [SerializeField, Min(1)] private int maxColonyQuota = 20;

        public Vector2Int BoardSize => new Vector2Int(width, height);
        public float CellSize => boardWorldSize / Mathf.Max(width, height);
        public int TrayCapacity => trayCapacity;

        public void BuildPixels(List<PixelData> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (TryGetGeneratedColor(x, y, out PixelColor color))
                        destination.Add(new PixelData(new Vector2Int(x, y), color));
                }
            }
        }

        public void BuildColumns(List<PixelData> generatedPixels,
            List<ColonyColumnSpec> destination)
        {
            if (generatedPixels == null || destination == null)
                throw new ArgumentNullException();

            destination.Clear();
            for (int i = 0; i < columnCount; i++)
                destination.Add(new ColonyColumnSpec());

            var counts = new Dictionary<PixelColor, int>();
            foreach (PixelData pixel in generatedPixels)
            {
                counts.TryGetValue(pixel.Color, out int count);
                counts[pixel.Color] = count + 1;
            }

            PixelColor[] order = pattern == GeneratedPixelPattern.Watermelon
                ? new[] { PixelColor.Green, PixelColor.Red, PixelColor.Purple }
                : (PixelColor[])Enum.GetValues(typeof(PixelColor));

            int nextColumn = 0;
            foreach (PixelColor color in order)
            {
                if (!counts.TryGetValue(color, out int remaining))
                    continue;
                while (remaining > 0)
                {
                    int quota = Mathf.Min(maxColonyQuota, remaining);
                    destination[nextColumn].colonies.Add(new ColonySpec(color, quota));
                    remaining -= quota;
                    nextColumn = (nextColumn + 1) % columnCount;
                }
            }
        }

        private bool TryGetGeneratedColor(int x, int y, out PixelColor color)
        {
            if (pattern == GeneratedPixelPattern.FilledBoard)
            {
                color = PixelColor.Blue;
                return true;
            }

            float nx = ((x + 0.5f) / width - 0.5f) * 2f;
            float ny = ((y + 0.5f) / height - 0.5f) * 2f;
            float radius = nx * nx / 0.92f + ny * ny / 0.78f;
            if (radius > 1f)
            {
                color = default;
                return false;
            }

            if (radius > 0.72f)
                color = PixelColor.Green;
            else if (Hash(x, y) % 29 == 0)
                color = PixelColor.Purple;
            else
                color = PixelColor.Red;
            return true;
        }

        private int Hash(int x, int y)
        {
            unchecked
            {
                int value = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                return value & int.MaxValue;
            }
        }
    }
}
