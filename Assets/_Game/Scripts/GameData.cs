using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(fileName = "GameData", menuName = "Colony Flow/Game Data")]
    public sealed class GameData : ScriptableObject
    {
        [Header("Visual Settings")]
        [SerializeField] private PixelColorUtility pixelColorPalette;

        [Header("Levels")]
        [SerializeField] private List<ColonyLevelData> levels =
            new List<ColonyLevelData>();

        public PixelColorUtility PixelColorPalette => pixelColorPalette;
        public int LevelCount => levels.Count;

        private void OnEnable()
        {
            PixelColorUtility.SetDefault(pixelColorPalette);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            PixelColorUtility.SetDefault(pixelColorPalette);
        }
#endif

        public ColonyLevelData GetLevel(int index)
        {
            return index >= 0 && index < levels.Count ? levels[index] : null;
        }
    }
}
