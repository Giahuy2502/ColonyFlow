using System;
using UnityEngine;

namespace ColonyFlow
{
    public enum PixelColor
    {
        Blue,
        White,
        Purple,
        Cyan,
        Orange,
        Red,
        Green,
        Yellow,
        Black
    }

    public enum PixelCellState : byte
    {
        Empty,
        Present,
        Reserved
    }

    // Pure runtime data: no GameObject or other Unity object references.
    public struct PixelCell
    {
        public PixelColor Color;
        public PixelCellState State;
        public bool IsOutsideReachable;

        public bool IsOccupied => State == PixelCellState.Present || State == PixelCellState.Reserved;
    }

    [Serializable]
    public sealed class PixelData
    {
        [SerializeField] private Vector2Int position;
        [SerializeField] private PixelColor color;

        public Vector2Int Position => position;
        public PixelColor Color => color;

        public PixelData(Vector2Int position, PixelColor color)
        {
            this.position = position;
            this.color = color;
        }
    }
}
