using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class AntManager : MonoBehaviour
    {
        [SerializeField] private Ant antPrefab;
        [SerializeField, Min(1)] private int initialPoolSize = 32;
        [SerializeField, Min(1)] private int maxActiveAnts = 64;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.35f;
        [SerializeField, Min(0f)] private float movementHeight;

        private readonly Queue<Ant> available = new Queue<Ant>();
        private readonly HashSet<Ant> active = new HashSet<Ant>();
        private readonly List<Colony> colonies = new List<Colony>();
        private readonly Dictionary<Colony, float> nextSpawnTimeByColony =
            new Dictionary<Colony, float>();
        private readonly List<Vector2Int> gridRoute = new List<Vector2Int>(128);
        private readonly List<Vector3> worldRoute = new List<Vector3>(128);
        private PixelBoard board;
        private ColonyGameplayController controller;
        private Transform holeTarget;
        private int nextColonyIndex;

        public int ActiveCount => active.Count;
        public int PooledCount => available.Count;

        public void Configure(PixelBoard pixelBoard, ColonyGameplayController gameplayController,
            Transform antHoleTarget)
        {
            board = pixelBoard;
            controller = gameplayController;
            holeTarget = antHoleTarget;
            nextSpawnTimeByColony.Clear();
            Prewarm();
        }

        private void Update()
        {
            if (board == null || controller == null || controller.State != LevelState.Playing ||
                active.Count >= maxActiveAnts)
                return;

            TryDispatchOneAnt();
        }

        private void Prewarm()
        {
            if (antPrefab == null)
            {
                Debug.LogError("AntManager requires an Ant prefab.", this);
                enabled = false;
                return;
            }

            for (int i = available.Count + active.Count; i < initialPoolSize; i++)
                available.Enqueue(CreateAnt());
        }

        private Ant CreateAnt()
        {
            Ant ant = Instantiate(antPrefab, transform);
            ant.name = "Ant";
            ant.PrepareForPool();
            return ant;
        }

        private void TryDispatchOneAnt()
        {
            controller.CopyActiveColonies(colonies);
            if (colonies.Count == 0)
                return;

            for (int attempt = 0; attempt < colonies.Count; attempt++)
            {
                int index = (nextColonyIndex + attempt) % colonies.Count;
                Colony colony = colonies[index];
                if (nextSpawnTimeByColony.TryGetValue(colony, out float nextSpawnTime) &&
                    Time.time < nextSpawnTime)
                    continue;

                Vector3 colonyPosition = colony.transform.localPosition;
                Vector2Int borderStart = board.GetClosestBottomBorder(colonyPosition.x);
                if (!controller.TryCreateTask(colony, borderStart, gridRoute,
                        out ColonyTask task))
                    continue;

                nextColonyIndex = (index + 1) % colonies.Count;
                nextSpawnTimeByColony[colony] = Time.time + spawnInterval;
                Ant ant = available.Count > 0 ? available.Dequeue() : CreateAnt();
                active.Add(ant);

                Vector3 target = board.GridToLocalPosition(task.Target) + Vector3.up * movementHeight;
                Vector3 spawnPosition = new Vector3(
                    colonyPosition.x, movementHeight, colonyPosition.z);
                BuildOutboundRoute(spawnPosition, borderStart);
                ant.Launch(this, task, spawnPosition, worldRoute, target);
                return;
            }
        }

        internal void NotifyTargetReached(Ant ant, ColonyTask task)
        {
            if (ant == null || !active.Contains(ant))
                return;

            if (!controller.CompleteTask(task.Id) ||
                !board.TryBuildReturnPath(task.Target, gridRoute))
            {
                active.Remove(ant);
                ant.ReturnToPool();
                available.Enqueue(ant);
                return;
            }

            worldRoute.Clear();
            for (int i = 1; i < gridRoute.Count; i++)
                worldRoute.Add(ToMovementPosition(gridRoute[i]));
            AppendHoleApproach();
            ant.BeginReturn(worldRoute);
        }

        internal void NotifyEnteredHole(Ant ant)
        {
            if (ant == null || !active.Remove(ant))
                return;

            ant.ReturnToPool();
            available.Enqueue(ant);
        }

        private void BuildOutboundRoute(Vector3 spawnPosition, Vector2Int borderStart)
        {
            worldRoute.Clear();
            Vector3 borderEntrance = ToMovementPosition(borderStart);
            Vector3 straightStep = new Vector3(spawnPosition.x, movementHeight, borderEntrance.z);
            if ((straightStep - spawnPosition).sqrMagnitude > 0.0001f)
                worldRoute.Add(straightStep);
            if ((borderEntrance - straightStep).sqrMagnitude > 0.0001f)
                worldRoute.Add(borderEntrance);

            for (int i = 1; i < gridRoute.Count; i++)
                worldRoute.Add(ToMovementPosition(gridRoute[i]));
        }

        private Vector3 ToMovementPosition(Vector2Int gridPosition)
        {
            return board.GridToLocalPosition(gridPosition) + Vector3.up * movementHeight;
        }

        private Vector3 GetHolePosition()
        {
            if (holeTarget != null)
            {
                Vector3 position = transform.InverseTransformPoint(holeTarget.position);
                position.y = movementHeight;
                return position;
            }

            Vector3 fallback = ToMovementPosition(board.GetBorderEntrance());
            fallback.z -= board.CellSize * 0.65f;
            return fallback;
        }

        private void AppendHoleApproach()
        {
            Vector3 holePosition = GetHolePosition();
            Vector3 lastPosition = worldRoute.Count > 0
                ? worldRoute[worldRoute.Count - 1]
                : ToMovementPosition(board.GetBorderEntrance());
            Vector3 alignedWithHole = new Vector3(holePosition.x, movementHeight, lastPosition.z);

            if ((alignedWithHole - lastPosition).sqrMagnitude > 0.0001f)
                worldRoute.Add(alignedWithHole);
            if ((holePosition - alignedWithHole).sqrMagnitude > 0.0001f)
                worldRoute.Add(holePosition);
        }

        public void CancelAll()
        {
            var snapshot = new List<Ant>(active);
            foreach (Ant ant in snapshot)
            {
                controller.CancelTask(ant.TaskId);
                active.Remove(ant);
                ant.ReturnToPool();
                available.Enqueue(ant);
            }
        }
    }
}
