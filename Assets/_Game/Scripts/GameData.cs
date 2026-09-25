using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [CreateAssetMenu(fileName = "GameData", menuName = "Colony Flow/Game Data")]
    public sealed class GameData : ScriptableObject
    {
        [SerializeField] private List<ColonyLevelData> levels =
            new List<ColonyLevelData>();

        public int LevelCount => levels.Count;

        public ColonyLevelData GetLevel(int index)
        {
            return index >= 0 && index < levels.Count ? levels[index] : null;
        }
    }
}
