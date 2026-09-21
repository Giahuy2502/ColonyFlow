#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    /// <summary>
    /// Builds the lightweight ant model directly into Ant.prefab. This code only
    /// runs in the editor; the built player never creates components at runtime.
    /// </summary>
    internal static class AntPrefabModelBuilder
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Ant.prefab";
        private const string BodyMaterialPath = "Assets/_Game/Materials/Ant.mat";
        private const string DetailMaterialPath = "Assets/_Game/Materials/AntDetail.mat";
        private const string PixelMaterialPath = "Assets/_Game/Materials/Pixel.mat";
        private const string ModelName = "Ant Model V2";

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
                if (existing != null && !force)
                    return;

                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                MeshFilter oldFilter = root.GetComponent<MeshFilter>();
                MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
                if (oldRenderer != null)
                    Object.DestroyImmediate(oldRenderer);
                if (oldFilter != null)
                    Object.DestroyImmediate(oldFilter);

                root.transform.localScale = Vector3.one;

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
                    new Vector3(-0.052f, 0.137f, 0.184f), Vector3.one * 0.032f, Vector3.zero, detail);
                CreatePart("Eye R", PrimitiveType.Sphere, model.transform,
                    new Vector3(0.052f, 0.137f, 0.184f), Vector3.one * 0.032f, Vector3.zero, detail);

                CreateLeg(model.transform, "Front Leg L", -1f, 0.055f, 0.068f, 28f);
                CreateLeg(model.transform, "Middle Leg L", -1f, 0.052f, 0.005f, 3f);
                CreateLeg(model.transform, "Back Leg L", -1f, 0.052f, -0.065f, -30f);
                CreateLeg(model.transform, "Front Leg R", 1f, 0.055f, 0.068f, -28f);
                CreateLeg(model.transform, "Middle Leg R", 1f, 0.052f, 0.005f, -3f);
                CreateLeg(model.transform, "Back Leg R", 1f, 0.052f, -0.065f, 30f);

                CreatePart("Antenna L", PrimitiveType.Cube, model.transform,
                    new Vector3(-0.053f, 0.16f, 0.218f), new Vector3(0.011f, 0.011f, 0.09f),
                    new Vector3(0f, -24f, -8f), body);
                CreatePart("Antenna R", PrimitiveType.Cube, model.transform,
                    new Vector3(0.053f, 0.16f, 0.218f), new Vector3(0.011f, 0.011f, 0.09f),
                    new Vector3(0f, 24f, 8f), body);

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

        private static void CreateLeg(Transform parent, string name, float side, float y, float z, float yaw)
        {
            CreatePart(name, PrimitiveType.Cube, parent,
                new Vector3(side * 0.077f, y, z), new Vector3(0.075f, 0.011f, 0.014f),
                new Vector3(0f, yaw, side * -12f),
                AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath));
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
