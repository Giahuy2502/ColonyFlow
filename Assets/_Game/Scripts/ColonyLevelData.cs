using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum GeneratedPixelPattern : byte
    {
        Watermelon,
        FilledBoard,
        RainbowRings,
        CandySpiral,
        Mosaic
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

        public bool ValidateGeneratedLevel(IReadOnlyList<PixelData> generatedPixels,
            IReadOnlyList<ColonyColumnSpec> generatedColumns, out string error)
        {
            error = string.Empty;
            if (generatedPixels == null || generatedPixels.Count == 0)
            {
                error = $"{name}: board contains no pixels.";
                return false;
            }
            if (generatedColumns == null || generatedColumns.Count == 0)
            {
                error = $"{name}: level contains no Colony columns.";
                return false;
            }
            if (trayCapacity <= 0 || columnCount <= 0 || maxColonyQuota <= 0)
            {
                error = $"{name}: tray capacity, column count and Colony quota must be positive.";
                return false;
            }

            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            int[] pixelCounts = new int[colorCount];
            int[] colonyCounts = new int[colorCount];
            var occupiedPositions = new HashSet<int>();

            for (int i = 0; i < generatedPixels.Count; i++)
            {
                PixelData pixel = generatedPixels[i];
                if (pixel == null || pixel.Position.x < 0 || pixel.Position.x >= width ||
                    pixel.Position.y < 0 || pixel.Position.y >= height ||
                    !Enum.IsDefined(typeof(PixelColor), pixel.Color))
                {
                    error = $"{name}: pixel #{i} is null or outside the board.";
                    return false;
                }

                int positionIndex = pixel.Position.y * width + pixel.Position.x;
                if (!occupiedPositions.Add(positionIndex))
                {
                    error = $"{name}: duplicate pixel at {pixel.Position}.";
                    return false;
                }
                pixelCounts[(int)pixel.Color]++;
            }

            for (int columnIndex = 0; columnIndex < generatedColumns.Count; columnIndex++)
            {
                ColonyColumnSpec column = generatedColumns[columnIndex];
                if (column == null || column.colonies == null)
                {
                    error = $"{name}: Colony column {columnIndex + 1} is null.";
                    return false;
                }

                for (int colonyIndex = 0; colonyIndex < column.colonies.Count; colonyIndex++)
                {
                    ColonySpec colony = column.colonies[colonyIndex];
                    if (colony == null || colony.count <= 0 ||
                        !Enum.IsDefined(typeof(PixelColor), colony.color))
                    {
                        error = $"{name}: invalid Colony in column {columnIndex + 1}.";
                        return false;
                    }
                    colonyCounts[(int)colony.color] += colony.count;
                }
            }

            for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                if (pixelCounts[colorIndex] == colonyCounts[colorIndex])
                    continue;
                PixelColor color = (PixelColor)colorIndex;
                error = $"{name}: {color} has {pixelCounts[colorIndex]} pixels but " +
                        $"{colonyCounts[colorIndex]} Colony capacity.";
                return false;
            }
            return true;
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
            float radiusSquared = nx * nx / 0.92f + ny * ny / 0.78f;
            if (radiusSquared > 1f)
            {
                color = default;
                return false;
            }

            if (pattern == GeneratedPixelPattern.RainbowRings)
            {
                PixelColor[] palette =
                {
                    PixelColor.Cyan, PixelColor.Blue, PixelColor.Purple,
                    PixelColor.Red, PixelColor.Orange, PixelColor.Yellow,
                    PixelColor.Green, PixelColor.White
                };
                float radius = Mathf.Sqrt(radiusSquared);
                int ring = Mathf.Clamp(Mathf.FloorToInt((1f - radius) * 8f), 0, palette.Length - 1);
                int shimmer = Hash(x / 2, y / 2) % 7 == 0 ? 1 : 0;
                color = palette[(ring + shimmer) % palette.Length];
                return true;
            }

            if (pattern == GeneratedPixelPattern.CandySpiral)
            {
                PixelColor[] palette =
                {
                    PixelColor.Red, PixelColor.White, PixelColor.Cyan,
                    PixelColor.Yellow, PixelColor.Purple, PixelColor.Orange
                };
                float radius = Mathf.Sqrt(radiusSquared);
                float angle = Mathf.Atan2(ny, nx) / (Mathf.PI * 2f) + 0.5f;
                int stripe = Mathf.FloorToInt(angle * 12f + radius * 13f);
                color = palette[PositiveModulo(stripe, palette.Length)];
                return true;
            }

            if (pattern == GeneratedPixelPattern.Mosaic)
            {
                PixelColor[] palette =
                {
                    PixelColor.Blue, PixelColor.Cyan, PixelColor.Green,
                    PixelColor.Yellow, PixelColor.Orange, PixelColor.Red,
                    PixelColor.Purple, PixelColor.White, PixelColor.Black
                };
                int cellX = x / 3;
                int cellY = y / 3;
                int index = Hash(cellX, cellY) + cellX * 3 + cellY * 5;
                color = palette[PositiveModulo(index, palette.Length)];
                return true;
            }

            if (radiusSquared > 0.72f)
                color = PixelColor.Green;
            else if (Hash(x, y) % 29 == 0)
                color = PixelColor.Purple;
            else
                color = PixelColor.Red;
            return true;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
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
