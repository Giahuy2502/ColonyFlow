using UnityEngine;

namespace ColonyFlow
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GameplayBackground : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField, Min(0.01f)] private float distanceFromCamera;
        [SerializeField] private Vector2 viewportPosition;
        [SerializeField, Min(1f)] private float coverScaleMultiplier;

        private GameManager subscribedGameManager;

        private void OnEnable()
        {
            ApplyLayout();

            if (Application.isPlaying)
                SubscribeToGameState();
            else
                SetRendererVisible(true);
        }

        private void LateUpdate()
        {
            ApplyLayout();

            if (!Application.isPlaying)
            {
                SetRendererVisible(true);
                return;
            }

            if (subscribedGameManager == null)
                SubscribeToGameState();
        }

        private void OnValidate()
        {
            ApplyLayout();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameState();
        }

        private void SubscribeToGameState()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == subscribedGameManager)
                return;

            UnsubscribeFromGameState();
            subscribedGameManager = gameManager;

            if (subscribedGameManager == null)
            {
                SetRendererVisible(false);
                return;
            }

            subscribedGameManager.StateChanged += OnGameStateChanged;
            OnGameStateChanged(subscribedGameManager.State);
        }

        private void UnsubscribeFromGameState()
        {
            if (subscribedGameManager != null)
                subscribedGameManager.StateChanged -= OnGameStateChanged;
            subscribedGameManager = null;
        }

        private void OnGameStateChanged(GameState state)
        {
            bool isLevelVisible = state == GameState.Playing ||
                                  state == GameState.Paused ||
                                  state == GameState.Victory ||
                                  state == GameState.Failed;
            SetRendererVisible(isLevelVisible);
        }

        private void SetRendererVisible(bool visible)
        {
            if (targetRenderer != null && targetRenderer.enabled != visible)
                targetRenderer.enabled = visible;
        }

        private void ApplyLayout()
        {
            if (targetCamera == null || targetRenderer == null || targetRenderer.sprite == null)
                return;

            float viewportHeight;
            if (targetCamera.orthographic)
            {
                viewportHeight = targetCamera.orthographicSize * 2f;
            }
            else
            {
                float halfFieldOfView = targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                viewportHeight = 2f * distanceFromCamera * Mathf.Tan(halfFieldOfView);
            }

            float viewportWidth = viewportHeight * targetCamera.aspect;
            Vector2 spriteSize = targetRenderer.sprite.bounds.size;
            if (spriteSize.x <= Mathf.Epsilon || spriteSize.y <= Mathf.Epsilon)
                return;

            float scale = Mathf.Max(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);
            scale *= coverScaleMultiplier;

            Vector3 targetPosition = targetCamera.ViewportToWorldPoint(
                new Vector3(viewportPosition.x, viewportPosition.y, distanceFromCamera));
            Quaternion targetRotation = targetCamera.transform.rotation;
            Vector3 targetScale = Vector3.one * scale;

            if ((transform.position - targetPosition).sqrMagnitude > Mathf.Epsilon ||
                Quaternion.Angle(transform.rotation, targetRotation) > Mathf.Epsilon)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
            }

            if ((transform.localScale - targetScale).sqrMagnitude > Mathf.Epsilon)
                transform.localScale = targetScale;
        }
    }
}
