using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class ColonyColumn : MonoBehaviour
    {
        [SerializeField] private List<Colony> colonies = new List<Colony>();
        private int frontIndex;

        public int RemainingCount => Mathf.Max(0, colonies.Count - frontIndex);
        public bool HasColony => Peek() != null;

        private void Awake()
        {
            ResetColumn();
        }

        public void ResetColumn()
        {
            frontIndex = 0;
            for (int i = 0; i < colonies.Count; i++)
                if (colonies[i] != null)
                    colonies[i].ResetRuntimeState();
        }

        public void Configure(IEnumerable<Colony> orderedColonies)
        {
            colonies.Clear();
            if (orderedColonies != null)
                colonies.AddRange(orderedColonies);
            ResetColumn();
        }

        public Colony Peek()
        {
            SkipMissingEntries();
            return frontIndex < colonies.Count ? colonies[frontIndex] : null;
        }

        internal bool TryTakeFront(out Colony colony)
        {
            colony = Peek();
            if (colony == null)
                return false;

            frontIndex++;
            SkipMissingEntries();
            return true;
        }

        internal bool TryTakeSpecific(Colony colony)
        {
            if (colony == null)
                return false;

            int index = colonies.IndexOf(colony, frontIndex);
            if (index <= frontIndex)
                return false;

            colonies.RemoveAt(index);
            return true;
        }

        internal void CopyRemaining(List<Colony> destination)
        {
            if (destination == null)
                throw new System.ArgumentNullException(nameof(destination));

            destination.Clear();
            for (int i = frontIndex; i < colonies.Count; i++)
                if (colonies[i] != null)
                    destination.Add(colonies[i]);
        }

        internal void ReplaceRemaining(IReadOnlyList<Colony> replacements)
        {
            if (frontIndex < colonies.Count)
                colonies.RemoveRange(frontIndex, colonies.Count - frontIndex);
            if (replacements == null)
                return;

            for (int i = 0; i < replacements.Count; i++)
                if (replacements[i] != null)
                    colonies.Add(replacements[i]);
        }

        internal int RemoveColor(PixelColor color, List<Colony> removed)
        {
            int count = 0;
            for (int i = colonies.Count - 1; i >= frontIndex; i--)
            {
                Colony colony = colonies[i];
                if (colony == null || colony.Color != color)
                    continue;

                colonies.RemoveAt(i);
                if (removed != null)
                    removed.Add(colony);
                count++;
            }
            SkipMissingEntries();
            return count;
        }

        private void SkipMissingEntries()
        {
            while (frontIndex < colonies.Count && colonies[frontIndex] == null)
                frontIndex++;
        }
    }
}
