#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class MainMenuCutoutRigBuilder
    {
        private const string Folder = "Assets/_Game/Animations/MainMenu/";
        private const string PrefabPath = Folder + "MainMenuAnt_Cutout.prefab";
        private const string ClipPath = Folder + "MainMenuAnt_CutoutIdle.anim";
        private const string ControllerPath = Folder + "MainMenuAnt_Cutout.controller";
        private const string MenuPath = "Assets/_Game/Resources/UI/Canvas-MainMenu.prefab";

        static MainMenuCutoutRigBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Build();
            if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && !SessionState.GetBool("CutoutRigValidatedV1", false))
            {
                Validate();
                SessionState.SetBool("CutoutRigValidatedV1", true);
            }
        }

        [MenuItem("Colony Flow/Main Menu/Rebuild Cutout Ant")]
        private static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorUtility.DisplayDialog("Rebuild cutout ant", "Rebuild only the cutout ant prefab/animation and reconnect it to Main Menu? Other UI is preserved.", "Rebuild", "Cancel")) Build();
        }

        private static void Build()
        {
            GameObject root = new GameObject("MainMenuAnt_Cutout", typeof(RectTransform), typeof(Canvas), typeof(Animator));
            root.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(476.83f, 730.2181f);
                root.layer = 5;
                Part("Panel", rect, "panel", new Vector2(0f, 190f), 385f, Vector2.one * .5f);
                RectTransform rig = Pivot("AntRigRoot", rect, Vector2.zero);
                // Feet remain independent of the upper-body breathing pivot.
                RectTransform leftLeg = Pivot("Leg_L_Pivot", rig, new Vector2(-68f, -217f));
                Part("Leg_L", leftLeg, "leg_r", Vector2.zero, 95f, new Vector2(.83f, .96f));
                RectTransform rightLeg = Pivot("Leg_R_Pivot", rig, new Vector2(0f, -218f));
                Part("Leg_R", rightLeg, "leg_l", Vector2.zero, 100f, new Vector2(.18f, .96f));
                RectTransform upper = Pivot("UpperBodyPivot", rig, new Vector2(-60f, -245f));
                RectTransform other = Pivot("Arm_Other_Pivot", upper, new Vector2(125f, 111f));
                RectTransform otherImage = Part("Arm_Other", other, "arm_other", Vector2.zero, 75f, new Vector2(.87f, .14f));
                otherImage.localRotation = Quaternion.Euler(0f, 0f, 180f);
                Part("Body", upper, "body", new Vector2(25f, 73f), 230f, Vector2.one * .5f);
                RectTransform head = Pivot("HeadPivot", upper, new Vector2(86f, 149f));
                Part("Head", head, "head", Vector2.zero, 250f, new Vector2(.44f, .10f));
                RectTransform antennaL = Pivot("Antenna_L_Pivot", head, new Vector2(-28f, 182f));
                RectTransform antennaLImage = Part("Antenna_L", antennaL, "antenna_l", Vector2.zero, 112.45f, new Vector2(.13f, .13f));
                antennaLImage.localScale = new Vector3(-1f, 1f, 1f);
                RectTransform antennaR = Pivot("Antenna_R_Pivot", head, new Vector2(64f, 181f));
                Part("Antenna_R", antennaR, "antenna_r", Vector2.zero, 121.74f, new Vector2(.14f, .14f));
                RectTransform raised = Pivot("Arm_Raised_Pivot", upper, new Vector2(36f, 140f));
                RectTransform raisedImage = Part("Arm_Raised", raised, "arm_raised", Vector2.zero, 196.3f, new Vector2(.17f, .12f));
                raisedImage.localScale = new Vector3(-1f, 1f, 1f);

                AnimationClip clip = MakeClip();
                AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                if (existingClip != null)
                {
                    EditorUtility.CopySerialized(clip, existingClip);
                    UnityEngine.Object.DestroyImmediate(clip);
                    clip = existingClip;
                }
                else AssetDatabase.CreateAsset(clip, ClipPath);
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                    AnimatorState state = controller.layers[0].stateMachine.AddState("Idle");
                    state.motion = clip;
                    controller.layers[0].stateMachine.defaultState = state;
                }
                else controller.layers[0].stateMachine.defaultState.motion = clip;
                Animator animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.hideFlags = HideFlags.None;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                ConnectMenu(prefab);
                AssetDatabase.SaveAssets();
                RenderPreview(prefab, clip, 0f, "ColonyFlowCutout_0.png");
                RenderPreview(prefab, clip, .55f, "ColonyFlowCutout_055.png");
                RenderPreview(prefab, clip, 1.25f, "ColonyFlowCutout_125.png");
                Debug.Log("MENU_CUTOUT_OK: 9 rigid Images, linked Main Menu Animator, Unscaled Time, 2s looping idle; preview PNGs in " + Path.GetTempPath());
            }
            catch (Exception error) { Debug.LogError("MENU_CUTOUT_FAILED: " + error); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static RectTransform Pivot(string name, Transform parent, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = 5;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static RectTransform Part(string name, Transform parent, string source, Vector2 position, float width, Vector2 pivot)
        {
            Sprite sprite = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Textures/Ant_Mainmenu/menu_ant_" + source + ".png"))
                if (asset is Sprite found) { sprite = found; break; }
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + source);
            RectTransform rect = Pivot(name, parent, position);
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return rect;
        }

        private static AnimationClip MakeClip()
        {
            var clip = new AnimationClip { name = "MainMenuAnt_CutoutIdle", frameRate = 60f, wrapMode = WrapMode.Loop };
            const string upper = "AntRigRoot/UpperBodyPivot";
            Curve(clip, upper, "m_AnchoredPosition.y", -245f, 7f, 0f, true);
            Curve(clip, upper, "localEulerAnglesRaw.z", 0f, .8f, 0f, false);
            Curve(clip, upper + "/HeadPivot", "localEulerAnglesRaw.z", 0f, -1.8f, .18f, false);
            Curve(clip, upper + "/HeadPivot/Antenna_L_Pivot", "localEulerAnglesRaw.z", 0f, 7f, .4f, false);
            Curve(clip, upper + "/HeadPivot/Antenna_R_Pivot", "localEulerAnglesRaw.z", 0f, -5f, 1.1f, false);
            Curve(clip, upper + "/Arm_Raised_Pivot", "localEulerAnglesRaw.z", 0f, 5f, .35f, false);
            Curve(clip, upper + "/Arm_Other_Pivot", "localEulerAnglesRaw.z", 0f, -2f, .8f, false);
            var serialized = new SerializedObject(clip);
            var settings = serialized.FindProperty("m_AnimationClipSettings");
            settings.FindPropertyRelative("m_LoopTime").boolValue = true;
            settings.FindPropertyRelative("m_StopTime").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return clip;
        }

        private static void Curve(AnimationClip clip, string path, string attribute, float basis, float amplitude, float phase, bool bob)
        {
            var keys = new Keyframe[61];
            for (int i = 0; i < keys.Length; i++)
            {
                float time = 2f * i / (keys.Length - 1);
                float angle = i == keys.Length - 1 ? 0f : Mathf.PI * time;
                float value = bob ? basis + amplitude * .5f * (1f - Mathf.Cos(angle)) : basis + amplitude * (Mathf.Sin(angle + phase) - Mathf.Sin(phase));
                float slope = bob ? amplitude * .5f * Mathf.PI * Mathf.Sin(angle) : amplitude * Mathf.PI * Mathf.Cos(angle + phase);
                keys[i] = new Keyframe(time, value, slope, slope);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(RectTransform), attribute), new AnimationCurve(keys));
        }

        private static void ConnectMenu(GameObject prefab)
        {
            GameObject menu = PrefabUtility.LoadPrefabContents(MenuPath);
            try
            {
                Transform imageAnt = null;
                foreach (Transform node in menu.GetComponentsInChildren<Transform>(true))
                    if (node.name == "Image-ant") { imageAnt = node; break; }
                if (imageAnt == null) throw new InvalidOperationException("Main Menu Image-ant root not found.");
                for (int i = imageAnt.childCount - 1; i >= 0; i--)
                {
                    Transform child = imageAnt.GetChild(i);
                    if (child.name == "MainMenuAnt_Cutout") UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                imageAnt.GetComponent<Image>().enabled = false;
                GameObject linked = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.scene);
                var rect = (RectTransform)linked.transform;
                rect.SetParent(imageAnt, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.SetAsFirstSibling();
                var serialized = new SerializedObject(menu.GetComponent<CanvasMainMenu>());
                serialized.FindProperty("mainMenuAntAnimator").objectReferenceValue = linked.GetComponent<Animator>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(menu, MenuPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(menu); }
        }

        private static void RenderPreview(GameObject prefab, AnimationClip clip, float time, string filename)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            GameObject cameraObject = new GameObject("CutoutPreviewCamera", typeof(Camera));
            instance.hideFlags = cameraObject.hideFlags = HideFlags.HideAndDontSave;
            RenderTexture target = new RenderTexture(477, 731, 24, RenderTextureFormat.ARGB32);
            Texture2D capture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                foreach (Transform node in instance.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                instance.GetComponent<Animator>().enabled = false;
                clip.SampleAnimation(instance, time);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 365.5f;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .27f, .18f, 1f);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                Canvas canvas = instance.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), filename), capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Colony Flow/Main Menu/Validate Cutout Ant")]
        private static void Validate()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
            var linked = new SerializedObject(menu.GetComponent<CanvasMainMenu>()).FindProperty("mainMenuAntAnimator").objectReferenceValue as Animator;
            if (linked == null || linked.runtimeAnimatorController != source.GetComponent<Animator>().runtimeAnimatorController || linked.updateMode != AnimatorUpdateMode.UnscaledTime)
                throw new InvalidOperationException("Main Menu Animator link or update mode is incorrect.");
            GameObject instance = UnityEngine.Object.Instantiate(source);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.GetComponent<Animator>().enabled = false;
                Image[] images = instance.GetComponentsInChildren<Image>();
                if (images.Length != 9) throw new InvalidOperationException("Expected nine rigid UI Images.");
                Vector3[] scales = new Vector3[images.Length];
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].sprite == null || images[i].raycastTarget) throw new InvalidOperationException("Missing sprite or UI raycast enabled.");
                    scales[i] = images[i].transform.localScale;
                }
                Transform panel = instance.transform.Find("Panel");
                Transform left = instance.transform.Find("AntRigRoot/Leg_L_Pivot");
                Transform right = instance.transform.Find("AntRigRoot/Leg_R_Pivot");
                Transform upper = instance.transform.Find("AntRigRoot/UpperBodyPivot");
                clip.SampleAnimation(instance, 0f);
                Vector3 panelStart = panel.position, leftStart = left.position, rightStart = right.position, upperStart = upper.position;
                Transform[] nodes = instance.GetComponentsInChildren<Transform>();
                Matrix4x4[] matrices = new Matrix4x4[nodes.Length];
                for (int i = 0; i < nodes.Length; i++) matrices[i] = nodes[i].localToWorldMatrix;
                clip.SampleAnimation(instance, 1f);
                if (Vector3.Distance(upperStart, upper.position) < 6f) throw new InvalidOperationException("Breathing movement missing.");
                for (int frame = 0; frame <= 120; frame++)
                {
                    clip.SampleAnimation(instance, frame / 60f);
                    if (Vector3.Distance(panelStart, panel.position) > .001f || Vector3.Distance(leftStart, left.position) > .001f || Vector3.Distance(rightStart, right.position) > .001f)
                        throw new InvalidOperationException("Panel or feet move unexpectedly.");
                    for (int i = 0; i < images.Length; i++)
                        if (Vector3.Distance(scales[i], images[i].transform.localScale) > .0001f) throw new InvalidOperationException("Image scale deformation detected.");
                }
                clip.SampleAnimation(instance, 2f);
                for (int i = 0; i < nodes.Length; i++)
                    for (int element = 0; element < 16; element++)
                        if (Mathf.Abs(matrices[i][element] - nodes[i].localToWorldMatrix[element]) > .001f) throw new InvalidOperationException("Loop seam mismatch.");
                Debug.Log("MENU_CUTOUT_VALIDATED: menu link, 121 pose samples, no Image scale deformation, fixed panel/feet, seamless loop.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
#endif
