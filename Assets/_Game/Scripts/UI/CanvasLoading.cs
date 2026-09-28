using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasLoading : UICanvas
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField, Min(0.01f)] private float duration = 1f;

        public float Duration => duration;

        public override void Setup()
        {
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            float value = Mathf.Clamp01(progress);
            if (slider != null)
                slider.value = value;
            if (fillImage != null)
                fillImage.fillAmount = value;
            if (text != null)
                text.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
