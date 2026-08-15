using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ProjectS.Maps
{
    public sealed class MapPathfinder : MonoBehaviour
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0)
        };

        [SerializeField] private MapDefinition mapDefinition;
        [SerializeField] private bool allowDiagonalMovement;

        private readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();

        public bool HasMapDefinition => mapDefinition != null;

        public MapDefinition MapDefinition
        {
            get => mapDefinition;
            set
            {
                mapDefinition = value;
                RebuildBlockCache();
            }
        }

        private void Awake()
        {
            ResolveMapDefinition();
            RebuildBlockCache();
        }

        private void OnEnable()
        {
            ResolveMapDefinition();
            RebuildBlockCache();
        }

        public bool ResolveMapDefinition()
        {
            if (mapDefinition == null)
            {
                var runtimeBuilder = GetComponent<MapRuntimeBuilder>();
                if (runtimeBuilder != null)
                {
                    mapDefinition = runtimeBuilder.MapDefinition;
                }
            }

            if (mapDefinition == null)
            {
                var runtimeBuilder = FindFirstObjectByType<MapRuntimeBuilder>();
                if (runtimeBuilder != null)
                {
                    mapDefinition = runtimeBuilder.MapDefinition;
                }
            }

#if UNITY_EDITOR
            if (mapDefinition == null)
            {
                mapDefinition = FindFirstMapDefinitionAssetInEditor();
            }
#endif

            return mapDefinition != null;
        }

#if UNITY_EDITOR
        private static MapDefinition FindFirstMapDefinitionAssetInEditor()
        {
            var guids = AssetDatabase.FindAssets("t:MapDefinition");
            if (guids.Length == 0)
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MapDefinition>(assetPath);
        }
