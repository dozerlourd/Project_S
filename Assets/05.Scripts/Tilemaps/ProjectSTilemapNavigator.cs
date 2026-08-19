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

            var open = new List<Vector3Int> { start };
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
            var fScore = new Dictionary<Vector3Int, float> { [start] = Heuristic(start, goal) };
            var closed = new HashSet<Vector3Int>();
            var expandedCells = 0;

            while (open.Count > 0 && expandedCells < maxExpandedCells)
            {
                var current = PopLowestScore(open, fScore);
                if (current == goal)
                {
                    BuildWorldPath(cameFrom, current, result);
                    return true;
                }

                closed.Add(current);
                expandedCells++;

                foreach (var neighbor in EnumerateNeighbors(current))
                {
                    if (closed.Contains(neighbor) || !tilemapWorld.IsWalkable(neighbor))
                    {
                        continue;
                    }

                    if (IsDiagonalMove(current, neighbor) && !CanMoveDiagonally(current, neighbor))
                    {
                        continue;
                    }

                    var tentativeScore = gScore[current] + GetStepCost(current, neighbor);
                    if (gScore.TryGetValue(neighbor, out var knownScore) && tentativeScore >= knownScore)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;
                    fScore[neighbor] = tentativeScore + Heuristic(neighbor, goal);
                    if (!open.Contains(neighbor))
                    {
                        open.Add(neighbor);
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

            return tilemapWorld.IsWalkable(tilemapWorld.WorldToCell(from))
                && tilemapWorld.IsWalkable(tilemapWorld.WorldToCell(to));
        }

        private void ResolveReferences()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = GetComponent<ProjectSTilemapWorld>();
            }
        }

        private IEnumerable<Vector3Int> EnumerateNeighbors(Vector3Int cell)
        {
            foreach (var direction in CardinalDirections)
            {
                yield return cell + direction;
            }

            if (!allowDiagonalMovement)
            {
                yield break;
            }

            foreach (var direction in DiagonalDirections)
            {
                yield return cell + direction;
            }
        }

        private bool CanMoveDiagonally(Vector3Int from, Vector3Int to)
        {
            var delta = to - from;
            var horizontal = from + new Vector3Int(delta.x, 0, 0);
            var vertical = from + new Vector3Int(0, delta.y, 0);
            return tilemapWorld.IsWalkable(horizontal) && tilemapWorld.IsWalkable(vertical);
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
            var cells = new List<Vector3Int> { current };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                cells.Add(current);
            }

            cells.Reverse();
            foreach (var cell in cells)
            {
                result.Add(tilemapWorld.GetCellCenterWorld(cell));
            }
        }

        private static Vector3Int PopLowestScore(List<Vector3Int> open, Dictionary<Vector3Int, float> fScore)
        {
            var bestIndex = 0;
            var bestScore = fScore.TryGetValue(open[0], out var score) ? score : float.PositiveInfinity;
            for (var i = 1; i < open.Count; i++)
            {
                var candidateScore = fScore.TryGetValue(open[i], out score) ? score : float.PositiveInfinity;
                if (candidateScore < bestScore)
                {
                    bestScore = candidateScore;
                    bestIndex = i;
                }
            }

            var best = open[bestIndex];
            open.RemoveAt(bestIndex);
            return best;
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
    }
}
