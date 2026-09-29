using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class ColonyView : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Renderer topRenderer;
        [SerializeField] private TextMeshPro countText;
        [SerializeField] private Collider clickCollider;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;
        [SerializeField, Min(0.1f)] private float columnReflowSpeed = 4f;
        [SerializeField, Min(0.01f)] private float disappearDuration = 0.32f;
        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float trayBounceDuration = 0.22f;
        [SerializeField, Min(1f)] private float trayBounceScale = 1.12f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;
        [SerializeField] private Colony colony;
        private LevelManager levelManager;
        private int columnIndex;
        private Vector3 targetLocalPosition;
        private float currentMoveSpeed;
        private bool activateOnArrival;
        private bool isMoving;
        private bool isDisappearing;
        private float disappearTime;
        private string animName;
        private int displayedCount = -1;
        private Vector3 visualRestingScale;
        private float trayBounceTime;
        private bool isTrayBouncing;
        private static readonly Dictionary<Collider, ColonyView> ClickTargets = new Dictionary<Collider, ColonyView>();

        private const string IdleAnim = "Idle";
        private const string MoveAnim = "Move";
        private const string DisappearAnim = "Disappear";

        public Colony Colony => colony;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (visualRoot != null)
                visualRestingScale = visualRoot.localScale;
        }

        private void OnEnable()
        {
            if (clickCollider != null)
                ClickTargets[clickCollider] = this;
        }

        private void OnDisable()
        {
            if (clickCollider != null)
                ClickTargets.Remove(clickCollider);
        }

        public void Initialize(Colony model, LevelManager gameplayLevelManager, int ownerColumn)
        {
            if (colony != null)
            {
                colony.CountChanged -= RefreshCount;
                colony.StateChanged -= OnStateChanged;
            }

            colony = model;
            levelManager = gameplayLevelManager;
            columnIndex = ownerColumn;
            targetLocalPosition = transform.localPosition;
            currentMoveSpeed = moveSpeed;
            isMoving = false;
            isDisappearing = false;
            disappearTime = 0f;
            animName = null;
            displayedCount = -1;
            isTrayBouncing = false;
            if (visualRoot != null)
                visualRoot.localScale = visualRestingScale;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            ChangeAnim(IdleAnim);

            if (bodyRenderer == null)
                throw new MissingReferenceException($"{name}: ColonyView requires a serialized Renderer reference.");
            ApplyColor();
            SyncCount();
            colony.CountChanged += RefreshCount;
            colony.StateChanged += OnStateChanged;
        }

        public void MoveToColumnPosition(Vector3 position)
        {
            targetLocalPosition = position;
            currentMoveSpeed = columnReflowSpeed;
            if ((transform.localPosition - targetLocalPosition).sqrMagnitude <= 0.0001f)
                return;

            isMoving = true;
            ChangeAnim(MoveAnim);
        }

        public void MoveToColumnPositionOverDuration(Vector3 position, float duration)
        {
            targetLocalPosition = position;
            float distance = Vector3.Distance(transform.localPosition, targetLocalPosition);
            currentMoveSpeed = distance / Mathf.Max(0.01f, duration);
            if (distance <= 0.0001f)
                return;

            isMoving = true;
            ChangeAnim(MoveAnim);
        }

        public void MoveToTray(Vector3 position)
        {
            targetLocalPosition = position;
            currentMoveSpeed = moveSpeed;
            activateOnArrival = true;
            isMoving = true;
            ChangeAnim(MoveAnim);
        }

        public void MoveWithinTray(Vector3 position)
        {
            targetLocalPosition = position;
            currentMoveSpeed = columnReflowSpeed;
            if ((transform.localPosition - targetLocalPosition).sqrMagnitude <= 0.0001f)
                return;

            isMoving = true;
            ChangeAnim(MoveAnim);
        }

        internal void SetOwnerColumn(int ownerColumn)
        {
            columnIndex = ownerColumn;
        }

        public void PlayDisappear()
        {
            BeginDisappear();
        }

        private void OnDestroy()
        {
            if (colony == null)
                return;
            colony.CountChanged -= RefreshCount;
            colony.StateChanged -= OnStateChanged;
        }

        public static bool TryGetClickTarget(Collider collider, out ColonyView view)
        {
            return ClickTargets.TryGetValue(collider, out view);
        }

        public void HandleClick()
        {
            if (levelManager != null && colony != null && colony.State == ColonyState.InColumn)
                levelManager.HandleColonyClick(this, columnIndex);
        }

        private void Update()
        {
            SyncCount();
            UpdateFeedback();

            if (isDisappearing)
            {
                disappearTime += Time.deltaTime;
                if (disappearTime >= disappearDuration)
                    gameObject.SetActive(false);
                return;
            }

            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, targetLocalPosition, currentMoveSpeed * Time.deltaTime);

            if ((transform.localPosition - targetLocalPosition).sqrMagnitude <= 0.0001f)
            {
                transform.localPosition = targetLocalPosition;
                if (isMoving)
                {
                    isMoving = false;
                    ChangeAnim(IdleAnim);
                }

                if (activateOnArrival)
                {
                    activateOnArrival = false;
                    BeginTrayBounce();
                    colony.Activate();
                    levelManager?.ReevaluateProgress();
                }
            }
        }

        private void RefreshCount(Colony changed)
        {
            if (changed == colony)
                SyncCount();
        }

        private void SyncCount()
        {
            if (colony == null || countText == null)
                return;

            int currentCount = colony.DisplayCount;
            if (currentCount == displayedCount)
                return;

            displayedCount = currentCount;
            countText.SetText("{0}", currentCount);
        }

        private void BeginTrayBounce()
        {
            if (visualRoot == null)
                return;
            trayBounceTime = 0f;
            isTrayBouncing = true;
        }

        private void UpdateFeedback()
        {
            if (isTrayBouncing && visualRoot != null)
            {
                trayBounceTime += Time.deltaTime;
                float progress = Mathf.Clamp01(trayBounceTime / trayBounceDuration);
                float wave = Mathf.Sin(progress * Mathf.PI);
                visualRoot.localScale = visualRestingScale *
                                        Mathf.Lerp(1f, trayBounceScale, wave);
                if (progress >= 1f)
                {
                    visualRoot.localScale = visualRestingScale;
                    isTrayBouncing = false;
                }
            }

        }

        private void OnStateChanged(Colony changed, ColonyState state)
        {
            if (state == ColonyState.Completed)
                BeginDisappear();
        }

        private void BeginDisappear()
        {
            if (isDisappearing)
                return;

            isDisappearing = true;
            isMoving = false;
            disappearTime = 0f;
            activateOnArrival = false;
            SoundManager.Instance?.PlaySfx(SfxId.ColonyDisappear);
            if (clickCollider != null)
                clickCollider.enabled = false;

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                gameObject.SetActive(false);
                return;
            }

            ChangeAnim(DisappearAnim);
        }

        private void ChangeAnim(string anim)
        {
            if (animator == null || string.IsNullOrEmpty(anim) || animName == anim)
                return;

            if (!string.IsNullOrEmpty(animName))
                animator.ResetTrigger(animName);

            animName = anim;
            animator.SetTrigger(animName);
        }

        private void ApplyColor()
        {
            if (bodyRenderer == null || colony == null)
                return;

            Color tint = PixelColorUtility.ToUnityColor(colony.Color);
            Color sideTint = Color.Lerp(tint, Color.black, 0.14f);
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, sideTint);
            propertyBlock.SetColor(ColorId, sideTint);
            bodyRenderer.SetPropertyBlock(propertyBlock);

            if (topRenderer != null)
            {
                topRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, tint);
                propertyBlock.SetColor(ColorId, tint);
                topRenderer.SetPropertyBlock(propertyBlock);
            }

            if (countText != null)
                countText.color = Color.white;
        }
    }
}
