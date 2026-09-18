using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PixelBoard))]
    public sealed class PixelBoardRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private struct InstanceData
        {
            public Matrix4x4 objectToWorld;
        }

        [SerializeField] private PixelBoard board;
        [SerializeField] private Mesh pixelMesh;
        [SerializeField] private Material pixelMaterial;
        [SerializeField] private Vector3 pixelScale = new Vector3(0.9f, 0.45f, 0.9f);
        [SerializeField] private float borderWidth = 0.14f;
        [SerializeField] private ShadowCastingMode castShadows = ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;

        private List<InstanceData>[] instancesByColor;
        private readonly List<InstanceData> borderInstances = new List<InstanceData>();
        private List<int>[] boardIndicesByColor;
        private Dictionary<int, int>[] slotsByColor;
        private Material runtimeMaterial;
        private MaterialPropertyBlock[] colorBlocks;
        private int observedRevision = -1;
        private Matrix4x4 observedLocalToWorld;

        private void Awake()
        {
            CreateRuntimeResources();
        }

        private void OnEnable()
        {
            if (board == null)
                return;

            board.BoardBuilt += Rebuild;
            board.PixelCollected += OnPixelCollected;
            if (board.IsBuilt)
                Rebuild();
        }

        private void OnDisable()
        {
            if (board == null)
                return;

            board.BoardBuilt -= Rebuild;
            board.PixelCollected -= OnPixelCollected;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        private void LateUpdate()
        {
            if (board == null || !board.IsBuilt || pixelMesh == null || runtimeMaterial == null)
                return;

            Matrix4x4 currentLocalToWorld = transform.localToWorldMatrix;
            if (observedRevision != board.Revision || currentLocalToWorld != observedLocalToWorld)
                Rebuild();

            DrawInstances();
        }

        private void CreateRuntimeResources()
        {
            int colorCount = Enum.GetValues(typeof(PixelColor)).Length;
            instancesByColor = new List<InstanceData>[colorCount];
            boardIndicesByColor = new List<int>[colorCount];
            slotsByColor = new Dictionary<int, int>[colorCount];
            colorBlocks = new MaterialPropertyBlock[colorCount];

            for (int i = 0; i < colorCount; i++)
            {
                instancesByColor[i] = new List<InstanceData>();
                boardIndicesByColor[i] = new List<int>();
                slotsByColor[i] = new Dictionary<int, int>();
                colorBlocks[i] = new MaterialPropertyBlock();
                UnityEngine.Color tint = PixelColorUtility.ToUnityColor((PixelColor)i);
                colorBlocks[i].SetColor(BaseColorId, tint);
                colorBlocks[i].SetColor(ColorId, tint);
            }

            if (pixelMaterial != null)
            {
                runtimeMaterial = new Material(pixelMaterial)
                {
                    name = $"{pixelMaterial.name} (GPU Instanced)",
                    enableInstancing = true
                };
            }
        }

        private void Rebuild()
        {
            if (instancesByColor == null || board == null)
                return;

            foreach (List<InstanceData> list in instancesByColor)
                list.Clear();
            foreach (List<int> list in boardIndicesByColor)
                list.Clear();
            foreach (Dictionary<int, int> lookup in slotsByColor)
                lookup.Clear();
            borderInstances.Clear();

            if (!board.IsBuilt)
            {
                observedRevision = board.Revision;
                return;
            }

            observedLocalToWorld = transform.localToWorldMatrix;
            Vector2Int boardSize = board.Size;
            for (int y = 0; y < boardSize.y; y++)
            {
                for (int x = 0; x < boardSize.x; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (!board.TryGetCell(position, out PixelCell cell) || !cell.IsOccupied)
                        continue;

                    int colorIndex = (int)cell.Color;
                    int boardIndex = y * boardSize.x + x;
                    slotsByColor[colorIndex][boardIndex] = instancesByColor[colorIndex].Count;
                    boardIndicesByColor[colorIndex].Add(boardIndex);
                    instancesByColor[colorIndex].Add(new InstanceData
                    {
                        objectToWorld = observedLocalToWorld * Matrix4x4.TRS(
                            board.GridToLocalPosition(position), Quaternion.identity,
                            pixelScale * board.CellSize)
                    });
                }
            }

            float halfBorderOffset = 0.55f * board.CellSize;
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector3 cell = board.GridToLocalPosition(new Vector2Int(x, 0));
                AddBorderInstance(new Vector3(cell.x, 0f, -halfBorderOffset),
                    new Vector3(0.96f, 0.08f, borderWidth) * board.CellSize);
                AddBorderInstance(new Vector3(cell.x, 0f,
                        (boardSize.y - 1) * board.CellSize + halfBorderOffset),
                    new Vector3(0.96f, 0.08f, borderWidth) * board.CellSize);
            }
            for (int y = 0; y < boardSize.y; y++)
            {
                Vector3 cell = board.GridToLocalPosition(new Vector2Int(0, y));
                AddBorderInstance(new Vector3(-halfBorderOffset, 0f, cell.z),
                    new Vector3(borderWidth, 0.08f, 0.96f) * board.CellSize);
                AddBorderInstance(new Vector3(
                        (boardSize.x - 1) * board.CellSize + halfBorderOffset, 0f, cell.z),
                    new Vector3(borderWidth, 0.08f, 0.96f) * board.CellSize);
            }

            observedRevision = board.Revision;
        }

        private void OnPixelCollected(Vector2Int position, PixelColor color)
        {
            int colorIndex = (int)color;
            int boardIndex = position.y * board.Size.x + position.x;
            Dictionary<int, int> lookup = slotsByColor[colorIndex];
            if (!lookup.TryGetValue(boardIndex, out int removedSlot))
            {
                observedRevision = -1;
                return;
            }

            List<InstanceData> instances = instancesByColor[colorIndex];
            List<int> boardIndices = boardIndicesByColor[colorIndex];
            int lastSlot = instances.Count - 1;
            if (removedSlot != lastSlot)
            {
                instances[removedSlot] = instances[lastSlot];
                int swappedBoardIndex = boardIndices[lastSlot];
                boardIndices[removedSlot] = swappedBoardIndex;
                lookup[swappedBoardIndex] = removedSlot;
            }

            instances.RemoveAt(lastSlot);
            boardIndices.RemoveAt(lastSlot);
            lookup.Remove(boardIndex);
            observedRevision = board.Revision;
        }

        private void DrawInstances()
        {
            DrawBatch(borderInstances, colorBlocks[(int)PixelColor.White]);
            for (int colorIndex = 0; colorIndex < instancesByColor.Length; colorIndex++)
            {
                List<InstanceData> instances = instancesByColor[colorIndex];
                if (instances.Count == 0)
                    continue;
                DrawBatch(instances, colorBlocks[colorIndex]);
            }
        }

        private void AddBorderInstance(Vector3 localPosition, Vector3 scale)
        {
            borderInstances.Add(new InstanceData
            {
                objectToWorld = observedLocalToWorld * Matrix4x4.TRS(
                    localPosition + Vector3.down * 0.25f, Quaternion.identity, scale)
            });
        }

        private void DrawBatch(List<InstanceData> instances, MaterialPropertyBlock properties)
        {
            if (instances.Count == 0)
                return;
            var renderParams = new RenderParams(runtimeMaterial)
            {
                matProps = properties,
                shadowCastingMode = castShadows,
                receiveShadows = receiveShadows,
                layer = gameObject.layer
            };
            for (int start = 0; start < instances.Count; start += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, instances.Count - start);
                Graphics.RenderMeshInstanced(renderParams, pixelMesh, 0, instances, count, start);
            }
        }

    }
}
