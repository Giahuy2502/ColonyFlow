#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class ColonyFlowModelBuilder
    {
        private const string ModelFolder = "Assets/_Game/Models";
        private const string MeshPath = ModelFolder + "/SoftBeveledCube.asset";
        private const string PixelModelPath = ModelFolder + "/rounded_cube.fbx";
        private const string BaseMaterialPath = "Assets/_Game/Materials/ColonyBase.mat";
        private const string OutlineMaterialPath =
            "Assets/_Game/Materials/ColonyOutline.mat";
        private const string OutlineShaderName = "ColonyFlow/Colony Outline";

        static ColonyFlowModelBuilder()
        {
            EditorApplication.delayCall += BuildIfNeeded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += BuildIfNeeded;
            };
        }

        [MenuItem("Colony Flow/Rebuild Gameplay Models")]
        private static void Rebuild() => Build(true);
        private static void BuildIfNeeded() => Build(false);

        public static void RefreshColonyPrefab()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
                throw new MissingReferenceException($"Missing colony mesh at {MeshPath}.");
            BuildColonyModel(mesh);
            AssetDatabase.SaveAssets();
        }

        private static void Build(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                AssetDatabase.CreateFolder("Assets/_Game", "Models");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = CreateBeveledCube(0.09f, 1);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else if (force)
            {
                Mesh rebuilt = CreateBeveledCube(0.09f, 1);
                EditorUtility.CopySerialized(rebuilt, mesh);
                Object.DestroyImmediate(rebuilt);
            }

            Mesh pixelMesh = LoadFirstMesh(PixelModelPath);
            if (pixelMesh != null)
                AssignPrefabMesh("Assets/_Game/Prefabs/Pixel.prefab", pixelMesh, "Pixel");
            BuildColonyModel(mesh);
            BuildTraySlot(mesh);
            AssetDatabase.SaveAssets();
        }

        private static void BuildColonyModel(Mesh mesh)
        {
            const string path = "Assets/_Game/Prefabs/Colony.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            Material baseMaterial = GetBaseMaterial();
            Material outlineMaterial = GetOutlineMaterial();
            if (outlineMaterial == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter[] existingFilters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter filter in existingFilters)
                    if (filter.name == "Colony" || filter.name == "Soft Top")
                        filter.sharedMesh = mesh;

                RemoveDirectChildrenNamed(root.transform, "Gift Wrap");
                Transform visualRoot = root.transform.Find("Visual") ?? root.transform;
                if (visualRoot != root.transform)
                    RemoveDirectChildrenNamed(root.transform, "Colony Base");

                MeshRenderer baseRenderer = EnsureModelPart("Colony Base", visualRoot,
                    mesh, baseMaterial, new Vector3(0f, -0.53f, 0.035f),
                    new Vector3(1.06f, 0.18f, 1.06f), Vector3.zero);
                Transform top = visualRoot.Find("Soft Top");
                MeshRenderer topOutline = top != null
                    ? EnsureOutlinePart(top, outlineMaterial)
                    : null;
                MeshRenderer baseOutline = EnsureOutlinePart(
                    baseRenderer.transform, outlineMaterial);
                AssignFeedbackRenderers(root, baseRenderer, topOutline, baseOutline);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static MeshRenderer EnsureModelPart(string objectName, Transform parent,
            Mesh mesh, Material material, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            Transform part = FindSingleDirectChild(parent, objectName);
            if (part == null)
            {
                var partObject = new GameObject(objectName);
                part = partObject.transform;
                part.SetParent(parent, false);
            }

            part.localPosition = position;
            part.localRotation = Quaternion.Euler(rotation);
            part.localScale = scale;
            if (!part.TryGetComponent(out MeshFilter filter))
                filter = part.gameObject.AddComponent<MeshFilter>();
            if (!part.TryGetComponent(out MeshRenderer renderer))
                renderer = part.gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        private static MeshRenderer EnsureOutlinePart(Transform source, Material material)
        {
            Transform outline = FindSingleDirectChild(source, "Colony Outline");
            if (outline == null)
            {
                var outlineObject = new GameObject("Colony Outline");
                outline = outlineObject.transform;
                outline.SetParent(source, false);
            }

            outline.localPosition = Vector3.zero;
            outline.localRotation = Quaternion.identity;
            outline.localScale = Vector3.one;
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (!outline.TryGetComponent(out MeshFilter outlineFilter))
                outlineFilter = outline.gameObject.AddComponent<MeshFilter>();
            if (!outline.TryGetComponent(out MeshRenderer renderer))
                renderer = outline.gameObject.AddComponent<MeshRenderer>();
            outlineFilter.sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        private static Transform FindSingleDirectChild(Transform parent, string childName)
        {
            Transform first = null;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name != childName)
                    continue;
                if (first == null)
                    first = child;
                else
                    Object.DestroyImmediate(child.gameObject);
            }
            return first;
        }

        private static void RemoveDirectChildrenNamed(Transform parent, string childName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void AssignFeedbackRenderers(GameObject root,
            Renderer baseRenderer, params Renderer[] outlineRenderers)
        {
            ColonyView view = root.GetComponent<ColonyView>();
            if (view == null)
                throw new MissingReferenceException(
                    $"{root.name} requires a ColonyView component.");

            var serializedView = new SerializedObject(view);
            SerializedProperty dimRenderers =
                serializedView.FindProperty("additionalDimRenderers");
            dimRenderers.arraySize = 1;
            dimRenderers.GetArrayElementAtIndex(0).objectReferenceValue = baseRenderer;

            SerializedProperty outlines = serializedView.FindProperty("outlineRenderers");
            outlines.arraySize = outlineRenderers.Length;
            for (int i = 0; i < outlineRenderers.Length; i++)
                outlines.GetArrayElementAtIndex(i).objectReferenceValue = outlineRenderers[i];
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTraySlot(Mesh mesh)
        {
            const string path = "Assets/_Game/Prefabs/TraySlot.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter rootFilter = root.GetComponent<MeshFilter>();
                rootFilter.sharedMesh = mesh;
                Transform oldBase = root.transform.Find("Slot Base");
                if (oldBase != null)
                    Object.DestroyImmediate(oldBase.gameObject);
                CreateModelPart("Slot Base", root.transform, mesh, GetBaseMaterial(),
                    new Vector3(0f, -0.57f, 0.035f), new Vector3(1.06f, 0.20f, 1.06f), Vector3.zero);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateModelPart(string name, Transform parent, Mesh mesh,
            Material material, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(rotation);
            part.transform.localScale = scale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Material GetBaseMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (material != null)
                return material;
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/Pixel.mat");
            material = new Material(source) { name = "ColonyBase" };
            Color baseColor = new Color(0.78f, 0.80f, 0.78f, 1f);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_Color", baseColor);
            material.SetFloat("_Smoothness", 0.34f);
            AssetDatabase.CreateAsset(material, BaseMaterialPath);
            return material;
        }

        private static Material GetOutlineMaterial()
        {
            Shader shader = Shader.Find(OutlineShaderName);
            if (shader == null)
            {
                Debug.LogError($"Cannot find shader '{OutlineShaderName}'.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "ColonyOutline" };
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_OutlineColor", Color.white);
            material.SetFloat("_OutlineWidth", 0.03f);
            material.enableInstancing = true;
            material.renderQueue = 1999;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignPrefabMesh(string path, Mesh mesh, params string[] objectNames)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter filter in filters)
                    foreach (string objectName in objectNames)
                        if (filter.name == objectName)
                        {
                            filter.sharedMesh = mesh;
                            break;
                        }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Mesh LoadFirstMesh(string modelPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] is Mesh mesh)
                    return mesh;

            Debug.LogError($"Cannot find a Mesh in model '{modelPath}'.");
            return null;
        }

        private static Mesh CreateBeveledCube(float radius, int bevelSegments)
        {
            const float half = 0.5f;
            radius = Mathf.Clamp(radius, 0.001f, half);
            bevelSegments = Mathf.Max(1, bevelSegments);
            float inner = half - radius;
            int sideVertexCount = bevelSegments * 2 + 2;
            float[] coordinates = new float[sideVertexCount];
            for (int i = 0; i <= bevelSegments; i++)
            {
                float offset = radius * i / bevelSegments;
                coordinates[i] = -half + offset;
                coordinates[sideVertexCount - 1 - i] = half - offset;
            }
            var vertices = new List<Vector3>(sideVertexCount * sideVertexCount * 6);
            var normals = new List<Vector3>(sideVertexCount * sideVertexCount * 6);
            var uvs = new List<Vector2>(sideVertexCount * sideVertexCount * 6);
            var triangles = new List<int>((sideVertexCount - 1) * (sideVertexCount - 1) * 36);
            AddFace(Vector3.right, Vector3.up, Vector3.forward);
            AddFace(Vector3.left, Vector3.up, Vector3.back);
            AddFace(Vector3.up, Vector3.forward, Vector3.right);
            AddFace(Vector3.down, Vector3.forward, Vector3.left);
            AddFace(Vector3.forward, Vector3.right, Vector3.up);
            AddFace(Vector3.back, Vector3.right, Vector3.down);
            Mesh mesh = new Mesh { name = "Soft Beveled Cube" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;

            void AddFace(Vector3 faceNormal, Vector3 axisU, Vector3 axisV)
            {
                int start = vertices.Count;
                for (int y = 0; y < sideVertexCount; y++)
                    for (int x = 0; x < sideVertexCount; x++)
                    {
                        Vector3 sharp = faceNormal * half + axisU * coordinates[x] + axisV * coordinates[y];
                        Vector3 closest = new Vector3(Mathf.Clamp(sharp.x, -inner, inner),
                            Mathf.Clamp(sharp.y, -inner, inner), Mathf.Clamp(sharp.z, -inner, inner));
                        Vector3 normal = (sharp - closest).normalized;
                        vertices.Add(closest + normal * radius);
                        normals.Add(normal);
                        uvs.Add(new Vector2(x / (float)(sideVertexCount - 1),
                            y / (float)(sideVertexCount - 1)));
                    }
                for (int y = 0; y < sideVertexCount - 1; y++)
                    for (int x = 0; x < sideVertexCount - 1; x++)
                    {
                        int a = start + y * sideVertexCount + x;
                        int b = a + 1;
                        int c = a + sideVertexCount;
                        int d = c + 1;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
            }
        }
    }
}
#endif
