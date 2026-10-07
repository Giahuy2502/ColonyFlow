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
        [SerializeField, Range(0.1f, 1.5f)]
        [Tooltip("Visual size of the carried pixel relative to a board pixel in the current map.")]
        private float carriedPixelSizeMultiplier = 0.9f;
        [SerializeField, Min(0.01f)] private float carriedPixelPopDuration = 0.18f;
        [SerializeField, Min(1f)] private float carriedPixelPopScale = 1.12f;
        [SerializeField, Range(0.05f, 0.95f)] private float carriedPixelPopPoint = 0.65f;
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
        private Vector3 carriedPixelTargetScale;
        private float carriedPixelPopTime;
        private bool carriedPixelPopping;
        private float cachedMoveSpeedMultiplier = float.NaN;

        // The baked gait covers one stride at this movement speed. Only Move
        // uses this parameter; Eat/Jump and scaled game time remain unchanged.
        private const float GaitReferenceSpeed = 1.5f;
        private static readonly int MoveSpeedMultiplierHash =
            Animator.StringToHash("MoveSpeedMultiplier");

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
            Vector3 targetCenter, Vector3 carriedPixelWorldScale)
        {
            owner = manager;
            EnsureResources();
            task = assignedTask;
            movementPlaneY = startLocalPosition.y;
            startLocalPosition.y = movementPlaneY;
            TF.localPosition = startLocalPosition;
            targetCenter.y = movementPlaneY;
            eatLookTarget = targetCenter;
            CopyRoute(outboundRoute);
            State = AntState.MovingToTarget;
            ApplyBodyColor(assignedTask.Color);
            SetCarriedPixelWorldScale(carriedPixelWorldScale);
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
            carriedPixelPopping = false;
            SetCarriedPixelVisible(false);
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            cachedMoveSpeedMultiplier = float.NaN;
            animName = null;
            ChangeAnim(IdleAnim);
        }

        private void Update()
        {
            if (State == AntState.Pooled)
                return;

            UpdateCarriedPixelPop();

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
            SyncMoveSpeedMultiplier();

            if (waypointIndex >= route.Count)
            {
                CompleteMovementRoute();
                return;
            }

            Vector3 position = TF.localPosition;
            position.y = movementPlaneY;
            TF.localPosition = position;
            float remainingDistance = moveSpeed * Time.deltaTime;
            Vector3 finalStepDirection = Vector3.zero;

            while (remainingDistance > 0f && waypointIndex < route.Count)
            {
                Vector3 destination = route[waypointIndex];
                destination.y = movementPlaneY;
                Vector3 offset = destination - TF.localPosition;
                float distance = offset.magnitude;
                if (distance <= 0.0001f)
                {
                    TF.localPosition = destination;
                    waypointIndex++;
                    continue;
                }

                float step = Mathf.Min(remainingDistance, distance);
                Vector3 stepDirection = offset / distance;
                TF.localPosition += stepDirection * step;
                finalStepDirection = stepDirection;
                remainingDistance -= step;
                if (step >= distance - 0.0001f)
                    waypointIndex++;
            }

            // The route already contains the turn arc. Following its final tangent
            // directly avoids adding a second, delayed rotation on tight corners.
            if (finalStepDirection.sqrMagnitude > 0.0001f)
                TF.localRotation = Quaternion.LookRotation(
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
            Vector3 direction = eatLookTarget - TF.localPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                TF.localRotation = Quaternion.LookRotation(
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
            jumpStart = TF.localPosition;
            Vector3 direction = jumpTarget - jumpStart;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                TF.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            ChangeAnim(JumpAnim);
        }

        private void UpdateJump()
        {
            actionTime += Time.deltaTime;
            float progress = Mathf.Clamp01(actionTime / jumpDuration);
            Vector3 position = Vector3.Lerp(jumpStart, jumpTarget, progress);
            position.y += 4f * jumpHeight * progress * (1f - progress);
            TF.localPosition = position;
            if (progress < 1f)
                return;

            SetCarriedPixelVisible(false);
            owner.NotifyEnteredHole(this);
        }

        public void ChangeAnim(string anim)
        {
            if (anim == MoveAnim)
                SyncMoveSpeedMultiplier();

            if (animator == null || string.IsNullOrEmpty(anim) || animName == anim)
                return;

            if (!string.IsNullOrEmpty(animName))
                animator.ResetTrigger(animName);

            animName = anim;
            animator.SetTrigger(animName);
        }

        private void SyncMoveSpeedMultiplier()
        {
            if (animator == null)
                return;

            float multiplier = moveSpeed / GaitReferenceSpeed;
            if (multiplier == cachedMoveSpeedMultiplier)
                return;

            animator.SetFloat(MoveSpeedMultiplierHash, multiplier);
            cachedMoveSpeedMultiplier = multiplier;
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
            if (carriedPixelVisual == null)
                return;
            if (carriedPixelVisual.activeSelf != visible)
                carriedPixelVisual.SetActive(visible);
            carriedPixelPopping = visible;
            carriedPixelPopTime = 0f;
            carriedPixelVisual.transform.localScale = visible
                ? Vector3.zero
                : carriedPixelTargetScale;
        }

        private void SetCarriedPixelWorldScale(Vector3 worldScale)
        {
            if (carriedPixelVisual == null)
                return;

            worldScale *= carriedPixelSizeMultiplier;
            Transform carriedTransform = carriedPixelVisual.transform;
            Transform parent = carriedTransform.parent;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            carriedPixelTargetScale = new Vector3(
                SafeScaleDivision(worldScale.x, parentScale.x),
                SafeScaleDivision(worldScale.y, parentScale.y),
                SafeScaleDivision(worldScale.z, parentScale.z));
            carriedTransform.localScale = carriedPixelTargetScale;
        }

        private void UpdateCarriedPixelPop()
        {
            if (!carriedPixelPopping || carriedPixelVisual == null)
                return;
            carriedPixelPopTime += Time.deltaTime;
            float progress = Mathf.Clamp01(carriedPixelPopTime / carriedPixelPopDuration);
            float scale = progress < carriedPixelPopPoint
                ? Mathf.Lerp(0f, carriedPixelPopScale, progress / carriedPixelPopPoint)
                : Mathf.Lerp(carriedPixelPopScale, 1f,
                    (progress - carriedPixelPopPoint) / (1f - carriedPixelPopPoint));
            carriedPixelVisual.transform.localScale = carriedPixelTargetScale * scale;
            if (progress >= 1f)
                carriedPixelPopping = false;
        }

        private static float SafeScaleDivision(float worldScale, float parentScale)
        {
            return Mathf.Approximately(parentScale, 0f)
                ? worldScale
                : worldScale / Mathf.Abs(parentScale);
        }

        private void EnsureResources()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }
    }
}
