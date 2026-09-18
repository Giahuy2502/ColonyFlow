using System;
using UnityEngine;

namespace ColonyFlow
{
    public enum ColonyState : byte
    {
        InColumn,
        MovingToTray,
        Active,
        Blocked,
        Completed
    }

    [DisallowMultipleComponent]
    public sealed class Colony : MonoBehaviour
    {
        [SerializeField] private PixelColor color;
        [SerializeField, Min(1)] private int pixelCount = 1;
        [SerializeField] private ColonyView view;

        public PixelColor Color => color;
        public int InitialCount => pixelCount;
        public int RemainingCount { get; private set; }
        public int InFlightCount { get; private set; }
        public int UnassignedCount => Mathf.Max(0, RemainingCount - InFlightCount);
        public ColonyState State { get; private set; }
        public bool CanReceiveTask => (State == ColonyState.Active || State == ColonyState.Blocked) && UnassignedCount > 0;
        public ColonyView View => view;

        public event Action<Colony, ColonyState> StateChanged;
        public event Action<Colony> CountChanged;
        public event Action<Colony> Completed;

        private void Awake()
        {
            ResetRuntimeState();
        }

        public void Configure(PixelColor newColor, int count)
        {
            color = newColor;
            pixelCount = Mathf.Max(1, count);
            ResetRuntimeState();
        }

        public void ResetRuntimeState()
        {
            RemainingCount = Mathf.Max(1, pixelCount);
            InFlightCount = 0;
            SetState(ColonyState.InColumn);
            CountChanged?.Invoke(this);
        }

        internal void BeginMovingToTray()
        {
            if (State == ColonyState.InColumn)
                SetState(ColonyState.MovingToTray);
        }

        internal void Activate()
        {
            if (State == ColonyState.MovingToTray)
                SetState(ColonyState.Active);
        }

        internal bool TryBeginTask()
        {
            if (!CanReceiveTask)
                return false;

            InFlightCount++;
            SetState(ColonyState.Active);
            CountChanged?.Invoke(this);
            return true;
        }

        internal void CompleteTask()
        {
            if (InFlightCount <= 0 || RemainingCount <= 0)
                throw new InvalidOperationException($"Colony {name} completed a task it does not own.");

            InFlightCount--;
            RemainingCount--;
            CountChanged?.Invoke(this);

            if (RemainingCount == 0)
            {
                SetState(ColonyState.Completed);
                Completed?.Invoke(this);
            }
        }

        internal void CancelTask()
        {
            if (InFlightCount <= 0)
                return;

            InFlightCount--;
            CountChanged?.Invoke(this);
        }

        internal void SetBlocked(bool blocked)
        {
            if (State == ColonyState.Completed || State == ColonyState.InColumn ||
                State == ColonyState.MovingToTray)
                return;

            SetState(blocked && InFlightCount == 0 ? ColonyState.Blocked : ColonyState.Active);
        }

        private void SetState(ColonyState next)
        {
            if (State == next)
                return;
            State = next;
            StateChanged?.Invoke(this, next);
        }
    }
}
