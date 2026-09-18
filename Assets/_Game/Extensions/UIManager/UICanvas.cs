using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public class UICanvas : MonoBehaviour
    {
        [SerializeField] private bool isDestroyOnClose;
        [SerializeField] private RectTransform safeAreaRoot;

        protected virtual void Awake()
        {
            if (safeAreaRoot == null)
                safeAreaRoot = transform as RectTransform;
            ApplySafeArea();
        }

        public virtual void Setup() { }

        public virtual void Open()
        {
            CancelInvoke(nameof(CloseDirectly));
            gameObject.SetActive(true);
        }

        public virtual void Close(float delay)
        {
            if (delay <= 0f)
                CloseDirectly();
            else
                Invoke(nameof(CloseDirectly), delay);
        }

        public virtual void CloseDirectly()
        {
            if (isDestroyOnClose)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safeArea = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(
                safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
        }
    }
}
