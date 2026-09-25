using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyFlow
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(PixelBoard))]
    public sealed class PixelBoardRenderer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private PixelBoard board;
        [SerializeField] private Pixel pixelPrefab;
        [SerializeField] private Transform pixelRoot;
        [SerializeField] private Material pixelMaterial;
        [SerializeField] private Vector3 pixelScale = new Vector3(0.84f, 1.18f, 0.84f);
        [SerializeField, Range(0.1f, 0.6f)] private float borderWidth = 0.28f;
        [SerializeField, Range(2, 12)] private int borderCornerSegments = 8;
        [SerializeField] private Color boardSurfaceColor = new Color(0.98f, 0.98f, 0.96f, 1f);
        [SerializeField] private Color outerBorderColor = new Color(0.68f, 0.48f, 0.28f, 1f);
        [SerializeField, Range(0.03f, 0.25f)] private float outerBorderWidth = 0.09f;
        [Header("Board Visual Objects")]
        [SerializeField] private MeshFilter boardSurfaceFilter;
        [SerializeField] private MeshRenderer boardSurfaceRenderer;
        [SerializeField] private MeshFilter boardFrameFilter;
        [SerializeField] private MeshRenderer boardFrameRenderer;
        [SerializeField] private MeshFilter outerBorderFilter;
        [SerializeField] private MeshRenderer outerBorderRenderer;
        [SerializeField] private ShadowCastingMode castShadows = ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;

        private readonly Dictionary<int, Pixel> pixelsByIndex = new Dictionary<int, Pixel>();
        private readonly Queue<Pixel> pixelPool = new Queue<Pixel>();
        private MaterialPropertyBlock[] colorBlocks;
        private MaterialPropertyBlock boardSurfaceBlock;
        private MaterialPropertyBlock outerBorderBlock;
        private Mesh boardFrameMesh;
        private Mesh boardSurfaceMesh;
        private Mesh outerBorderMesh;

        private void Awake()
        {
            if (Application.isPlaying)
                CreateRuntimeResources();
            else
                RefreshEditorPreview();
        }

        private void OnEnable()
        {
            if (board == null)
                return;

            if (!Application.isPlaying)
            {
                RefreshEditorPreview();
                return;
            }

            board.BoardBuilt += Rebuild;
            board.BoardCleared += ClearRuntimeVisuals;
            board.PixelCollected += OnPixelCollected;
            if (board.IsBuilt)
                Rebuild();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || board == null)
                return;

            board.BoardBuilt -= Rebuild;
            board.BoardCleared -= ClearRuntimeVisuals;
            board.PixelCollected -= OnPixelCollected;
        }

        private void OnDestroy()
        {
            DestroyGenerated(boardFrameMesh);
            DestroyGenerated(boardSurfaceMesh);
            DestroyGenerated(outerBorderMesh);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && isActiveAndEnabled)
                RefreshEditorPreview();
        }

        private void CreateRuntimeResources()
        {
            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            colorBlocks = new MaterialPropertyBlock[colorCount];
            boardSurfaceBlock = new MaterialPropertyBlock();
            boardSurfaceBlock.SetColor(BaseColorId, boardSurfaceColor);
            boardSurfaceBlock.SetColor(ColorId, boardSurfaceColor);
            outerBorderBlock = new MaterialPropertyBlock();
            outerBorderBlock.SetColor(BaseColorId, outerBorderColor);
            outerBorderBlock.SetColor(ColorId, outerBorderColor);

            for (int i = 0; i < colorCount; i++)
            {
                colorBlocks[i] = new MaterialPropertyBlock();
                UnityEngine.Color tint = PixelColorUtility.ToUnityColor((PixelColor)i);
                colorBlocks[i].SetColor(BaseColorId, tint);
                colorBlocks[i].SetColor(ColorId, tint);
            }

            AssignBoardMaterial(boardSurfaceRenderer);
            AssignBoardMaterial(boardFrameRenderer);
            AssignBoardMaterial(outerBorderRenderer);
        }

        [ContextMenu("Refresh Board Visual Preview")]
        private void RefreshEditorPreview()
        {
            if (Application.isPlaying || board == null)
                return;

            if (boardSurfaceBlock == null)
                boardSurfaceBlock = new MaterialPropertyBlock();
            boardSurfaceBlock.SetColor(BaseColorId, boardSurfaceColor);
            boardSurfaceBlock.SetColor(ColorId, boardSurfaceColor);

            if (outerBorderBlock == null)
                outerBorderBlock = new MaterialPropertyBlock();
            outerBorderBlock.SetColor(BaseColorId, outerBorderColor);
            outerBorderBlock.SetColor(ColorId, outerBorderColor);

            if (colorBlocks == null || colorBlocks.Length != Enum.GetValues(typeof(PixelColor)).Length)
                colorBlocks = new MaterialPropertyBlock[Enum.GetValues(typeof(PixelColor)).Length];
            int whiteIndex = (int)PixelColor.White;
            if (colorBlocks[whiteIndex] == null)
                colorBlocks[whiteIndex] = new MaterialPropertyBlock();
            Color frameColor = PixelColorUtility.ToUnityColor(PixelColor.White);
            colorBlocks[whiteIndex].SetColor(BaseColorId, frameColor);
            colorBlocks[whiteIndex].SetColor(ColorId, frameColor);

            AssignPreviewMaterial(boardSurfaceRenderer);
            AssignPreviewMaterial(boardFrameRenderer);
            AssignPreviewMaterial(outerBorderRenderer);
            BuildBoardMeshes(board.Size);
        }

        private void AssignPreviewMaterial(MeshRenderer targetRenderer)
        {
            if (targetRenderer == null || pixelMaterial == null)
                return;
            targetRenderer.sharedMaterial = pixelMaterial;
            targetRenderer.shadowCastingMode = castShadows;
            targetRenderer.receiveShadows = receiveShadows;
        }

        private static void DestroyGenerated(UnityEngine.Object generatedObject)
        {
            if (generatedObject == null)
                return;
            if (Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }

        private void Rebuild()
        {
            if (board == null || pixelPrefab == null || pixelRoot == null)
                return;

            foreach (Pixel pixel in pixelsByIndex.Values)
            {
                pixel.MarkCollected();
                pixelPool.Enqueue(pixel);
            }
            pixelsByIndex.Clear();

            if (!board.IsBuilt)
                return;

            Vector2Int boardSize = board.Size;
            BuildBoardMeshes(boardSize);
            for (int y = 0; y < boardSize.y; y++)
            {
                for (int x = 0; x < boardSize.x; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (!board.TryGetCell(position, out PixelCell cell) || !cell.IsOccupied)
                        continue;

                    int boardIndex = y * boardSize.x + x;
                    Pixel pixel = pixelPool.Count > 0
                        ? pixelPool.Dequeue()
                        : Instantiate(pixelPrefab, pixelRoot, false);
                    pixel.name = $"Pixel {x}-{y} {cell.Color}";
                    pixel.transform.localPosition = board.GridToLocalPosition(position);
                    pixel.transform.localRotation = Quaternion.identity;
                    pixel.transform.localScale = pixelScale * board.CellSize;
                    pixel.Initialize(position, cell.Color);
                    pixelsByIndex[boardIndex] = pixel;
                }
            }
        }

        private void ClearRuntimeVisuals()
        {
            foreach (Pixel pixel in pixelsByIndex.Values)
            {
                pixel.MarkCollected();
                pixelPool.Enqueue(pixel);
            }
            pixelsByIndex.Clear();
            boardFrameMesh?.Clear();
            boardSurfaceMesh?.Clear();
            outerBorderMesh?.Clear();
        }

        private void OnPixelCollected(Vector2Int position, PixelColor color)
        {
            int boardIndex = position.y * board.Size.x + position.x;
            if (!pixelsByIndex.TryGetValue(boardIndex, out Pixel pixel))
                return;
            pixelsByIndex.Remove(boardIndex);
            pixel.MarkCollected();
            pixelPool.Enqueue(pixel);
        }

        private void AssignBoardMaterial(MeshRenderer targetRenderer)
        {
            if (targetRenderer == null)
                return;
            targetRenderer.sharedMaterial = pixelMaterial;
            targetRenderer.shadowCastingMode = castShadows;
            targetRenderer.receiveShadows = receiveShadows;
        }

        private void BuildBoardMeshes(Vector2Int boardSize)
        {
            float cell = board.CellSize;
            float innerMinX = -0.56f * cell;
            float innerMinZ = -0.56f * cell;
            float innerMaxX = (boardSize.x - 1) * cell + 0.56f * cell;
            float innerMaxZ = (boardSize.y - 1) * cell + 0.56f * cell;
            float thickness = borderWidth * cell;
            float outerRadius = 0.82f * cell;
            float innerRadius = Mathf.Max(0.2f * cell, outerRadius - thickness);
            float trimThickness = outerBorderWidth * cell;
            float topY = -0.04f * cell;
            float bottomY = -0.24f * cell;

            float frameMinX = innerMinX - thickness;
            float frameMinZ = innerMinZ - thickness;
            float frameMaxX = innerMaxX + thickness;
            float frameMaxZ = innerMaxZ + thickness;

            outerBorderMesh = CreateRoundedPlateMesh(outerBorderMesh,
                frameMinX - trimThickness, frameMinZ - trimThickness,
                frameMaxX + trimThickness, frameMaxZ + trimThickness,
                outerRadius + trimThickness,
                bottomY - 0.01f * cell, bottomY - 0.20f * cell, borderCornerSegments);

            boardFrameMesh = CreateRoundedRingMesh(boardFrameMesh,
                frameMinX, frameMinZ, frameMaxX, frameMaxZ, outerRadius,
                innerMinX, innerMinZ, innerMaxX, innerMaxZ, innerRadius,
                topY, bottomY, borderCornerSegments);
            boardSurfaceMesh = CreateRoundedSurfaceMesh(boardSurfaceMesh,
                innerMinX, innerMinZ, innerMaxX, innerMaxZ, innerRadius,
                bottomY + 0.01f * cell, borderCornerSegments);

            AssignBoardVisual(boardSurfaceFilter, boardSurfaceRenderer,
                boardSurfaceMesh, boardSurfaceBlock);
            AssignBoardVisual(boardFrameFilter, boardFrameRenderer,
                boardFrameMesh, colorBlocks[(int)PixelColor.White]);
            AssignBoardVisual(outerBorderFilter, outerBorderRenderer,
                outerBorderMesh, outerBorderBlock);
        }

        private static void AssignBoardVisual(MeshFilter targetFilter,
            MeshRenderer targetRenderer, Mesh mesh, MaterialPropertyBlock properties)
        {
            if (targetFilter != null)
                targetFilter.sharedMesh = mesh;
            if (targetRenderer != null)
                targetRenderer.SetPropertyBlock(properties);
        }

        private static Mesh CreateRoundedRingMesh(Mesh mesh,
            float outerMinX, float outerMinZ, float outerMaxX, float outerMaxZ, float outerRadius,
            float innerMinX, float innerMinZ, float innerMaxX, float innerMaxZ, float innerRadius,
            float topY, float bottomY, int cornerSegments)
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Continuous Rounded Board Frame" };
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }
            int perimeterCount = cornerSegments * 4;
            var vertices = new Vector3[perimeterCount * 4];
            var triangles = new int[perimeterCount * 18];
            for (int i = 0; i < perimeterCount; i++)
            {
                float angle = Mathf.PI * 2f * i / perimeterCount;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 outerCenter = RoundedCornerCenter(
                    outerMinX, outerMinZ, outerMaxX, outerMaxZ, outerRadius, direction);
                Vector2 innerCenter = RoundedCornerCenter(
                    innerMinX, innerMinZ, innerMaxX, innerMaxZ, innerRadius, direction);
                Vector2 outer = outerCenter + direction * outerRadius;
                Vector2 inner = innerCenter + direction * innerRadius;
                int vertex = i * 4;
                vertices[vertex] = new Vector3(outer.x, topY, outer.y);
                vertices[vertex + 1] = new Vector3(inner.x, topY, inner.y);
                vertices[vertex + 2] = new Vector3(outer.x, bottomY, outer.y);
                vertices[vertex + 3] = new Vector3(inner.x, bottomY, inner.y);
            }
            for (int i = 0; i < perimeterCount; i++)
            {
                int next = (i + 1) % perimeterCount;
                int a = i * 4;
                int b = next * 4;
                int triangle = i * 18;
                AddQuad(triangles, triangle, a, b, b + 1, a + 1);
                AddQuad(triangles, triangle + 6, a + 2, b + 2, b, a);
                AddQuad(triangles, triangle + 12, a + 1, b + 1, b + 3, a + 3);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRoundedSurfaceMesh(Mesh mesh, float minX, float minZ,
            float maxX, float maxZ, float radius, float y, int cornerSegments)
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Rounded Board Surface" };
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }
            int perimeterCount = cornerSegments * 4;
            var vertices = new Vector3[perimeterCount + 1];
            var triangles = new int[perimeterCount * 3];
            vertices[0] = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);
            for (int i = 0; i < perimeterCount; i++)
            {
                float angle = Mathf.PI * 2f * i / perimeterCount;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 center = RoundedCornerCenter(minX, minZ, maxX, maxZ, radius, direction);
                Vector2 point = center + direction * radius;
                vertices[i + 1] = new Vector3(point.x, y, point.y);
                int next = (i + 1) % perimeterCount;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRoundedPlateMesh(Mesh mesh, float minX, float minZ,
            float maxX, float maxZ, float radius, float topY, float bottomY, int cornerSegments)
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Solid Rounded Outer Border" };
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }

            int perimeterCount = cornerSegments * 4;
            var vertices = new Vector3[perimeterCount * 2 + 2];
            var triangles = new int[perimeterCount * 12];
            int topCenter = perimeterCount * 2;
            int bottomCenter = topCenter + 1;
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            vertices[topCenter] = new Vector3(centerX, topY, centerZ);
            vertices[bottomCenter] = new Vector3(centerX, bottomY, centerZ);

            for (int i = 0; i < perimeterCount; i++)
            {
                float angle = Mathf.PI * 2f * i / perimeterCount;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 corner = RoundedCornerCenter(minX, minZ, maxX, maxZ, radius, direction);
                Vector2 point = corner + direction * radius;
                vertices[i * 2] = new Vector3(point.x, topY, point.y);
                vertices[i * 2 + 1] = new Vector3(point.x, bottomY, point.y);
            }

            for (int i = 0; i < perimeterCount; i++)
            {
                int next = (i + 1) % perimeterCount;
                int top = i * 2;
                int bottom = top + 1;
                int nextTop = next * 2;
                int nextBottom = nextTop + 1;
                int triangle = i * 12;

                // Solid top and bottom close the middle of the plate.
                triangles[triangle] = topCenter;
                triangles[triangle + 1] = nextTop;
                triangles[triangle + 2] = top;
                triangles[triangle + 3] = bottomCenter;
                triangles[triangle + 4] = bottom;
                triangles[triangle + 5] = nextBottom;
                AddQuad(triangles, triangle + 6, top, nextTop, nextBottom, bottom);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 RoundedCornerCenter(float minX, float minZ,
            float maxX, float maxZ, float radius, Vector2 direction)
        {
            return new Vector2(direction.x >= 0f ? maxX - radius : minX + radius,
                direction.y >= 0f ? maxZ - radius : minZ + radius);
        }

        private static void AddQuad(int[] triangles, int start, int a, int b, int c, int d)
        {
            triangles[start] = a;
            triangles[start + 1] = b;
            triangles[start + 2] = c;
            triangles[start + 3] = a;
            triangles[start + 4] = c;
            triangles[start + 5] = d;
        }

    }
}
