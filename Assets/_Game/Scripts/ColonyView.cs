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
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;
        [SerializeField] private Colony colony;
        private LevelManager levelManager;
        private int columnIndex;
        private Vector3 targetLocalPosition;
        private Camera mainCamera;
        private bool activateOnArrival;
        private static readonly Dictionary<Collider, ColonyView> ClickTargets = new Dictionary<Collider, ColonyView>();
        private static Font runtimeFont;

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
            mainCamera = Camera.main;

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
            if (immediate)
                transform.localPosition = position;
        }

        public void MoveToTray(Vector3 position)
        {
            targetLocalPosition = position;
            activateOnArrival = true;
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
                levelManager.SelectColumn(columnIndex);
        }

        private void Update()
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, targetLocalPosition, moveSpeed * Time.deltaTime);

            if (activateOnArrival &&
                (transform.localPosition - targetLocalPosition).sqrMagnitude <= 0.0001f)
            {
                transform.localPosition = targetLocalPosition;
                activateOnArrival = false;
                colony.Activate();
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
                gameObject.SetActive(false);
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
