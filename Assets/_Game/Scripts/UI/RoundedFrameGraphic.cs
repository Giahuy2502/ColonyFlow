using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class RoundedFrameGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float cornerRadius = 36f;
        [SerializeField, Min(1f)] private float thickness = 8f;
        [SerializeField, Range(2, 12)] private int cornerSegments = 6;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
            float innerRadius = Mathf.Max(0f, radius - thickness);
            Rect inner = new Rect(rect.xMin + thickness, rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f), Mathf.Max(0f, rect.height - thickness * 2f));
            int count = cornerSegments * 4;

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 outerCenter = CornerCenter(rect, direction, radius);
                Vector2 innerCenter = CornerCenter(inner, direction, innerRadius);
                AddVertex(vh, outerCenter + direction * radius);
                AddVertex(vh, innerCenter + direction * innerRadius);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int outer = i * 2;
                int innerIndex = outer + 1;
                int nextOuter = next * 2;
                int nextInner = nextOuter + 1;
                vh.AddTriangle(outer, nextOuter, nextInner);
                vh.AddTriangle(outer, nextInner, innerIndex);
            }
        }

        private static Vector2 CornerCenter(Rect rect, Vector2 direction, float radius)
        {
            return new Vector2(direction.x >= 0f ? rect.xMax - radius : rect.xMin + radius,
                direction.y >= 0f ? rect.yMax - radius : rect.yMin + radius);
        }

        private void AddVertex(VertexHelper vh)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = Vector3.zero;
            vh.AddVert(vertex);
        }

        private void AddVertex(VertexHelper vh, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}
