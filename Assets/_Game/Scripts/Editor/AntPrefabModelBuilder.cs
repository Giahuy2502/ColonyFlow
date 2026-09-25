#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    /// <summary>
    /// Builds the lightweight ant model directly into Ant.prefab. This code only
    /// runs in the editor; the built player never creates components at runtime.
    /// </summary>
    [InitializeOnLoad]
    internal static class AntPrefabModelBuilder
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Ant.prefab";
        private const string BodyMaterialPath = "Assets/_Game/Materials/Ant.mat";
        private const string DetailMaterialPath = "Assets/_Game/Materials/AntDetail.mat";
        private const string PixelMaterialPath = "Assets/_Game/Materials/Pixel.mat";
        private const string ModelName = "Ant Model V2";

        static AntPrefabModelBuilder()
        {
            EditorApplication.delayCall += BuildIfNeeded;
        }
        private static void BuildIfNeeded() => Build(false);

        [MenuItem("Colony Flow/Rebuild Ant Model")]
        private static void RebuildFromMenu()
        {
            Build(true);
        }

        private static void Build(bool force)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"Ant prefab was not found at {PrefabPath}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
                return;

            try
            {
                Transform existing = root.transform.Find(ModelName);
                if (existing != null && !force && IsCurrentModel(existing))
                    return;

                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                MeshFilter oldFilter = root.GetComponent<MeshFilter>();
                MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
                if (oldRenderer != null)
                    Object.DestroyImmediate(oldRenderer);
                if (oldFilter != null)
                    Object.DestroyImmediate(oldFilter);

                Material body = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
                Material detail = GetOrCreateDetailMaterial(body);
                Material pixel = AssetDatabase.LoadAssetAtPath<Material>(PixelMaterialPath);

                GameObject model = new GameObject(ModelName);
                model.transform.SetParent(root.transform, false);

                // Rounded white body matching the small toy-like ants in the reference.
                CreatePart("Abdomen", PrimitiveType.Sphere, model.transform,
                    new Vector3(0f, 0.105f, -0.11f), new Vector3(0.18f, 0.15f, 0.20f), Vector3.zero, body);
                CreatePart("Thorax", PrimitiveType.Sphere, model.transform,
                    new Vector3(0f, 0.09f, 0.015f), new Vector3(0.095f, 0.09f, 0.11f), Vector3.zero, body);
                CreatePart("Head", PrimitiveType.Sphere, model.transform,
                    new Vector3(0f, 0.11f, 0.12f), new Vector3(0.17f, 0.15f, 0.16f), Vector3.zero, body);

                CreatePart("Eye L", PrimitiveType.Sphere, model.transform,
                    new Vector3(-0.058f, 0.135f, 0.184f), Vector3.one * 0.06f, Vector3.zero, detail);
                CreatePart("Eye R", PrimitiveType.Sphere, model.transform,
                    new Vector3(0.058f, 0.135f, 0.184f), Vector3.one * 0.06f, Vector3.zero, detail);

                CreateBentLeg(model.transform, "Front Leg L", -1f, 0.067f,
                    0.098f, 0.132f, body);
                CreateBentLeg(model.transform, "Middle Leg L", -1f, 0.008f,
                    0.012f, 0.018f, body);
                CreateBentLeg(model.transform, "Back Leg L", -1f, -0.052f,
                    -0.088f, -0.128f, body);
                CreateBentLeg(model.transform, "Front Leg R", 1f, 0.067f,
                    0.098f, 0.132f, body);
                CreateBentLeg(model.transform, "Middle Leg R", 1f, 0.008f,
                    0.012f, 0.018f, body);
                CreateBentLeg(model.transform, "Back Leg R", 1f, -0.052f,
                    -0.088f, -0.128f, body);

                CreateBentAntenna(model.transform, "Antenna L", -1f, body);
                CreateBentAntenna(model.transform, "Antenna R", 1f, body);

                Transform carried = root.transform.Find("Carried Pixel");
                if (carried == null)
                {
                    GameObject carriedObject = CreatePart("Carried Pixel", PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.085f, 0.225f), new Vector3(0.12f, 0.075f, 0.12f),
                        new Vector3(10f, 8f, 0f), pixel);
                    carried = carriedObject.transform;
                }
                else
                {
                    carried.localPosition = new Vector3(0f, 0.085f, 0.225f);
                    carried.localRotation = Quaternion.Euler(10f, 8f, 0f);
                    carried.localScale = new Vector3(0.12f, 0.075f, 0.12f);
                }

                Ant ant = root.GetComponent<Ant>();
                SerializedObject serializedAnt = new SerializedObject(ant);
                MeshRenderer[] modelRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
                SerializedProperty bodyRenderers = serializedAnt.FindProperty("bodyRenderers");
                bodyRenderers.arraySize = modelRenderers.Length - 2;
                int bodyIndex = 0;
                for (int i = 0; i < modelRenderers.Length; i++)
                {
                    string rendererName = modelRenderers[i].name;
                    if (rendererName == "Eye L" || rendererName == "Eye R")
                        continue;
                    bodyRenderers.GetArrayElementAtIndex(bodyIndex++).objectReferenceValue = modelRenderers[i];
                }
                serializedAnt.FindProperty("carriedPixelVisual").objectReferenceValue = carried.gameObject;
                serializedAnt.FindProperty("carriedPixelRenderer").objectReferenceValue = carried.GetComponent<MeshRenderer>();
                serializedAnt.ApplyModifiedPropertiesWithoutUndo();
                carried.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsCurrentModel(Transform model)
        {
            Transform leftEye = model.Find("Eye L");
            return leftEye != null &&
                   Mathf.Approximately(leftEye.localScale.x, 0.06f) &&
                   model.Find("Front Leg L/Upper") != null &&
                   model.Find("Antenna L/Upper") != null;
        }

        private static void CreateBentLeg(Transform parent, string name, float side,
            float startZ, float jointZ, float endZ, Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector3 start = new Vector3(side * 0.052f, 0.074f, startZ);
            Vector3 joint = new Vector3(side * 0.112f, 0.048f, jointZ);
            Vector3 end = new Vector3(side * 0.145f, 0.012f, endZ);
            CreateRoundedSegment("Upper", root.transform, start, joint, 0.021f, material);
            CreatePart("Joint", PrimitiveType.Sphere, root.transform,
                joint, Vector3.one * 0.026f, Vector3.zero, material);
            CreateRoundedSegment("Lower", root.transform, joint, end, 0.021f, material);
        }

        private static void CreateBentAntenna(Transform parent, string name,
            float side, Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector3 start = new Vector3(side * 0.043f, 0.168f, 0.177f);
            Vector3 joint = new Vector3(side * 0.068f, 0.205f, 0.218f);
            Vector3 end = new Vector3(side * 0.096f, 0.218f, 0.248f);
            CreateRoundedSegment("Lower", root.transform, start, joint, 0.019f, material);
            CreatePart("Joint", PrimitiveType.Sphere, root.transform,
                joint, Vector3.one * 0.021f, Vector3.zero, material);
            CreateRoundedSegment("Upper", root.transform, joint, end, 0.019f, material);
        }

        private static GameObject CreateRoundedSegment(string name, Transform parent,
            Vector3 start, Vector3 end, float diameter, Material material)
        {
            Vector3 direction = end - start;
            GameObject segment = CreatePart(name, PrimitiveType.Cylinder, parent,
                (start + end) * 0.5f,
                new Vector3(diameter * 0.5f, direction.magnitude * 0.5f, diameter * 0.5f),
                Vector3.zero, material);
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            return segment;
        }

        private static GameObject CreatePart(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Vector3 rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(rotation);
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        private static Material GetOrCreateDetailMaterial(Material body)
        {
            Material detail = AssetDatabase.LoadAssetAtPath<Material>(DetailMaterialPath);
            if (detail != null)
                return detail;

            detail = new Material(body.shader) { name = "AntDetail" };
            Color dark = new Color(0.055f, 0.045f, 0.04f, 1f);
            detail.SetColor("_BaseColor", dark);
            detail.SetColor("_Color", dark);
            detail.enableInstancing = true;
            AssetDatabase.CreateAsset(detail, DetailMaterialPath);
            return detail;
        }

    }
}
#endif
