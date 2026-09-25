using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class ColonyTray : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 5;
        private Colony[] slots;

        public int Capacity => capacity;
        public int OccupiedCount { get; private set; }
        public bool HasFreeSlot => OccupiedCount < capacity;
        public bool IsFull => OccupiedCount >= capacity;

        public event Action<int, Colony> ColonyAdded;
        public event Action<int, Colony> ColonyRemoved;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (slots != null)
            {
                foreach (Colony colony in slots)
                    if (colony != null)
                        colony.Completed -= OnColonyCompleted;
            }

            capacity = Mathf.Max(1, capacity);
            slots = new Colony[capacity];
            OccupiedCount = 0;
        }

        public void Configure(int newCapacity)
        {
            capacity = Mathf.Max(1, newCapacity);
            Initialize();
        }

        public bool ExpandCapacity(int amount)
        {
            if (amount <= 0 || slots == null)
                return false;

            capacity += amount;
            Array.Resize(ref slots, capacity);
            return true;
        }

        public bool TryAdd(Colony colony, out int slotIndex)
        {
            slotIndex = -1;
            if (colony == null || !HasFreeSlot || Contains(colony))
                return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                    continue;

                slots[i] = colony;
                slotIndex = i;
                OccupiedCount++;
                colony.Completed += OnColonyCompleted;
                colony.BeginMovingToTray();
                ColonyAdded?.Invoke(i, colony);
                return true;
            }
            return false;
        }

        public bool Remove(Colony colony)
        {
            if (colony == null || slots == null)
                return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != colony)
                    continue;

                slots[i] = null;
                OccupiedCount--;
                colony.Completed -= OnColonyCompleted;
                ColonyRemoved?.Invoke(i, colony);
                return true;
            }
            return false;
        }

        public Colony GetSlot(int index)
        {
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        }

        public void CopyColonies(List<Colony> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (slots == null)
                return;
            foreach (Colony colony in slots)
                if (colony != null)
                    destination.Add(colony);
        }

        private bool Contains(Colony colony)
        {
            if (slots == null)
                return false;
            foreach (Colony item in slots)
                if (item == colony)
                    return true;
            return false;
        }

        private void OnColonyCompleted(Colony colony)
        {
            Remove(colony);
        }
    }
}
