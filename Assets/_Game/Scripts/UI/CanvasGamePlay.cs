using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    public sealed class CanvasGamePlay : UICanvas
    {
        [SerializeField] private TextMeshProUGUI aliveText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private LoseNotification loseNotification;
        [Header("Boosters")]
        [SerializeField] private Button addTrayButton;
        [SerializeField] private Button shuffleButton;
        [SerializeField] private Button pickHiddenButton;
        [SerializeField] private Button removeColorButton;
        [SerializeField] private TextMeshProUGUI addTrayCountText;
        [SerializeField] private TextMeshProUGUI shuffleCountText;
        [SerializeField] private TextMeshProUGUI pickHiddenCountText;
        [SerializeField] private TextMeshProUGUI removeColorCountText;
        [SerializeField] private TextMeshProUGUI boosterPromptText;

        private LevelManager levelManager;
        private BoosterManager boosterManager;
        private bool doubleSpeed;

        private static readonly Color BoosterNormal = new Color(1f, 0.93f, 0.78f, 1f);
        private static readonly Color BoosterSelected = new Color(1f, 0.72f, 0.24f, 1f);

        public override void Setup()
        {
            if (aliveText != null && GameManager.Instance != null)
                aliveText.text = $"Level {GameManager.Instance.DisplayLevelNumber}";
            if (loseNotification != null)
            {
                loseNotification.Setup();
                loseNotification.gameObject.SetActive(false);
            }
            doubleSpeed = GameManager.Instance != null && GameManager.Instance.IsDoubleSpeed;
            RefreshSpeedText();
            ResolveBoosterReferences();
            WireBoosterButtons();
            if (boosterManager != null)
            {
                boosterManager.StateChanged -= RefreshBoosters;
                boosterManager.StateChanged += RefreshBoosters;
            }
            RefreshBoosters();
        }

        private void OnDisable()
        {
            if (boosterManager != null)
                boosterManager.StateChanged -= RefreshBoosters;
        }

        public void SettingButton()
        {
            GameManager.Instance?.OpenSettings();
        }

        public void SpeedButton()
        {
            GameManager.Instance?.ToggleGameSpeed();
            doubleSpeed = GameManager.Instance != null && GameManager.Instance.IsDoubleSpeed;
            RefreshSpeedText();
        }

        public void AddTrayBoosterButton()
        {
            boosterManager?.UseAddTrayBooster();
            RefreshBoosters();
        }

        public void ShuffleBoosterButton()
        {
            boosterManager?.UseShuffleBooster();
            RefreshBoosters();
        }

        public void PickHiddenBoosterButton()
        {
            boosterManager?.UsePickHiddenColonyBooster();
            RefreshBoosters();
        }

        public void RemoveColorBoosterButton()
        {
            boosterManager?.UseRemoveColorBooster();
            RefreshBoosters();
        }

        private void RefreshSpeedText()
        {
            if (speedText != null)
                speedText.text = doubleSpeed ? "2x" : "1x";
        }

        private void ResolveBoosterReferences()
        {
            levelManager = LevelManager.Instance;
            boosterManager = levelManager != null ? levelManager.BoosterManager : null;
            addTrayButton ??= FindButton("ADD_TRAY", "UNDO");
            shuffleButton ??= FindButton("SHUFFLE");
            pickHiddenButton ??= FindButton("PICK", "MAGNET");
            removeColorButton ??= FindButton("CLEAR_COLOR", "PAINT");

            if (addTrayButton != null) addTrayButton.name = "ADD_TRAY";
            if (pickHiddenButton != null) pickHiddenButton.name = "PICK";
            if (removeColorButton != null) removeColorButton.name = "CLEAR_COLOR";

            addTrayCountText ??= FindCount(addTrayButton);
            shuffleCountText ??= FindCount(shuffleButton);
            pickHiddenCountText ??= FindCount(pickHiddenButton);
            removeColorCountText ??= FindCount(removeColorButton);
            EnsureBoosterPrompt();
        }

        private void WireBoosterButtons()
        {
            Wire(addTrayButton, AddTrayBoosterButton);
            Wire(shuffleButton, ShuffleBoosterButton);
            Wire(pickHiddenButton, PickHiddenBoosterButton);
            Wire(removeColorButton, RemoveColorBoosterButton);
        }

        private void RefreshBoosters()
        {
            BoosterTargetMode mode = boosterManager != null
                ? boosterManager.TargetMode
                : BoosterTargetMode.None;

            if (addTrayCountText != null)
                addTrayCountText.text = boosterManager != null &&
                                        boosterManager.CanUseAddTray ? "1" : "0";
            if (shuffleCountText != null) shuffleCountText.text = "∞";
            if (pickHiddenCountText != null) pickHiddenCountText.text = "∞";
            if (removeColorCountText != null) removeColorCountText.text = "∞";

            if (addTrayButton != null)
                addTrayButton.interactable = boosterManager != null &&
                                             boosterManager.CanUseAddTray;
            SetSelected(pickHiddenButton, mode == BoosterTargetMode.PickHiddenColony);
            SetSelected(removeColorButton, mode == BoosterTargetMode.RemoveColor);

            if (boosterPromptText != null)
            {
                boosterPromptText.gameObject.SetActive(mode != BoosterTargetMode.None);
                boosterPromptText.text = mode == BoosterTargetMode.PickHiddenColony
                    ? "Chọn một Colony phía dưới"
                    : mode == BoosterTargetMode.RemoveColor
                        ? "Chọn một Pixel trên Board"
                        : string.Empty;
            }
        }

        private Button FindButton(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                    if (buttons[i].name == names[nameIndex])
                        return buttons[i];
            return null;
        }

        private static TextMeshProUGUI FindCount(Button button)
        {
            if (button == null)
                return null;
            Transform count = button.transform.Find("Count");
            return count != null ? count.GetComponent<TextMeshProUGUI>() : null;
        }

        private void EnsureBoosterPrompt()
        {
            if (boosterPromptText != null || addTrayButton == null ||
                addTrayButton.transform.parent == null ||
                addTrayButton.transform.parent.parent == null)
                return;

            var promptObject = new GameObject(
                "Booster Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = promptObject.GetComponent<RectTransform>();
            rect.SetParent(addTrayButton.transform.parent.parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 178f);
            rect.sizeDelta = new Vector2(760f, 48f);

            boosterPromptText = promptObject.GetComponent<TextMeshProUGUI>();
            boosterPromptText.alignment = TextAlignmentOptions.Center;
            boosterPromptText.fontSize = 26f;
            boosterPromptText.fontStyle = FontStyles.Bold;
            boosterPromptText.color = new Color(0.25f, 0.15f, 0.08f, 1f);
            if (aliveText != null)
                boosterPromptText.font = aliveText.font;
            promptObject.SetActive(false);
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? BoosterSelected : BoosterNormal;
        }
    }
}
