using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasVictory : UICanvas
    {
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Animator victoryEffectAnimator;
        [SerializeField] private VictoryStarFall victoryStarEffect;

        [Header("Confetti")]
        [SerializeField] private GameObject victoryVfxRigPrefab;
        [SerializeField] private RawImage victoryVfxImage;
        [SerializeField, Min(0f)] private float confettiDelay = 0.15f;
        [SerializeField, Range(0f, 90f)] private float confettiLaunchAngle = 65f;
        [SerializeField] private Vector2 confettiLeftViewport = new Vector2(0.1f, 0.3f);
        [SerializeField] private Vector2 confettiRightViewport = new Vector2(0.9f, 0.3f);

        private GameObject victoryVfxRig;
        private Camera victoryVfxCamera;
        private ParticleSystem leftConfetti;
        private ParticleSystem rightConfetti;
        private RenderTexture victoryVfxTexture;
        private Coroutine confettiRoutine;
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private bool updatingVfxLayout;

        public override void Setup()
        {
            if (levelText != null && GameManager.Instance != null)
                levelText.text = $"Level {GameManager.Instance.DisplayLevelNumber} Complete";
        }

        public override void Open()
        {
            base.Open();
            RestartVictoryEffect();
        }

        public override void CloseDirectly()
        {
            ResetVictoryEffect();
            base.CloseDirectly();
        }

        public void NextLevelButton()
        {
            GameManager.Instance?.NextLevel();
        }

        public void ReplayButton()
        {
            GameManager.Instance?.ReplayLevel();
        }

        public void MainMenuButton()
        {
            GameManager.Instance?.GoToMainMenu();
        }

        private void RestartVictoryEffect()
        {
            RestartAnimator(victoryEffectAnimator);
            victoryStarEffect?.Play();
            StopConfetti();
            if (EnsureConfettiRig())
                confettiRoutine = StartCoroutine(PlayConfetti());
        }

        private void ResetVictoryEffect()
        {
            ResetAnimator(victoryEffectAnimator);
            victoryStarEffect?.StopAndClear();
            StopConfetti();
        }

        private bool EnsureConfettiRig()
        {
            if (victoryVfxRigPrefab == null || victoryVfxImage == null)
                return false;
            if (victoryVfxRig != null)
                return true;

            // The capture stage is world-space, not a child of the Overlay Canvas.
            victoryVfxRig = Instantiate(victoryVfxRigPrefab);
            victoryVfxCamera = victoryVfxRig.GetComponentInChildren<Camera>(true);
            leftConfetti = victoryVfxRig.transform.Find("VictoryFirework_Left").GetComponent<ParticleSystem>();
            rightConfetti = victoryVfxRig.transform.Find("VictoryFirework_Right").GetComponent<ParticleSystem>();
            victoryVfxCamera.enabled = false;
            return true;
        }

        private IEnumerator PlayConfetti()
        {
            yield return new WaitForSecondsRealtime(confettiDelay);
            Canvas.ForceUpdateCanvases();
            UpdateVfxLayout();
            ClearVfxTexture();
            victoryVfxImage.gameObject.SetActive(true);
            victoryVfxCamera.enabled = true;
            leftConfetti.Play(true);
            rightConfetti.Play(true);

            // Only the short celebration renders an extra camera. Stars are independent.
            do { yield return null; }
            while (leftConfetti.IsAlive(true) || rightConfetti.IsAlive(true));
            HideConfetti();
            confettiRoutine = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying && victoryVfxRig != null && gameObject.activeInHierarchy)
                UpdateVfxLayout();
        }

        private void UpdateVfxLayout()
        {
            if (updatingVfxLayout || victoryVfxCamera == null || victoryVfxImage == null)
                return;
            updatingVfxLayout = true;
            try
            {
                var canvasRect = (RectTransform)transform;
                var imageRect = victoryVfxImage.rectTransform;
                var imageParent = (RectTransform)imageRect.parent;
                canvasRect.GetWorldCorners(canvasCorners);
                Vector3 bottomLeft = imageParent.InverseTransformPoint(canvasCorners[0]);
                Vector3 topRight = imageParent.InverseTransformPoint(canvasCorners[2]);
                imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.localScale = Vector3.one;
                imageRect.sizeDelta = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
                Vector2 center = (bottomLeft + topRight) * 0.5f;
                Vector2 anchorPosition = center - imageParent.rect.center;
                imageRect.anchoredPosition3D = new Vector3(anchorPosition.x, anchorPosition.y, 0f);

                float aspect = canvasRect.rect.width / Mathf.Max(1f, canvasRect.rect.height);
                var ownerCanvas = GetComponentInParent<Canvas>();
                float pixelHeight = ownerCanvas != null ? ownerCanvas.rootCanvas.pixelRect.height : Screen.height;
                int height = Mathf.Clamp(Mathf.RoundToInt(pixelHeight), 16, 1024);
                int width = Mathf.Max(16, Mathf.RoundToInt(height * aspect));
                if (width > 1024)
                {
                    width = 1024;
                    height = Mathf.Max(16, Mathf.RoundToInt(width / aspect));
                }
                if (victoryVfxTexture == null || victoryVfxTexture.width != width || victoryVfxTexture.height != height)
                {
                    ReleaseVfxTexture();
                    victoryVfxTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                    {
                        name = "Victory Confetti Capture",
                        antiAliasing = 1,
                        useMipMap = false,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    victoryVfxTexture.Create();
                    victoryVfxCamera.targetTexture = victoryVfxTexture;
                    victoryVfxImage.texture = victoryVfxTexture;
                    ClearVfxTexture();
                }
                victoryVfxCamera.aspect = aspect;
                PositionConfetti(leftConfetti, confettiLeftViewport, false);
                PositionConfetti(rightConfetti, confettiRightViewport, true);
            }
            finally { updatingVfxLayout = false; }
        }

        private void PositionConfetti(ParticleSystem particles, Vector2 viewport, bool mirrored)
        {
            float depth = Vector3.Dot(victoryVfxRig.transform.position - victoryVfxCamera.transform.position,
                victoryVfxCamera.transform.forward);
            particles.transform.position = victoryVfxCamera.ViewportToWorldPoint(
                new Vector3(viewport.x, viewport.y, depth));
            float angle = confettiLaunchAngle * Mathf.Deg2Rad;
            Vector3 direction = victoryVfxCamera.transform.up * Mathf.Sin(angle) +
                                victoryVfxCamera.transform.right * (mirrored ? -Mathf.Cos(angle) : Mathf.Cos(angle));
            particles.transform.rotation = Quaternion.LookRotation(direction, victoryVfxCamera.transform.forward);
        }

        private void StopConfetti()
        {
            if (confettiRoutine != null)
            {
                StopCoroutine(confettiRoutine);
                confettiRoutine = null;
            }
            if (leftConfetti != null)
                leftConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (rightConfetti != null)
                rightConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            HideConfetti();
        }

        private void HideConfetti()
        {
            if (victoryVfxCamera != null)
                victoryVfxCamera.enabled = false;
            if (victoryVfxImage != null)
                victoryVfxImage.gameObject.SetActive(false);
            ClearVfxTexture();
        }

        private void ClearVfxTexture()
        {
            if (victoryVfxTexture == null || !victoryVfxTexture.IsCreated())
                return;
            var previous = RenderTexture.active;
            RenderTexture.active = victoryVfxTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void ReleaseVfxTexture()
        {
            if (victoryVfxCamera != null)
                victoryVfxCamera.targetTexture = null;
            if (victoryVfxImage != null)
                victoryVfxImage.texture = null;
            if (victoryVfxTexture == null)
                return;
            victoryVfxTexture.Release();
            Destroy(victoryVfxTexture);
            victoryVfxTexture = null;
        }

        private void OnDisable()
        {
            StopConfetti();
        }

        private void OnDestroy()
        {
            StopConfetti();
            ReleaseVfxTexture();
            if (victoryVfxRig != null)
                Destroy(victoryVfxRig);
        }

        private static void RestartAnimator(Animator target)
        {
            if (target == null)
                return;

            target.enabled = true;
            target.Rebind();
            target.Update(0f);
        }

        private static void ResetAnimator(Animator target)
        {
            if (target == null)
                return;

            target.Rebind();
            target.Update(0f);
            target.enabled = false;
        }
    }
}
