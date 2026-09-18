using TMPro;
using UnityEngine;

namespace ColonyFlow
{
    // Economy has not been added to Colony Flow yet. This canvas remains reusable
    // and deliberately contains no dependencies on the previous game's item types.
    public sealed class CanvasShop : UICanvas
    {
        [SerializeField] private TextMeshProUGUI statusText;

        public override void Setup()
        {
            if (statusText != null)
                statusText.text = "Coming Soon";
        }

        public void BackButton()
        {
            Close(0f);
        }
    }
}
