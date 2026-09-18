using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasLoading : UICanvas
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField, Min(0.01f)] private float duration = 1f;

        public void SetProgress(float progress)
        {
            float value = Mathf.Clamp01(progress);
            if (slider != null)
                slider.value = value;
            if (text != null)
                text.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
