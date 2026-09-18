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
        private const string BaseMaterialPath = "Assets/_Game/Materials/ColonyBase.mat";

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

        private static void Build(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                AssetDatabase.CreateFolder("Assets/_Game", "Models");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = CreateBeveledCube(0.09f);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else if (force)
            {
                Mesh rebuilt = CreateBeveledCube(0.09f);
                EditorUtility.CopySerialized(rebuilt, mesh);
                Object.DestroyImmediate(rebuilt);
            }
            AssignPrefabMesh("Assets/_Game/Prefabs/Pixel.prefab", mesh, "Pixel");
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
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter[] existingFilters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter filter in existingFilters)
                    if (filter.name == "Colony" || filter.name == "Soft Top")
                        filter.sharedMesh = mesh;

                Transform oldWrap = root.transform.Find("Gift Wrap");
                if (oldWrap != null)
                    Object.DestroyImmediate(oldWrap.gameObject);
                Transform oldBase = root.transform.Find("Colony Base");
                if (oldBase != null)
                    Object.DestroyImmediate(oldBase.gameObject);
                CreateModelPart("Colony Base", root.transform, mesh, baseMaterial,
                    new Vector3(0f, -0.53f, 0.035f), new Vector3(1.06f, 0.18f, 1.06f), Vector3.zero);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
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

        private static Mesh CreateBeveledCube(float radius)
        {
            const float half = 0.5f;
            float inner = half - radius;
            float[] coordinates = { -half, -inner, inner, half };
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var triangles = new List<int>(324);
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
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        Vector3 sharp = faceNormal * half + axisU * coordinates[x] + axisV * coordinates[y];
                        Vector3 closest = new Vector3(Mathf.Clamp(sharp.x, -inner, inner),
                            Mathf.Clamp(sharp.y, -inner, inner), Mathf.Clamp(sharp.z, -inner, inner));
                        Vector3 normal = (sharp - closest).normalized;
                        vertices.Add(closest + normal * radius);
                        normals.Add(normal); uvs.Add(new Vector2(x / 3f, y / 3f));
                    }
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        int a = start + y * 4 + x, b = a + 1, c = a + 4, d = c + 1;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
            }
        }
    }
}
#endif
