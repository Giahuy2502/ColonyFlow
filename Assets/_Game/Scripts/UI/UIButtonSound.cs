using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class UIButtonSound : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(PlayClick);
        }

        private static void PlayClick()
        {
            SoundManager.Instance?.PlaySfx(SfxId.UiClick);
        }
    }
}
