using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class ColonyTray : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Min(1)] private int capacity = 5;

        [Header("Layout")]
        [SerializeField] private List<Transform> slotObjects = new List<Transform>();
        [SerializeField, Min(0f)] private float slotSpacing = 0.9f;
        [SerializeField] private float slotCenterX = 2.1f;
        [SerializeField] private Vector3 slotScale = new Vector3(0.72f, 0.12f, 0.72f);
        [SerializeField] private float slotHeight;
        [SerializeField] private float depth = -2.4f;
        [SerializeField] private float colonyHeight = 0.31f;

        [Header("Expansion")]
        [SerializeField, Min(1)] private int expansionAmount = 1;
        [SerializeField, Min(0.01f)] private float slotPopDuration = 0.22f;
        [SerializeField, Min(1f)] private float slotPopEasePower = 3f;
        [SerializeField, Min(0f)] private float slotPopOvershoot = 0.12f;
        private Colony[] slots;

        public int Capacity => capacity;
        public int SlotObjectCount => slotObjects.Count;
        public float SlotSpacing => slotSpacing;
        public float SlotCenterX => slotCenterX;
        public Vector3 SlotScale => slotScale;
        public float SlotHeight => slotHeight;
        public float Depth => depth;
        public float ColonyHeight => colonyHeight;
        public int ExpansionAmount => expansionAmount;
        public float SlotPopDuration => slotPopDuration;
        public float SlotPopEasePower => slotPopEasePower;
        public float SlotPopOvershoot => slotPopOvershoot;
        public int OccupiedCount { get; private set; }
        public bool HasFreeSlot => OccupiedCount < capacity;

        public event Action<int, Colony> ColonyAdded;
        public event Action<int, Colony> ColonyRemoved;

        public Transform GetSlotObject(int index)
        {
            return index >= 0 && index < slotObjects.Count ? slotObjects[index] : null;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(1, capacity);
            slotSpacing = Mathf.Max(0f, slotSpacing);
            expansionAmount = Mathf.Max(1, expansionAmount);
            slotPopDuration = Mathf.Max(0.01f, slotPopDuration);
            slotPopEasePower = Mathf.Max(1f, slotPopEasePower);
            slotPopOvershoot = Mathf.Max(0f, slotPopOvershoot);

            if (!Application.isPlaying)
                RefreshEditorSlotLayout();
        }

        private void RefreshEditorSlotLayout()
        {
            int slotCount = slotObjects.Count;
            int visibleSlotCount = Mathf.Min(capacity, slotCount);
            float startX = slotCenterX - (visibleSlotCount - 1) * slotSpacing * 0.5f;
            for (int i = 0; i < slotCount; i++)
            {
                Transform slot = slotObjects[i];
                if (slot == null)
                    continue;
                Vector3 position = slot.localPosition;
                position.x = startX + i * slotSpacing;
                position.y = slotHeight;
                position.z = depth;
                slot.localPosition = position;
                slot.localScale = slotScale;
            }
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
