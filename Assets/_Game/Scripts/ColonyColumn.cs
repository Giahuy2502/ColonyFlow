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

        private void SkipMissingEntries()
        {
            while (frontIndex < colonies.Count && colonies[frontIndex] == null)
                frontIndex++;
        }
    }
}
