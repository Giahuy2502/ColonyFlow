using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class ColonyView : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Renderer topRenderer;
        [SerializeField] private TextMesh countText;
        [SerializeField] private Renderer countRenderer;
        [SerializeField] private Collider clickCollider;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;
        [SerializeField, Min(0.1f)] private float columnReflowSpeed = 4f;
        [SerializeField, Min(0.01f)] private float disappearDuration = 0.32f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;
        [SerializeField] private Colony colony;
        private LevelManager levelManager;
        private int columnIndex;
        private Vector3 targetLocalPosition;
        private float currentMoveSpeed;
        private Camera mainCamera;
        private bool activateOnArrival;
        private bool isMoving;
        private bool isDisappearing;
        private float disappearTime;
        private string animName;
        private static readonly Dictionary<Collider, ColonyView> ClickTargets = new Dictionary<Collider, ColonyView>();
        private static Font runtimeFont;

        private const string IdleAnim = "Idle";
        private const string MoveAnim = "Move";
        private const string DisappearAnim = "Disappear";

        public Colony Colony => colony;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (countText != null && countRenderer != null)
            {
                if (runtimeFont == null)
                    runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (runtimeFont != null)
                {
                    countText.font = runtimeFont;
                    countRenderer.sharedMaterial = runtimeFont.material;
                }
            }
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
            colony = model;
            levelManager = gameplayLevelManager;
            columnIndex = ownerColumn;
            targetLocalPosition = transform.localPosition;
            currentMoveSpeed = moveSpeed;
            mainCamera = Camera.main;
            isMoving = false;
            isDisappearing = false;
            disappearTime = 0f;
            animName = null;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            ChangeAnim(IdleAnim);

            if (bodyRenderer == null)
                throw new MissingReferenceException($"{name}: ColonyView requires a serialized Renderer reference.");
            ApplyColor();
            RefreshCount(model);
            colony.CountChanged += RefreshCount;
            colony.StateChanged += OnStateChanged;
        }

        public void SetTargetLocalPosition(Vector3 position, bool immediate = false)
        {
            targetLocalPosition = position;
            currentMoveSpeed = moveSpeed;
            if (immediate)
            {
                transform.localPosition = position;
                isMoving = false;
                ChangeAnim(IdleAnim);
            }
            else if ((transform.localPosition - targetLocalPosition).sqrMagnitude > 0.0001f)
            {
                isMoving = true;
                ChangeAnim(MoveAnim);
            }
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
                    colony.Activate();
                }
            }
        }

        // private void LateUpdate()
        // {
        //     if (countText == null)
        //         return;
        //     if (mainCamera == null)
        //         mainCamera = Camera.main;
        //     if (mainCamera != null)
        //         countText.transform.rotation = Quaternion.LookRotation(
        //             countText.transform.position - mainCamera.transform.position,
        //             mainCamera.transform.up);
        // }

        private void RefreshCount(Colony changed)
        {
            if (countText != null)
                countText.text = changed.DisplayCount.ToString();
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
            {
                bool useDarkText = colony.Color == PixelColor.White ||
                    colony.Color == PixelColor.Yellow || colony.Color == PixelColor.Cyan;
                countText.color = useDarkText
                    ? new Color(0.16f, 0.12f, 0.10f, 1f)
                    : Color.white;
            }
        }
    }
}
