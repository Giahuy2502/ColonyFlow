using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum AntState : byte
    {
        Pooled,
        MovingToTarget,
        Eating,
        ReturningToHole,
        Jumping
    }

    [DisallowMultipleComponent]
    public sealed class Ant : GameUnit
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Range(0f, 0.2f)] private float headReach = 0.08f;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private GameObject carriedPixelVisual;
        [SerializeField] private Renderer carriedPixelRenderer;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.05f)] private float eatDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float jumpDuration = 0.42f;
        [SerializeField, Min(0f)] private float jumpHeight = 0.18f;
        [SerializeField, Min(0f)] private float jumpStartDistanceFromHole = 0.08f;

        private readonly List<Vector3> route = new List<Vector3>(64);
        private AntManager owner;
        private ColonyTask task;
        private float movementPlaneY;
        private int waypointIndex;
        private MaterialPropertyBlock propertyBlock;
        private float actionTime;
        private Vector3 jumpStart;
        private Vector3 jumpTarget;
        private Vector3 eatLookTarget;
        private string animName;

        private const string MoveAnim = "Move";
        private const string EatAnim = "Eat";
        private const string JumpAnim = "Jump";
        private const string IdleAnim = "Idle";

        public AntState State { get; private set; } = AntState.Pooled;
        public int TaskId => task.Id;
        internal bool IsTaskColor(PixelColor color) => task.Owner != null && task.Color == color;
        internal float HeadReach => headReach;
        internal float JumpStartDistanceFromHole => jumpStartDistanceFromHole;

        private void Awake()
        {
            EnsureResources();
        }

        internal void PrepareForPool()
        {
            EnsureResources();
            OnDespawn();
        }

        internal void OnInit(AntManager manager, ColonyTask assignedTask,
            Vector3 startLocalPosition, List<Vector3> outboundRoute,
            Vector3 targetCenter)
        {
            owner = manager;
            EnsureResources();
            task = assignedTask;
            movementPlaneY = startLocalPosition.y;
            startLocalPosition.y = movementPlaneY;
            transform.localPosition = startLocalPosition;
            targetCenter.y = movementPlaneY;
            eatLookTarget = targetCenter;
            CopyRoute(outboundRoute);
            State = AntState.MovingToTarget;
            ApplyBodyColor(assignedTask.Color);
            SetCarriedPixelVisible(false);
            gameObject.SetActive(true);
            ChangeAnim(MoveAnim);
        }

        internal void BeginReturn(List<Vector3> returnRoute, Vector3 holdPosition)
        {
            holdPosition.y = movementPlaneY;
            jumpTarget = holdPosition;
            CopyRoute(returnRoute);
            State = AntState.ReturningToHole;
            ApplyRendererColor(carriedPixelRenderer, task.Color);
            SetCarriedPixelVisible(true);
            ChangeAnim(MoveAnim);
        }

        internal void OnDespawn()
        {
            route.Clear();
            waypointIndex = 0;
            owner = null;
            task = default;
            eatLookTarget = Vector3.zero;
            State = AntState.Pooled;
            SetCarriedPixelVisible(false);
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            animName = null;
            ChangeAnim(IdleAnim);
        }

        private void Update()
        {
            if (State == AntState.Pooled)
                return;

            if (State == AntState.Eating)
            {
                actionTime += Time.deltaTime;
                if (actionTime >= eatDuration)
                    owner.NotifyTargetReached(this, task);
                return;
            }

            if (State == AntState.Jumping)
            {
                UpdateJump();
                return;
            }

            UpdateMovement();
        }

        private void UpdateMovement()
        {
            if (waypointIndex >= route.Count)
            {
                CompleteMovementRoute();
                return;
            }

            Vector3 position = transform.localPosition;
            position.y = movementPlaneY;
            transform.localPosition = position;
            float remainingDistance = moveSpeed * Time.deltaTime;
            Vector3 finalStepDirection = Vector3.zero;

            while (remainingDistance > 0f && waypointIndex < route.Count)
            {
                Vector3 destination = route[waypointIndex];
                destination.y = movementPlaneY;
                Vector3 offset = destination - transform.localPosition;
                float distance = offset.magnitude;
                if (distance <= 0.0001f)
                {
                    transform.localPosition = destination;
                    waypointIndex++;
                    continue;
                }

                float step = Mathf.Min(remainingDistance, distance);
                Vector3 stepDirection = offset / distance;
                transform.localPosition += stepDirection * step;
                finalStepDirection = stepDirection;
                remainingDistance -= step;
                if (step >= distance - 0.0001f)
                    waypointIndex++;
            }

            // The route already contains the turn arc. Following its final tangent
            // directly avoids adding a second, delayed rotation on tight corners.
            if (finalStepDirection.sqrMagnitude > 0.0001f)
                transform.localRotation = Quaternion.LookRotation(
                    finalStepDirection, Vector3.up);

            if (waypointIndex >= route.Count)
                CompleteMovementRoute();
        }

        private void CompleteMovementRoute()
        {
            if (State == AntState.ReturningToHole)
                BeginJump();
            else
                BeginEat();
        }

        private void BeginEat()
        {
            Vector3 direction = eatLookTarget - transform.localPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.localRotation = Quaternion.LookRotation(
                    direction.normalized, Vector3.up);

            State = AntState.Eating;
            actionTime = 0f;
            SoundManager.Instance?.PlaySfx(SfxId.AntEat);
            ChangeAnim(EatAnim);
        }

        private void BeginJump()
        {
            State = AntState.Jumping;
            actionTime = 0f;
            SoundManager.Instance?.PlaySfx(SfxId.AntJump);
            jumpStart = transform.localPosition;
            Vector3 direction = jumpTarget - jumpStart;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            ChangeAnim(JumpAnim);
        }

        private void UpdateJump()
        {
            actionTime += Time.deltaTime;
            float progress = Mathf.Clamp01(actionTime / jumpDuration);
            Vector3 position = Vector3.Lerp(jumpStart, jumpTarget, progress);
            position.y += 4f * jumpHeight * progress * (1f - progress);
            transform.localPosition = position;
            if (progress < 1f)
                return;

            SetCarriedPixelVisible(false);
            owner.NotifyEnteredHole(this);
        }

        public void ChangeAnim(string anim)
        {
            if (animator == null || string.IsNullOrEmpty(anim) || animName == anim)
                return;

            if (!string.IsNullOrEmpty(animName))
                animator.ResetTrigger(animName);

            animName = anim;
            animator.SetTrigger(animName);
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
