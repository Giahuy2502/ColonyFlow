using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public class UICanvas : MonoBehaviour
    {
        [SerializeField] private bool isDestroyOnClose;

        protected virtual void Awake()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].GetComponent<UIButtonSound>() == null)
                    buttons[i].gameObject.AddComponent<UIButtonSound>();
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

    }
}
