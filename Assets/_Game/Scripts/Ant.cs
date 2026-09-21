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
    public sealed class Ant : GameUnit
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.03f;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private GameObject carriedPixelVisual;
        [SerializeField] private Renderer carriedPixelRenderer;

        private readonly List<Vector3> route = new List<Vector3>(64);
        private AntManager owner;
        private ColonyTask task;
        private Vector3 targetPosition;
        private float movementPlaneY;
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
            movementPlaneY = startLocalPosition.y;
            startLocalPosition.y = movementPlaneY;
            targetLocalPosition.y = movementPlaneY;
            transform.localPosition = startLocalPosition;
            targetPosition = targetLocalPosition;
            CopyRoute(outboundRoute);
            State = AntState.MovingToTarget;
            ApplyBodyColor(assignedTask.Color);
            SetCarriedPixelVisible(false);
            gameObject.SetActive(true);
        }

        internal void BeginReturn(List<Vector3> returnRoute)
        {
            CopyRoute(returnRoute);
            State = AntState.ReturningToHole;
            ApplyRendererColor(carriedPixelRenderer, task.Color);
            SetCarriedPixelVisible(true);
        }

        internal void ReturnToPool()
        {
            route.Clear();
            waypointIndex = 0;
            owner = null;
            task = default;
            State = AntState.Pooled;
            SetCarriedPixelVisible(false);
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
            destination.y = movementPlaneY;
            Vector3 currentPosition = transform.localPosition;
            if (!Mathf.Approximately(currentPosition.y, movementPlaneY))
            {
                currentPosition.y = movementPlaneY;
                transform.localPosition = currentPosition;
            }
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
            {
                for (int i = 0; i < source.Count; i++)
                {
                    Vector3 waypoint = source[i];
                    waypoint.y = movementPlaneY;
                    route.Add(waypoint);
                }
            }
            waypointIndex = 0;
        }

        private void ApplyRendererColor(Renderer targetRenderer, PixelColor color)
        {
            if (targetRenderer == null)
                return;
            Color tint = PixelColorUtility.ToUnityColor(color);
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", tint);
            propertyBlock.SetColor("_Color", tint);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyBodyColor(PixelColor color)
        {
            if (bodyRenderers == null)
                return;

            for (int i = 0; i < bodyRenderers.Length; i++)
                ApplyRendererColor(bodyRenderers[i], color);
        }

        private void SetCarriedPixelVisible(bool visible)
        {
            if (carriedPixelVisual != null && carriedPixelVisual.activeSelf != visible)
                carriedPixelVisual.SetActive(visible);
        }

        private void EnsureResources()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }
    }
}
