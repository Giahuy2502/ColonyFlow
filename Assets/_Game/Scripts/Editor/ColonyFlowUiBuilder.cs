#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class ColonyFlowUiBuilder
    {
        private const string VersionName = "Colony Flow UI v7";
        private static readonly Color Navy = new Color(0.12f, 0.22f, 0.38f, 0.97f);
        private static readonly Color Cream = new Color(1f, 0.93f, 0.78f, 1f);
        private static readonly Color Orange = new Color(1f, 0.63f, 0.20f, 1f);
        private static readonly Color Blue = new Color(0.20f, 0.58f, 0.95f, 1f);
        private static readonly Color Purple = new Color(0.66f, 0.31f, 0.84f, 0.96f);
        private static readonly Color Brown = new Color(0.25f, 0.15f, 0.08f, 1f);
        private static readonly Color Overlay = new Color(0.05f, 0.08f, 0.14f, 0.72f);

        static ColonyFlowUiBuilder()
        {
            EditorApplication.delayCall += BuildIfNeeded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += BuildIfNeeded;
        }

        [MenuItem("Colony Flow/Rebuild Gameplay UI")]
        private static void RebuildAll() => Build(true);

        private static void BuildIfNeeded() => Build(false);

        private static void Build(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string gameplayPath = "Assets/_Game/Resources/UI/Canvas-GamePlay.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPath);
            if (!force && existing != null && existing.transform.Find(VersionName) != null)
                return;

            BuildGameplay(gameplayPath);
            BuildVictory("Assets/_Game/Resources/UI/Canvas-Victory.prefab");
            BuildFail("Assets/_Game/Resources/UI/Canvas-Lose.prefab");
            BuildSettings("Assets/_Game/Resources/UI/Canvas-Setting.prefab");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildGameplay(string path)
        {
            GameObject root = CreateCanvas("Canvas-GamePlay");
            CanvasGamePlay controller = root.AddComponent<CanvasGamePlay>();
            RectTransform safe = CreateRect(VersionName, root.transform);
            Stretch(safe);
            SetSafeArea(controller, safe);

            RectTransform top = CreateRect("Top Bar", safe);
            SetRect(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -82f), new Vector2(1010f, 120f));

            Button pause = CreateButton("Pause", top, new Vector2(-438f, 0f), new Vector2(84f, 84f),
                "Ⅱ", Color.white, Purple);
            UnityEventTools.AddPersistentListener(pause.onClick, controller.SettingButton);

            TextMeshProUGUI level = CreateText("Level", top, "Level 1", 46, Brown);
            SetRect(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(450f, 90f));

            Button speed = CreateButton("Speed", top, new Vector2(420f, 0f), new Vector2(150f, 82f),
                "1x", Color.white, new Color(0.48f, 0.53f, 0.56f, 1f));
            UnityEventTools.AddPersistentListener(speed.onClick, controller.SpeedButton);

            RectTransform boosters = CreateRoundedPanel("Boosters", safe, Purple, 54f);
            SetRect(boosters, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 78f), new Vector2(1080f, 156f));
            string[] icons = { "+", "↻", "↓", "✦" };
            string[] names = { "ADD_TRAY", "SHUFFLE", "PICK", "CLEAR_COLOR" };
            var boosterButtons = new Button[icons.Length];
            var boosterCounts = new TextMeshProUGUI[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                float x = (i - 1.5f) * 225f;
                Button button = CreateButton(names[i], boosters, new Vector2(x, 28f),
                    new Vector2(132f, 132f), icons[i], Brown, Cream);
                TextMeshProUGUI count = CreateText("Count", button.transform,
                    i == 0 ? "1" : "∞", 23, Color.white);
                count.alignment = TextAlignmentOptions.Center;
                SetRect(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-8f, 8f), new Vector2(38f, 38f));
                boosterButtons[i] = button;
                boosterCounts[i] = count;
            }

            TextMeshProUGUI boosterPrompt = CreateText(
                "Booster Prompt", safe, string.Empty, 26, Brown);
            SetRect(boosterPrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 178f), new Vector2(760f, 48f));
            boosterPrompt.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("aliveText").objectReferenceValue = level;
            serialized.FindProperty("speedText").objectReferenceValue = speed.GetComponentInChildren<TextMeshProUGUI>();
            serialized.FindProperty("addTrayButton").objectReferenceValue = boosterButtons[0];
            serialized.FindProperty("shuffleButton").objectReferenceValue = boosterButtons[1];
            serialized.FindProperty("pickHiddenButton").objectReferenceValue = boosterButtons[2];
            serialized.FindProperty("removeColorButton").objectReferenceValue = boosterButtons[3];
            serialized.FindProperty("addTrayCountText").objectReferenceValue = boosterCounts[0];
            serialized.FindProperty("shuffleCountText").objectReferenceValue = boosterCounts[1];
            serialized.FindProperty("pickHiddenCountText").objectReferenceValue = boosterCounts[2];
            serialized.FindProperty("removeColorCountText").objectReferenceValue = boosterCounts[3];
            serialized.FindProperty("boosterPromptText").objectReferenceValue = boosterPrompt;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        private static void BuildVictory(string path)
        {
            GameObject root = CreateCanvas("Canvas-Victory");
            CanvasVictory controller = root.AddComponent<CanvasVictory>();
            RectTransform safe = CreateModalBase(root, controller, "LEVEL COMPLETE!", out TextMeshProUGUI title);
            Button next = CreateButton("Next", safe, new Vector2(0f, -120f), new Vector2(560f, 120f),
                "NEXT LEVEL", Color.white, Orange);
            Button replay = CreateButton("Replay", safe, new Vector2(0f, -275f), new Vector2(420f, 96f),
                "REPLAY", Navy, Cream);
            UnityEventTools.AddPersistentListener(next.onClick, controller.NextLevelButton);
            UnityEventTools.AddPersistentListener(replay.onClick, controller.ReplayButton);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("levelText").objectReferenceValue = title;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        private static void BuildFail(string path)
        {
            GameObject root = CreateCanvas("Canvas-Lose");
            CanvasFail controller = root.AddComponent<CanvasFail>();
            RectTransform safe = CreateModalBase(root, controller, "OUT OF MOVES", out _);
            Button retry = CreateButton("Retry", safe, new Vector2(0f, -120f), new Vector2(560f, 120f),
                "TRY AGAIN", Color.white, Orange);
            Button home = CreateButton("Home", safe, new Vector2(0f, -275f), new Vector2(420f, 96f),
                "MAIN MENU", Navy, Cream);
            UnityEventTools.AddPersistentListener(retry.onClick, controller.RetryButton);
            UnityEventTools.AddPersistentListener(home.onClick, controller.MainMenuButton);
            Save(root, path);
        }

        private static void BuildSettings(string path)
        {
            GameObject root = CreateCanvas("Canvas-Setting");
            CanvasSettings controller = root.AddComponent<CanvasSettings>();
            RectTransform safe = CreateModalBase(root, controller, "PAUSED", out _);
            RectTransform buttons = CreateRect("Gameplay Buttons", safe);
            SetRect(buttons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -130f), new Vector2(620f, 430f));
            Button resume = CreateButton("Continue", buttons, new Vector2(0f, 120f), new Vector2(560f, 110f),
                "CONTINUE", Color.white, Orange);
            Button retry = CreateButton("Retry", buttons, new Vector2(0f, -15f), new Vector2(500f, 96f),
                "RESTART", Color.white, Blue);
            Button home = CreateButton("Home", buttons, new Vector2(0f, -140f), new Vector2(420f, 90f),
                "MAIN MENU", Navy, Cream);
            UnityEventTools.AddPersistentListener(resume.onClick, controller.ContinueButton);
            UnityEventTools.AddPersistentListener(retry.onClick, controller.RetryButton);
            UnityEventTools.AddPersistentListener(home.onClick, controller.MainMenuButton);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("gameplayButtons").objectReferenceValue = buttons.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        private static RectTransform CreateModalBase(GameObject root, UICanvas controller,
            string heading, out TextMeshProUGUI title)
        {
            RectTransform safe = CreateRect(VersionName, root.transform);
            Stretch(safe);
            SetSafeArea(controller, safe);
            RectTransform dim = CreatePanel("Dim", safe, Overlay);
            Stretch(dim);
            RectTransform card = CreateRoundedPanel("Card", safe,
                new Color(1f, 0.86f, 0.63f, 1f), 58f);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 760f));
            title = CreateText("Title", card, heading, 62, Navy);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(650f, 120f));
            return card;
        }

        private static GameObject CreateCanvas(string name)
        {
            // Screen prefabs are children of Canvas-Main, so the root only needs a
            // RectTransform. Canvas-Main owns scaling and raycasting for all screens.
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRoundedPanel(string name, Transform parent,
            Color color, float radius)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            RoundedFrameGraphic graphic = rect.gameObject.AddComponent<RoundedFrameGraphic>();
            graphic.color = color;
            SerializedObject serialized = new SerializedObject(graphic);
            serialized.FindProperty("cornerRadius").floatValue = radius;
            serialized.FindProperty("filled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return rect;
        }

        private static RoundedFrameGraphic CreateFrame(string name, Transform parent, Color color,
            float radius, float thickness)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            RoundedFrameGraphic frame = rect.gameObject.AddComponent<RoundedFrameGraphic>();
            frame.color = color;
            SerializedObject serialized = new SerializedObject(frame);
            serialized.FindProperty("cornerRadius").floatValue = radius;
            serialized.FindProperty("thickness").floatValue = thickness;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return frame;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 position,
            Vector2 size, string label, Color textColor, Color background)
        {
            float radius = Mathf.Min(size.x, size.y) * 0.28f;
            RectTransform shadow = CreateRoundedPanel(name + " Shadow", parent,
                new Color(0.18f, 0.13f, 0.16f, 0.34f), radius);
            SetRect(shadow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position + new Vector2(0f, -7f), size);
            shadow.GetComponent<RoundedFrameGraphic>().raycastTarget = false;

            RectTransform rect = CreateRoundedPanel(name, parent, background, radius);
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<RoundedFrameGraphic>();

            RoundedFrameGraphic outline = CreateFrame("Outline", rect,
                new Color(1f, 1f, 1f, 0.82f), radius, 5f);
            Stretch(outline.rectTransform);
            outline.raycastTarget = false;
            TextMeshProUGUI text = CreateText("Label", rect, label, 34, textColor);
            Stretch(text.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value,
            float size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetSafeArea(UICanvas controller, RectTransform safe)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("safeAreaRoot").objectReferenceValue = safe;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
