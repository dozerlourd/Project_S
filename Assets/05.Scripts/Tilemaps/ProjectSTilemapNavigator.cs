using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Tilemaps
{
    [RequireComponent(typeof(ProjectSTilemapWorld))]
    public sealed class ProjectSTilemapNavigator : MonoBehaviour
    {
        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private bool allowDiagonalMovement = true;
        [SerializeField] private int maxExpandedCells = 4096;

        private static readonly Vector3Int[] CardinalDirections =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        private static readonly Vector3Int[] DiagonalDirections =
        {
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        private readonly CellPriorityQueue open = new CellPriorityQueue();
        private readonly HashSet<Vector3Int> closed = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        private readonly Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float>();
        private readonly List<Vector3Int> pathCells = new List<Vector3Int>();

        public static ProjectSTilemapNavigator ActiveInstance { get; private set; }

        public ProjectSTilemapWorld TilemapWorld => tilemapWorld;

        private void Awake()
        {
            ResolveReferences();
            ActiveInstance = this;
        }

        private void OnEnable()
        {
            ResolveReferences();
            ActiveInstance = this;
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public bool TryFindPath(Vector3 startWorld, Vector3 destinationWorld, List<Vector3> result)
        {
            return TryFindPath(startWorld, destinationWorld, result, null);
        }

        public bool TryFindPath(
            Vector3 startWorld,
            Vector3 destinationWorld,
            List<Vector3> result,
            ICollection<Vector3Int> blockedCells)
        {
            result?.Clear();
            ResolveReferences();
            if (tilemapWorld == null || result == null)
            {
                return false;
            }

            var start = tilemapWorld.WorldToCell(startWorld);
            var goal = tilemapWorld.WorldToCell(destinationWorld);
            if (!tilemapWorld.IsWalkable(start) || !tilemapWorld.IsWalkable(goal))
            {
                return false;
            }

            if (start == goal)
            {
                result.Add(tilemapWorld.GetCellCenterWorld(goal));
                return true;
            }

            ResetSearchState();
            var expandedCells = 0;
            gScore[start] = 0f;
            open.Enqueue(start, Heuristic(start, goal));

            while (open.Count > 0 && expandedCells < maxExpandedCells)
            {
                var current = open.Dequeue();
                if (closed.Contains(current))
                {
                    continue;
                }

                if (current == goal)
                {
                    BuildWorldPath(cameFrom, current, result);
                    return true;
                }

                closed.Add(current);
                expandedCells++;

                for (var i = 0; i < CardinalDirections.Length; i++)
                {
                    VisitNeighbor(current, current + CardinalDirections[i], start, goal, blockedCells);
                }

                if (!allowDiagonalMovement)
                {
                    continue;
                }

                for (var i = 0; i < DiagonalDirections.Length; i++)
                {
                    var neighbor = current + DiagonalDirections[i];
                    if (CanMoveDiagonally(current, neighbor, start, goal, blockedCells))
                    {
                        VisitNeighbor(current, neighbor, start, goal, blockedCells);
                    }
                }
            }

            return false;
        }

        public bool TryGetCommandPoint(Vector3 worldPosition, out Vector3 commandPoint)
        {
            ResolveReferences();
            if (tilemapWorld == null)
            {
                commandPoint = default;
                return false;
            }

            var cell = tilemapWorld.WorldToCell(worldPosition);
            if (!tilemapWorld.IsWalkable(cell))
            {
                commandPoint = default;
                return false;
            }

            commandPoint = tilemapWorld.GetCellCenterWorld(cell);
            return true;
        }

        public bool IsSegmentWalkable(Vector3 from, Vector3 to)
        {
            ResolveReferences();
            if (tilemapWorld == null)
            {
                return true;
            }

            var fromCell = tilemapWorld.WorldToCell(from);
            var toCell = tilemapWorld.WorldToCell(to);
            if (!tilemapWorld.IsWalkable(fromCell) || !tilemapWorld.IsWalkable(toCell))
            {
                return false;
            }

            if (fromCell == toCell)
            {
                return true;
            }

            var distance = Vector3.Distance(from, to);
            var sampleDistance = GetSegmentSampleDistance();
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleDistance));
            for (var i = 1; i < sampleCount; i++)
            {
                var point = Vector3.Lerp(from, to, i / (float)sampleCount);
                if (!tilemapWorld.IsWalkable(tilemapWorld.WorldToCell(point)))
                {
                    return false;
                }
            }

            return true;
        }

        private void ResolveReferences()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = GetComponent<ProjectSTilemapWorld>();
            }
        }

        private void ResetSearchState()
        {
            open.Clear();
            closed.Clear();
            cameFrom.Clear();
            gScore.Clear();
        }

        private void VisitNeighbor(
            Vector3Int current,
            Vector3Int neighbor,
            Vector3Int start,
            Vector3Int goal,
            ICollection<Vector3Int> blockedCells)
        {
            if (closed.Contains(neighbor)
                || !tilemapWorld.IsWalkable(neighbor)
                || IsDynamicallyBlocked(neighbor, start, goal, blockedCells))
            {
                return;
            }

            var tentativeScore = gScore[current] + GetStepCost(current, neighbor);
            if (gScore.TryGetValue(neighbor, out var knownScore) && tentativeScore >= knownScore)
            {
                return;
            }

            cameFrom[neighbor] = current;
            gScore[neighbor] = tentativeScore;
            var neighborScore = tentativeScore + Heuristic(neighbor, goal);
            open.Enqueue(neighbor, neighborScore);
        }

        private bool CanMoveDiagonally(
            Vector3Int from,
            Vector3Int to,
            Vector3Int start,
            Vector3Int goal,
            ICollection<Vector3Int> blockedCells)
        {
            var delta = to - from;
            var horizontal = from + new Vector3Int(delta.x, 0, 0);
            var vertical = from + new Vector3Int(0, delta.y, 0);
            return tilemapWorld.IsWalkable(horizontal)
                && tilemapWorld.IsWalkable(vertical)
                && !IsDynamicallyBlocked(horizontal, start, goal, blockedCells)
                && !IsDynamicallyBlocked(vertical, start, goal, blockedCells);
        }

        private static bool IsDynamicallyBlocked(
            Vector3Int cell,
            Vector3Int start,
            Vector3Int goal,
            ICollection<Vector3Int> blockedCells)
        {
            return cell != start
                && cell != goal
                && blockedCells != null
                && blockedCells.Contains(cell);
        }

        private float GetSegmentSampleDistance()
        {
            var cellSize = tilemapWorld.Grid != null ? tilemapWorld.Grid.cellSize : Vector3.one;
            var shortestAxis = Mathf.Min(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y));
            return Mathf.Max(0.05f, shortestAxis * 0.25f);
        }

        private float GetStepCost(Vector3Int from, Vector3Int to)
        {
            var diagonalMultiplier = IsDiagonalMove(from, to) ? 1.41421356f : 1f;
            return tilemapWorld.TrySample(to, out var sample)
                ? sample.MovementCost * diagonalMultiplier
                : diagonalMultiplier;
        }

        private void BuildWorldPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current, List<Vector3> result)
        {
            pathCells.Clear();
            pathCells.Add(current);
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                pathCells.Add(current);
            }

            for (var i = pathCells.Count - 1; i >= 0; i--)
            {
                result.Add(tilemapWorld.GetCellCenterWorld(pathCells[i]));
            }
        }

        private static bool IsDiagonalMove(Vector3Int from, Vector3Int to)
        {
            var delta = to - from;
            return Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1;
        }

        private static float Heuristic(Vector3Int from, Vector3Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        private sealed class CellPriorityQueue
        {
            private readonly List<Entry> entries = new List<Entry>();

            public int Count => entries.Count;

            public void Clear()
            {
                entries.Clear();
            }

            public void Enqueue(Vector3Int cell, float priority)
            {
                entries.Add(new Entry(cell, priority));
                SiftUp(entries.Count - 1);
            }

            public Vector3Int Dequeue()
            {
                var best = entries[0].Cell;
                var lastIndex = entries.Count - 1;
                entries[0] = entries[lastIndex];
                entries.RemoveAt(lastIndex);
                if (entries.Count > 0)
                {
                    SiftDown(0);
                }

                return best;
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (entries[parent].Priority <= entries[index].Priority)
                    {
                        break;
                    }

                    Swap(parent, index);
                    index = parent;
                }
            }

            private void SiftDown(int index)
            {
                while (true)
                {
                    var left = index * 2 + 1;
                    var right = left + 1;
                    var best = index;

                    if (left < entries.Count && entries[left].Priority < entries[best].Priority)
                    {
                        best = left;
                    }

                    if (right < entries.Count && entries[right].Priority < entries[best].Priority)
                    {
                        best = right;
                    }

                    if (best == index)
                    {
                        break;
                    }

                    Swap(index, best);
                    index = best;
                }
            }

            private void Swap(int a, int b)
            {
                var temp = entries[a];
                entries[a] = entries[b];
                entries[b] = temp;
            }

            private readonly struct Entry
            {
                public Entry(Vector3Int cell, float priority)
                {
                    Cell = cell;
                    Priority = priority;
                }

                public Vector3Int Cell { get; }
                public float Priority { get; }
            }
        }
    }
}