#endif

        [ContextMenu("Rebuild Block Cache")]
        public void RebuildBlockCache()
        {
            blockedCells.Clear();
            if (mapDefinition == null && !ResolveMapDefinition())
            {
                return;
            }

            mapDefinition.EnsureCells();
            foreach (var placedObject in mapDefinition.PlacedObjects)
            {
                if (placedObject == null || !placedObject.blocksMovement)
                {
                    continue;
                }

                AddBlockedArea(placedObject.gridPosition, placedObject.size);
            }

            foreach (var resource in mapDefinition.ResourceNodes)
            {
                if (resource == null)
                {
                    continue;
                }

                AddBlockedArea(resource.gridPosition, resource.size);
            }
        }

        public bool TryFindPath(Vector3 startWorld, Vector3 goalWorld, List<Vector3> worldPath)
        {
            worldPath?.Clear();
            if ((mapDefinition == null && !ResolveMapDefinition()) || worldPath == null)
            {
                return false;
            }

            var start = mapDefinition.WorldToGrid(startWorld);
            var goal = mapDefinition.WorldToGrid(goalWorld);
            if (!TryFindPath(start, goal, worldPath))
            {
                return false;
            }

            if (worldPath.Count > 0)
            {
                var goalCell = mapDefinition.GetCell(goal);
                worldPath[worldPath.Count - 1] = GetUnitWorldPosition(goalWorld, goalCell);
            }

            SmoothWorldPath(worldPath);
            return true;
        }

        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector3> worldPath)
        {
            worldPath?.Clear();
            if ((mapDefinition == null && !ResolveMapDefinition()) || worldPath == null)
            {
                return false;
            }

            mapDefinition.EnsureCells();
            if (!TryGetNearestWalkable(start, out var walkableStart) || !TryGetNearestWalkable(goal, out var walkableGoal))
            {
                return false;
            }

            var gridPath = FindGridPath(walkableStart, walkableGoal);
            if (gridPath.Count == 0)
            {
                return false;
            }

            foreach (var cellPosition in gridPath)
            {
                var cell = mapDefinition.GetCell(cellPosition);
                worldPath.Add(GetUnitWorldPosition(cellPosition, cell));
            }

            return true;
        }

        public bool IsGroundWalkable(Vector2Int position)
        {
            if ((mapDefinition == null && !ResolveMapDefinition()) || blockedCells.Contains(position))
            {
                return false;
            }

            var cell = mapDefinition.GetCell(position);
            return cell != null && cell.walkable && !cell.occupied;
        }

        public bool TryGetMapPoint(Ray ray, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (mapDefinition == null && !ResolveMapDefinition())
            {
                return false;
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            var point = ray.GetPoint(enter);
            var gridPosition = mapDefinition.WorldToGrid(point);
            var cell = mapDefinition.GetCell(gridPosition);
            if (cell == null)
            {
                return false;
            }

            worldPosition = GetUnitWorldPosition(point, cell);
            return true;
        }

        private Vector3 GetUnitWorldPosition(Vector2Int gridPosition, MapCellData cell)
        {
            var worldPosition = mapDefinition.GridToWorld(gridPosition, cell?.heightLevel ?? 0);
            worldPosition.y = MapTerrainRules.GetUnitSurfaceY(cell);
            return worldPosition;
        }

        private Vector3 GetUnitWorldPosition(Vector3 worldPosition, MapCellData cell)
        {
            return new Vector3(worldPosition.x, MapTerrainRules.GetUnitSurfaceY(cell), worldPosition.z);
        }

        public bool IsSegmentWalkable(Vector3 fromWorld, Vector3 toWorld)
        {
            if (mapDefinition == null && !ResolveMapDefinition())
            {
                return false;
            }

            var distance = Vector3.Distance(
                new Vector3(fromWorld.x, 0f, fromWorld.z),
                new Vector3(toWorld.x, 0f, toWorld.z));
            var stepLength = Mathf.Max(0.1f, mapDefinition.TileSize * 0.35f);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepLength));
            var previous = mapDefinition.WorldToGrid(fromWorld);
            if (!IsGroundWalkable(previous))
            {
                return false;
            }

            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var sample = Vector3.Lerp(fromWorld, toWorld, t);
                var current = mapDefinition.WorldToGrid(sample);
                if (current == previous)
                {
                    continue;
                }

                if (!CanMoveBetween(previous, current))
                {
                    return false;
                }

                previous = current;
            }

            return true;
        }

        private void SmoothWorldPath(List<Vector3> worldPath)
        {
            if (worldPath.Count <= 2)
            {
                return;
            }

            var smoothed = new List<Vector3> { worldPath[0] };
            var anchorIndex = 0;
            while (anchorIndex < worldPath.Count - 1)
            {
                var nextIndex = worldPath.Count - 1;
                while (nextIndex > anchorIndex + 1 && !IsSegmentWalkable(worldPath[anchorIndex], worldPath[nextIndex]))
                {
                    nextIndex--;
                }

                smoothed.Add(worldPath[nextIndex]);
                anchorIndex = nextIndex;
            }

            worldPath.Clear();
            worldPath.AddRange(smoothed);
        }

        private void AddBlockedArea(Vector2Int origin, Vector2Int size)
        {
            var width = Mathf.Max(1, size.x);
            var height = Mathf.Max(1, size.y);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    blockedCells.Add(new Vector2Int(origin.x + x, origin.y + y));
                }
            }
        }

        private bool TryGetNearestWalkable(Vector2Int origin, out Vector2Int walkable)
        {
            if (IsGroundWalkable(origin))
            {
                walkable = origin;
                return true;
            }

            var maxDistance = Mathf.Max(mapDefinition.Width, mapDefinition.Height);
            for (var distance = 1; distance <= maxDistance; distance++)
            {
                for (var y = -distance; y <= distance; y++)
                {
                    for (var x = -distance; x <= distance; x++)
                    {
                        if (Mathf.Abs(x) != distance && Mathf.Abs(y) != distance)
                        {
                            continue;
                        }

                        var candidate = origin + new Vector2Int(x, y);
                        if (IsGroundWalkable(candidate))
                        {
                            walkable = candidate;
                            return true;
                        }
                    }
                }
            }

            walkable = default;
            return false;
        }

        private List<Vector2Int> FindGridPath(Vector2Int start, Vector2Int goal)
        {
            var open = new List<PathNode> { new PathNode(start, 0, EstimateCost(start, goal), default) };
            var bestCosts = new Dictionary<Vector2Int, int> { [start] = 0 };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();

            while (open.Count > 0)
            {
                var currentIndex = GetBestOpenNodeIndex(open);
                var current = open[currentIndex];
                open.RemoveAt(currentIndex);

                if (closed.Contains(current.Position))
                {
                    continue;
                }

                if (current.Position == goal)
                {
                    return ReconstructPath(cameFrom, start, goal);
                }

                closed.Add(current.Position);
                foreach (var next in GetNeighbors(current.Position))
                {
                    if (closed.Contains(next))
                    {
                        continue;
                    }

                    var nextCost = current.CostFromStart + 10;
                    if (bestCosts.TryGetValue(next, out var knownCost) && knownCost <= nextCost)
                    {
                        continue;
                    }

                    bestCosts[next] = nextCost;
                    cameFrom[next] = current.Position;
                    open.Add(new PathNode(next, nextCost, EstimateCost(next, goal), current.Position));
                }
            }

            return new List<Vector2Int>();
        }

        private IEnumerable<Vector2Int> GetNeighbors(Vector2Int position)
        {
            foreach (var direction in CardinalDirections)
            {
                var next = position + direction;
                if (CanMoveBetween(position, next))
                {
                    yield return next;
                }
            }

            if (!allowDiagonalMovement)
            {
                yield break;
            }

            for (var y = -1; y <= 1; y += 2)
            {
                for (var x = -1; x <= 1; x += 2)
                {
                    var next = position + new Vector2Int(x, y);
                    if (CanMoveBetween(position, next)
                        && IsGroundWalkable(position + new Vector2Int(x, 0))
                        && IsGroundWalkable(position + new Vector2Int(0, y)))
                    {
                        yield return next;
                    }
                }
            }
        }

        private bool CanMoveBetween(Vector2Int from, Vector2Int to)
        {
            if (!IsGroundWalkable(to))
            {
                return false;
            }

            var fromCell = mapDefinition.GetCell(from);
            var toCell = mapDefinition.GetCell(to);
            if (fromCell == null || toCell == null)
            {
                return false;
            }

            if (fromCell.heightLevel == toCell.heightLevel)
            {
                return true;
            }

            var heightDelta = Mathf.Abs(fromCell.heightLevel - toCell.heightLevel);
            return heightDelta <= 1
                && (MapTerrainRules.IsRamp(fromCell.terrainType) || MapTerrainRules.IsRamp(toCell.terrainType));
        }

        private static int GetBestOpenNodeIndex(List<PathNode> open)
        {
            var bestIndex = 0;
            for (var i = 1; i < open.Count; i++)
            {
                if (open[i].TotalCost < open[bestIndex].TotalCost
                    || open[i].TotalCost == open[bestIndex].TotalCost && open[i].EstimatedCostToGoal < open[bestIndex].EstimatedCostToGoal)
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int EstimateCost(Vector2Int from, Vector2Int to)
        {
            return (Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y)) * 10;
        }

        private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int> { goal };
            var current = goal;
            while (current != start)
            {
                if (!cameFrom.TryGetValue(current, out current))
                {
                    return new List<Vector2Int>();
                }

                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private readonly struct PathNode
        {
            public readonly Vector2Int Position;
            public readonly int CostFromStart;
            public readonly int EstimatedCostToGoal;
            public readonly Vector2Int Previous;
            public int TotalCost => CostFromStart + EstimatedCostToGoal;

            public PathNode(Vector2Int position, int costFromStart, int estimatedCostToGoal, Vector2Int previous)
            {
                Position = position;
                CostFromStart = costFromStart;
                EstimatedCostToGoal = estimatedCostToGoal;
                Previous = previous;
            }
        }
    }
}
