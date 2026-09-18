using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum AntState : byte
    {
        Pooled,
        MovingToTarget,
        ReturningToHole
    }

    [DisallowMultipleComponent]
    public sealed class Ant : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.03f;
        [SerializeField] private Renderer bodyRenderer;

        private readonly List<Vector3> route = new List<Vector3>(64);
        private AntManager owner;
        private ColonyTask task;
        private Vector3 targetPosition;
        private int waypointIndex;
        private MaterialPropertyBlock propertyBlock;

        public AntState State { get; private set; } = AntState.Pooled;
        public int TaskId => task.Id;

        private void Awake()
        {
            EnsureResources();
        }

        internal void PrepareForPool()
        {
            EnsureResources();
            ReturnToPool();
        }

        internal void Launch(AntManager manager, ColonyTask assignedTask,
            Vector3 startLocalPosition, List<Vector3> outboundRoute, Vector3 targetLocalPosition)
        {
            owner = manager;
            EnsureResources();
            task = assignedTask;
            transform.localPosition = startLocalPosition;
            targetPosition = targetLocalPosition;
            CopyRoute(outboundRoute);
            State = AntState.MovingToTarget;
            ApplyColor(assignedTask.Color);
            gameObject.SetActive(true);
        }

        internal void BeginReturn(List<Vector3> returnRoute)
        {
            CopyRoute(returnRoute);
            State = AntState.ReturningToHole;
        }

        internal void ReturnToPool()
        {
            route.Clear();
            waypointIndex = 0;
            owner = null;
            task = default;
            State = AntState.Pooled;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (State == AntState.Pooled)
                return;

            if (State == AntState.ReturningToHole && waypointIndex >= route.Count)
            {
                owner.NotifyEnteredHole(this);
                return;
            }

            Vector3 destination = waypointIndex < route.Count ? route[waypointIndex] : targetPosition;
            Vector3 offset = destination - transform.localPosition;
            if (offset.sqrMagnitude > arrivalDistance * arrivalDistance)
            {
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition, destination, moveSpeed * Time.deltaTime);
                if (offset.sqrMagnitude > 0.0001f)
                    transform.localRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                return;
            }

            transform.localPosition = destination;
            if (waypointIndex < route.Count)
            {
                waypointIndex++;
                return;
            }

            owner.NotifyTargetReached(this, task);
        }

        private void CopyRoute(List<Vector3> source)
        {
            route.Clear();
            if (source != null)
                route.AddRange(source);
            waypointIndex = 0;
        }

        private void ApplyColor(PixelColor color)
        {
            if (bodyRenderer == null)
                return;
            Color tint = PixelColorUtility.ToUnityColor(color);
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", tint);
            propertyBlock.SetColor("_Color", tint);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsureResources()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }
    }
}
