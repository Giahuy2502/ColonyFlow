using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class AntManager : Singleton<AntManager>
    {
        [SerializeField, Min(1)] private int maxActiveAnts = 64;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.35f;
        [SerializeField, Min(0f)] private float movementHeight;
        [SerializeField, Min(0.05f)] private float holeAvoidanceRadiusX = 0.27f;
        [SerializeField, Min(0.05f)] private float holeAvoidanceRadiusZ = 0.18f;
        [SerializeField, Min(0f)] private float holeAvoidancePadding = 0.5f;
        [SerializeField, Range(3, 8)] private int holeDetourSegments = 5;
        [Header("Path smoothing")]
        [SerializeField, Range(0f, 0.5f)] private float outsideTurnRadius = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float insideTurnRadius = 0.06f;
        [SerializeField, Range(2, 16)] private int turnSegments = 8;

        private readonly HashSet<Ant> active = new HashSet<Ant>();
        private readonly List<Colony> colonies = new List<Colony>();
        private readonly Dictionary<Colony, float> nextSpawnTimeByColony =
            new Dictionary<Colony, float>();
        private readonly List<Vector2Int> gridRoute = new List<Vector2Int>(128);
        private readonly List<Vector3> worldRoute = new List<Vector3>(128);
        private readonly List<Vector3> smoothedRoute = new List<Vector3>(256);
        private readonly List<Vector3> rawRouteScratch = new List<Vector3>(128);
        private readonly List<Vector3> pathScratch = new List<Vector3>(128);
        private PixelBoard board;
        private LevelManager levelManager;
        private Transform holeTarget;
        private Transform antRoot;
        private int nextColonyIndex;

        public int ActiveCount => active.Count;

        public void OnInit(PixelBoard pixelBoard, LevelManager gameplayLevelManager,
            Transform antHoleTarget, Transform spawnedAntRoot)
        {
            board = pixelBoard;
            levelManager = gameplayLevelManager;
            holeTarget = antHoleTarget;
            antRoot = spawnedAntRoot;
            nextSpawnTimeByColony.Clear();
        }

        private void Update()
        {
            if (board == null || levelManager == null ||
                !levelManager.CanProcessGameplay ||
                active.Count >= maxActiveAnts)
                return;

            TryDispatchOneAnt();
        }

        private void TryDispatchOneAnt()
        {
            levelManager.CopyActiveColonies(colonies);
            if (colonies.Count == 0)
                return;

            for (int attempt = 0; attempt < colonies.Count; attempt++)
            {
                int index = (nextColonyIndex + attempt) % colonies.Count;
                Colony colony = colonies[index];
                if (nextSpawnTimeByColony.TryGetValue(colony, out float nextSpawnTime) &&
                    Time.time < nextSpawnTime)
                    continue;

                Vector3 colonyWorldPosition = colony.transform.position;
                Vector3 colonyBoardPosition =
                    board.transform.InverseTransformPoint(colonyWorldPosition);
                Vector2Int borderStart =
                    board.GetClosestBottomBorder(colonyBoardPosition.x);
                if (!levelManager.TryCreateTask(colony, borderStart, gridRoute,
                        out ColonyTask task))
                    continue;

                nextColonyIndex = (index + 1) % colonies.Count;
                nextSpawnTimeByColony[colony] = Time.time + spawnInterval;
                Ant ant = SimplePool.Spawn<Ant>(PoolType.Ant, antRoot.position, antRoot.rotation);
                if (ant == null)
                {
                    levelManager.CancelTask(task.Id);
                    return;
                }
                active.Add(ant);

                Vector3 target = ToMovementPosition(task.Target);
                Vector3 spawnPosition =
                    antRoot.InverseTransformPoint(colonyWorldPosition);
                spawnPosition.y = movementHeight;
                BuildOutboundRoute(spawnPosition, borderStart);
                AppendTargetContactEndpoint(ant, spawnPosition, target);
                BuildSmoothedRoute(spawnPosition, Vector3.forward, false, task.Id);
                ant.OnInit(this, task, spawnPosition, smoothedRoute, target);
                return;
            }
        }

        internal void NotifyTargetReached(Ant ant, ColonyTask task)
        {
            if (ant == null || !active.Contains(ant))
                return;

            if (!levelManager.CompleteTask(task.Id) ||
                !board.TryBuildReturnPath(task.Target, gridRoute))
            {
                active.Remove(ant);
                ant.OnDespawn();
                SimplePool.Despawn(ant);
                return;
            }

            worldRoute.Clear();
            for (int i = 1; i < gridRoute.Count; i++)
            {
                worldRoute.Add(ToMovementPosition(gridRoute[i]));
                if (gridRoute[i].y == -1)
                    break;
            }
            Vector3 holePosition = AppendHoleEdgeApproach(ant);
            Vector3 forward = ant.transform.localRotation * Vector3.forward;
            BuildSmoothedRoute(ant.transform.localPosition, forward, true, task.Id);
            ant.BeginReturn(smoothedRoute, holePosition);
        }

        internal void NotifyEnteredHole(Ant ant)
        {
            if (ant == null || !active.Remove(ant))
                return;

            ant.OnDespawn();
            SimplePool.Despawn(ant);
            levelManager.ReevaluateProgress();
        }

        private void BuildOutboundRoute(Vector3 spawnPosition, Vector2Int borderStart)
        {
            worldRoute.Clear();
            Vector3 borderEntrance = ToMovementPosition(borderStart);
            Vector3 straightStep = new Vector3(spawnPosition.x, movementHeight, borderEntrance.z);
            AppendSegmentAvoidingHole(spawnPosition, straightStep);
            AppendSegmentAvoidingHole(straightStep, borderEntrance);

            for (int i = 1; i < gridRoute.Count; i++)
                worldRoute.Add(ToMovementPosition(gridRoute[i]));
        }

        private Vector3 ToMovementPosition(Vector2Int gridPosition)
        {
            Vector3 worldPosition = board.transform.TransformPoint(
                board.GridToLocalPosition(gridPosition));
            Vector3 antLocalPosition = antRoot.InverseTransformPoint(worldPosition);
            antLocalPosition.y = movementHeight;
            return antLocalPosition;
        }

        private void AppendTargetContactEndpoint(Ant ant, Vector3 spawnPosition,
            Vector3 targetCenter)
        {
            Vector3 approachStart = worldRoute.Count > 0
                ? worldRoute[worldRoute.Count - 1]
                : spawnPosition;
            Vector3 approach = targetCenter - approachStart;
            approach.y = 0f;
            float approachLength = approach.magnitude;
            if (approachLength <= 0.0001f)
            {
                AddUnique(worldRoute, targetCenter);
                return;
            }

            float headReach = ant != null ? ant.HeadReach : 0f;
            float desiredStopDistance = board.CellSize * 0.5f + headReach;
            Vector3 contactEndpoint = targetCenter -
                approach / approachLength * desiredStopDistance;
            contactEndpoint.y = movementHeight;

            // The grid route already ends in the empty cell next to the target.
            // Replace that cell centre with the actual contact point. Appending it
            // would create a tiny or backwards segment when Head Reach is large;
            // that segment was discarded by AddUnique and left the ant facing the
            // preceding corner instead of the pixel.
            if (worldRoute.Count > 0)
                worldRoute[worldRoute.Count - 1] = contactEndpoint;
            else
                AddUnique(worldRoute, contactEndpoint);

            RemoveDuplicateRouteEnd();
        }

        private void RemoveDuplicateRouteEnd()
        {
            while (worldRoute.Count > 1 &&
                   (worldRoute[worldRoute.Count - 1] -
                    worldRoute[worldRoute.Count - 2]).sqrMagnitude <= 0.000001f)
            {
                worldRoute.RemoveAt(worldRoute.Count - 2);
            }
        }

        private Vector3 GetHolePosition()
        {
            if (holeTarget != null)
            {
                Vector3 position = antRoot.InverseTransformPoint(holeTarget.position);
                position.y = movementHeight;
                return position;
            }

            Vector3 fallback = ToMovementPosition(board.GetBorderEntrance());
            fallback.z -= board.CellSize * 0.65f;
            return fallback;
        }

        private Vector3 AppendHoleEdgeApproach(Ant ant)
        {
            Vector3 holePosition = GetHolePosition();
            Vector3 lastPosition = worldRoute.Count > 0
                ? worldRoute[worldRoute.Count - 1]
                : ToMovementPosition(board.GetBorderEntrance());
            Vector3 fromHole = lastPosition - holePosition;
            fromHole.y = 0f;
            if (fromHole.sqrMagnitude > 0.0001f)
            {
                Vector2 direction = new Vector2(fromHole.x, fromHole.z).normalized;
                float jumpDistance = ant != null ? ant.JumpStartDistanceFromHole : 0f;
                float jumpRadiusX = holeAvoidanceRadiusX + jumpDistance;
                float jumpRadiusZ = holeAvoidanceRadiusZ + jumpDistance;
                float denominator = Mathf.Sqrt(
                    direction.x * direction.x / (jumpRadiusX * jumpRadiusX) +
                    direction.y * direction.y / (jumpRadiusZ * jumpRadiusZ));
                float edgeDistance = denominator > 0.0001f ? 1f / denominator : jumpRadiusZ;
                Vector3 edge = holePosition + new Vector3(direction.x, 0f, direction.y) * edgeDistance;
                if ((edge - lastPosition).sqrMagnitude > 0.0001f)
                    worldRoute.Add(edge);
            }
            return holePosition;
        }

        private void AppendSegmentAvoidingHole(Vector3 from, Vector3 to)
        {
            if ((to - from).sqrMagnitude <= 0.0001f)
                return;

            Vector3 hole = GetHolePosition();
            float radiusX = holeAvoidanceRadiusX + holeAvoidancePadding;
            float radiusZ = holeAvoidanceRadiusZ + holeAvoidancePadding;
            if (!SegmentIntersectsHole(from, to, hole, Mathf.Max(radiusX, radiusZ)))
            {
                worldRoute.Add(to);
                return;
            }

            bool vertical = Mathf.Abs(to.z - from.z) >= Mathf.Abs(to.x - from.x);
            if (vertical)
            {
                float direction = Mathf.Sign(to.z - from.z);
                float side = to.x < hole.x ? -1f : 1f;
                if (Mathf.Approximately(to.x, hole.x))
                    side = from.x <= hole.x ? -1f : 1f;

                for (int i = 0; i <= holeDetourSegments; i++)
                {
                    float phase = i / (float)holeDetourSegments * Mathf.PI;
                    worldRoute.Add(new Vector3(
                        hole.x + side * Mathf.Sin(phase) * radiusX,
                        movementHeight,
                        hole.z - direction * Mathf.Cos(phase) * radiusZ));
                }
            }
            else
            {
                float direction = Mathf.Sign(to.x - from.x);
                float side = from.z <= hole.z ? -1f : 1f;
                for (int i = 0; i <= holeDetourSegments; i++)
                {
                    float phase = i / (float)holeDetourSegments * Mathf.PI;
                    worldRoute.Add(new Vector3(
                        hole.x - direction * Mathf.Cos(phase) * radiusX,
                        movementHeight,
                        hole.z + side * Mathf.Sin(phase) * radiusZ));
                }
            }
            worldRoute.Add(to);
        }

        private static bool SegmentIntersectsHole(Vector3 from, Vector3 to,
            Vector3 hole, float radius)
        {
            Vector2 start = new Vector2(from.x, from.z);
            Vector2 end = new Vector2(to.x, to.z);
            Vector2 center = new Vector2(hole.x, hole.z);
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return (start - center).sqrMagnitude < radius * radius;

            float t = Mathf.Clamp01(Vector2.Dot(center - start, segment) / lengthSquared);
            Vector2 closest = start + segment * t;
            return (closest - center).sqrMagnitude < radius * radius;
        }

        private void BuildSmoothedRoute(Vector3 startPosition, Vector3 startForward,
            bool smoothInitialReverse, int taskId)
        {
            smoothedRoute.Clear();
            rawRouteScratch.Clear();
            startPosition.y = movementHeight;
            AddUnique(rawRouteScratch, startPosition);
            for (int i = 0; i < worldRoute.Count; i++)
            {
                Vector3 waypoint = worldRoute[i];
                waypoint.y = movementHeight;
                AddUnique(rawRouteScratch, waypoint);
            }

            CompressCollinearPath(rawRouteScratch, pathScratch);
            if (pathScratch.Count < 2)
                return;

            int roundedPathStart = 0;
            if (smoothInitialReverse &&
                TryAppendInitialUTurn(startForward, taskId))
                roundedPathStart = 1;

            AppendRoundedPath(roundedPathStart);
        }

        private static void CompressCollinearPath(List<Vector3> source,
            List<Vector3> destination)
        {
            destination.Clear();
            if (source == null || source.Count == 0)
                return;

            AddUnique(destination, source[0]);
            for (int i = 1; i < source.Count - 1; i++)
            {
                Vector3 previous = destination[destination.Count - 1];
                Vector3 current = source[i];
                Vector3 next = source[i + 1];
                Vector3 incoming = current - previous;
                Vector3 outgoing = next - current;
                incoming.y = 0f;
                outgoing.y = 0f;

                float incomingLength = incoming.magnitude;
                float outgoingLength = outgoing.magnitude;
                if (incomingLength <= 0.0001f || outgoingLength <= 0.0001f)
                    continue;

                float directionDot = Vector3.Dot(
                    incoming / incomingLength, outgoing / outgoingLength);
                if (directionDot > 0.9999f)
                    continue;

                AddUnique(destination, current);
            }

            AddUnique(destination, source[source.Count - 1]);
        }

        private bool TryAppendInitialUTurn(Vector3 startForward, int taskId)
        {
            Vector3 start = pathScratch[0];
            Vector3 first = pathScratch[1];
            Vector3 toFirst = first - start;
            toFirst.y = 0f;
            startForward.y = 0f;
            if (toFirst.sqrMagnitude <= 0.0001f || startForward.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 forward = startForward.normalized;
            Vector3 firstDirection = toFirst.normalized;
            if (Vector3.Dot(forward, firstDirection) > -0.35f)
                return false;

            float desiredRadius = GetTurnRadius(start, first, first);
            float radius = Mathf.Min(desiredRadius, toFirst.magnitude * 0.45f);
            if (radius <= 0.0001f)
                return false;

            Vector3 endDirection = firstDirection;
            if (pathScratch.Count > 2)
            {
                Vector3 nextDirection = pathScratch[2] - first;
                nextDirection.y = 0f;
                if (nextDirection.sqrMagnitude > 0.0001f)
                    endDirection = nextDirection.normalized;
            }

            float sideSign = (taskId & 1) == 0 ? 1f : -1f;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized * sideSign;
            Vector3 control1 = start + forward * radius + side * radius;
            Vector3 control2 = first - endDirection * radius + side * radius;
            int segments = Mathf.Max(2, turnSegments);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                AddUnique(smoothedRoute,
                    CubicBezier(start, control1, control2, first, t));
            }
            return true;
        }

        private void AppendRoundedPath(int startIndex)
        {
            if (startIndex >= pathScratch.Count - 1)
                return;

            for (int i = startIndex + 1; i < pathScratch.Count - 1; i++)
            {
                Vector3 previous = pathScratch[i - 1];
                Vector3 corner = pathScratch[i];
                Vector3 next = pathScratch[i + 1];
                Vector3 incoming = corner - previous;
                Vector3 outgoing = next - corner;
                incoming.y = 0f;
                outgoing.y = 0f;

                float incomingLength = incoming.magnitude;
                float outgoingLength = outgoing.magnitude;
                if (incomingLength <= 0.0001f || outgoingLength <= 0.0001f)
                    continue;

                Vector3 incomingDirection = incoming / incomingLength;
                Vector3 outgoingDirection = outgoing / outgoingLength;
                float directionDot = Mathf.Clamp(
                    Vector3.Dot(incomingDirection, outgoingDirection), -1f, 1f);
                if (directionDot > 0.999f)
                    continue;
                if (directionDot < -0.999f)
                {
                    AddUnique(smoothedRoute, corner);
                    continue;
                }

                float desiredRadius = GetTurnRadius(previous, corner, next);
                float turnAngle = Mathf.Acos(directionDot);
                float tangentScale = Mathf.Tan(turnAngle * 0.5f);
                if (desiredRadius <= 0.0001f || tangentScale <= 0.0001f)
                {
                    AddUnique(smoothedRoute, corner);
                    continue;
                }

                float tangentDistance = Mathf.Min(desiredRadius * tangentScale,
                    Mathf.Min(incomingLength, outgoingLength) * 0.45f);
                float effectiveRadius = tangentDistance / tangentScale;
                Vector3 entry = corner - incomingDirection * tangentDistance;
                Vector3 exit = corner + outgoingDirection * tangentDistance;
                float turnSign = Mathf.Sign(
                    Vector3.Cross(incomingDirection, outgoingDirection).y);
                Vector3 center = entry +
                    Vector3.Cross(Vector3.up, incomingDirection) *
                    (effectiveRadius * turnSign);
                Vector3 startRadial = entry - center;
                Vector3 endRadial = exit - center;

                AddUnique(smoothedRoute, entry);
                float angleDegrees = turnAngle * Mathf.Rad2Deg;
                int segments = Mathf.Max(2,
                    Mathf.CeilToInt(turnSegments * angleDegrees / 90f));
                for (int segment = 1; segment <= segments; segment++)
                {
                    float t = segment / (float)segments;
                    Vector3 radial = Vector3.Slerp(startRadial, endRadial, t);
                    AddUnique(smoothedRoute, center + radial);
                }
            }

            AddUnique(smoothedRoute, pathScratch[pathScratch.Count - 1]);
        }

        private float GetTurnRadius(Vector3 previous, Vector3 corner, Vector3 next)
        {
            return IsOutsideBoard(previous) && IsOutsideBoard(corner) && IsOutsideBoard(next)
                ? outsideTurnRadius
                : insideTurnRadius;
        }

        private bool IsOutsideBoard(Vector3 position)
        {
            float maxX = (board.Size.x - 1) * board.CellSize;
            float maxZ = (board.Size.y - 1) * board.CellSize;
            return position.x < 0f || position.z < 0f ||
                   position.x > maxX || position.z > maxZ;
        }

        private static Vector3 CubicBezier(Vector3 start, Vector3 control1,
            Vector3 control2, Vector3 end, float t)
        {
            float inverse = 1f - t;
            float inverseSquared = inverse * inverse;
            float tSquared = t * t;
            return inverseSquared * inverse * start +
                   3f * inverseSquared * t * control1 +
                   3f * inverse * tSquared * control2 +
                   tSquared * t * end;
        }

        private static void AddUnique(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 ||
                (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
                points.Add(point);
        }

        public void CancelAll()
        {
            var snapshot = new List<Ant>(active);
            foreach (Ant ant in snapshot)
            {
                if (ant == null)
                {
                    active.Remove(ant);
                    continue;
                }
                levelManager?.CancelTask(ant.TaskId);
                active.Remove(ant);
                ant.OnDespawn();
                SimplePool.Despawn(ant);
            }
        }

        public int CancelByColor(PixelColor color)
        {
            int cancelled = 0;
            var snapshot = new List<Ant>(active);
            foreach (Ant ant in snapshot)
            {
                if (ant == null)
                {
                    active.Remove(ant);
                    continue;
                }
                if (!ant.IsTaskColor(color))
                    continue;

                levelManager?.CancelTask(ant.TaskId, false);
                active.Remove(ant);
                ant.OnDespawn();
                SimplePool.Despawn(ant);
                cancelled++;
            }
            return cancelled;
        }

        public void Shutdown()
        {
            CancelAll();
            nextSpawnTimeByColony.Clear();
            colonies.Clear();
            gridRoute.Clear();
            worldRoute.Clear();
            smoothedRoute.Clear();
            board = null;
            levelManager = null;
            holeTarget = null;
            antRoot = null;
        }
    }
}
