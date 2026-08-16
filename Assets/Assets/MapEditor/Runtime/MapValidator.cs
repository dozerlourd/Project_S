using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Maps
{
    public static class MapValidator
    {
        private const float DefaultMaxGroundHeightStep = 0.6f;

        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public static List<MapValidationIssue> Validate(MapDefinition map)
        {
            var issues = new List<MapValidationIssue>();
            if (map == null)
            {
                issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "MapDefinition is missing.", Vector2Int.zero));
                return issues;
            }

            map.EnsureCells();
            var terrainQuery = new MapTerrainQuery(map);
            ValidateSpawns(map, issues);
            ValidateResources(map, terrainQuery, issues);
            ValidateRamps(map, terrainQuery, issues);
            ValidateConnectivity(map, terrainQuery, issues);
            ValidateObjectOverlap(map, issues);

            if (issues.Count == 0)
            {
                issues.Add(new MapValidationIssue(MapValidationSeverity.Info, "Map validation passed.", Vector2Int.zero));
            }

            return issues;
        }

        private static void ValidateSpawns(MapDefinition map, List<MapValidationIssue> issues)
        {
            if (map.SpawnPoints.Count < 2)
            {
                issues.Add(new MapValidationIssue(MapValidationSeverity.Warning, "At least two spawn points are recommended for a 1v1 RTS map.", Vector2Int.zero));
            }

            foreach (var spawn in map.SpawnPoints)
            {
                if (spawn == null || !map.InBounds(spawn.gridPosition))
                {
                    issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Spawn point is outside the map.", spawn?.gridPosition ?? Vector2Int.zero));
                    continue;
                }

                var buildableCount = 0;
                for (var y = -2; y <= 2; y++)
                {
                    for (var x = -2; x <= 2; x++)
                    {
                        var position = spawn.gridPosition + new Vector2Int(x, y);
                        if (map.GetCell(position)?.buildable == true)
                        {
                            buildableCount++;
                        }
                    }
                }

                if (buildableCount < 9)
                {
                    issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Spawn point needs more buildable space around it.", spawn.gridPosition));
                }
            }
        }

        private static void ValidateResources(MapDefinition map, MapTerrainQuery terrainQuery, List<MapValidationIssue> issues)
        {
            foreach (var resource in map.ResourceNodes)
            {
                if (resource == null || !map.InBounds(resource.gridPosition))
                {
                    issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Resource node is outside the map.", resource?.gridPosition ?? Vector2Int.zero));
                    continue;
                }

                if (!HasAdjacentWalkableCell(terrainQuery, resource.gridPosition))
                {
                    issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Resource node has no adjacent walkable cell.", resource.gridPosition));
                }
            }
        }

        private static void ValidateRamps(MapDefinition map, MapTerrainQuery terrainQuery, List<MapValidationIssue> issues)
        {
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (!terrainQuery.TryGetCell(position, out var cell) || !cell.IsRamp)
                    {
                        continue;
                    }

                    var centerY = terrainQuery.GetUnitSurfaceY(position, map.GridToWorld(position));
                    var hasLower = false;
                    var hasHigher = false;
                    foreach (var direction in CardinalDirections)
                    {
                        var neighborPosition = position + direction;
                        if (!terrainQuery.TryGetCell(neighborPosition, out var neighbor)
                            || !neighbor.Walkable
                            || !terrainQuery.CanMoveBetween(position, neighborPosition, DefaultMaxGroundHeightStep))
                        {
                            continue;
                        }

                        var neighborY = terrainQuery.GetUnitSurfaceYAtEdge(neighborPosition, neighbor, -direction);
                        if (neighborY < centerY - 0.01f)
                        {
                            hasLower = true;
                        }

                        if (neighborY > centerY + 0.01f)
                        {
                            hasHigher = true;
                        }
                    }

                    if (!hasLower || !hasHigher)
                    {
                        issues.Add(new MapValidationIssue(MapValidationSeverity.Warning, "Ramp should connect lower and higher walkable terrain.", position));
                    }

                }
            }
        }

        private static void ValidateConnectivity(MapDefinition map, MapTerrainQuery terrainQuery, List<MapValidationIssue> issues)
        {
            var start = FindFirstWalkable(map, terrainQuery);
            if (!start.HasValue)
            {
                issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Map has no walkable cells.", Vector2Int.zero));
                return;
            }

            var reachable = FloodFillWalkable(map, terrainQuery, start.Value);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (terrainQuery.IsGroundWalkable(position) && !reachable.Contains(position))
                    {
                        issues.Add(new MapValidationIssue(MapValidationSeverity.Warning, "Walkable area is disconnected from the main area.", position));
                        return;
                    }
                }
            }

            foreach (var resource in map.ResourceNodes)
            {
                if (resource != null && !HasReachableAdjacentCell(map, terrainQuery, reachable, resource.gridPosition))
                {
                    issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Resource node is not reachable from the main walkable area.", resource.gridPosition));
                }
            }
        }

        private static void ValidateObjectOverlap(MapDefinition map, List<MapValidationIssue> issues)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var placedObject in map.PlacedObjects)
            {
                if (placedObject == null)
                {
                    continue;
                }

                for (var y = 0; y < Mathf.Max(1, placedObject.size.y); y++)
                {
                    for (var x = 0; x < Mathf.Max(1, placedObject.size.x); x++)
                    {
                        var position = placedObject.gridPosition + new Vector2Int(x, y);
                        if (!map.InBounds(position))
                        {
                            issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Placed object extends outside the map.", position));
                            continue;
                        }

                        if (!occupied.Add(position))
                        {
                            issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Placed objects overlap.", position));
                        }

                        if (placedObject.blocksConstruction && map.GetCell(position)?.buildable == true)
                        {
                            issues.Add(new MapValidationIssue(MapValidationSeverity.Warning, "Object blocks construction on a buildable tile.", position));
                        }
                    }
                }
            }

            foreach (var resource in map.ResourceNodes)
            {
                if (resource == null)
                {
                    continue;
                }

                AddOccupiedArea(map, issues, occupied, resource.gridPosition, resource.size, "Resource node overlaps another object.");
            }

            foreach (var spawn in map.SpawnPoints)
            {
                if (spawn == null)
                {
                    continue;
                }

                AddOccupiedArea(map, issues, occupied, spawn.gridPosition, Vector2Int.one, "Spawn point overlaps another object.");
            }
        }

        private static void AddOccupiedArea(
            MapDefinition map,
            List<MapValidationIssue> issues,
            HashSet<Vector2Int> occupied,
            Vector2Int origin,
            Vector2Int size,
            string overlapMessage)
        {
            for (var y = 0; y < Mathf.Max(1, size.y); y++)
            {
                for (var x = 0; x < Mathf.Max(1, size.x); x++)
                {
                    var position = origin + new Vector2Int(x, y);
                    if (!map.InBounds(position))
                    {
                        issues.Add(new MapValidationIssue(MapValidationSeverity.Error, "Object extends outside the map.", position));
                        continue;
                    }

                    if (!occupied.Add(position))
                    {
                        issues.Add(new MapValidationIssue(MapValidationSeverity.Error, overlapMessage, position));
                    }
                }
            }
        }

        private static bool HasAdjacentWalkableCell(MapTerrainQuery terrainQuery, Vector2Int position)
        {
            foreach (var direction in CardinalDirections)
            {
                if (terrainQuery.IsGroundWalkable(position + direction))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasReachableAdjacentCell(MapDefinition map, MapTerrainQuery terrainQuery, HashSet<Vector2Int> reachable, Vector2Int position)
        {
            foreach (var direction in CardinalDirections)
            {
                var adjacent = position + direction;
                if (map.InBounds(adjacent) && terrainQuery.IsGroundWalkable(adjacent) && reachable.Contains(adjacent))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2Int? FindFirstWalkable(MapDefinition map, MapTerrainQuery terrainQuery)
        {
            if (map.SpawnPoints.Count > 0)
            {
                var spawnPosition = map.SpawnPoints[0].gridPosition;
                if (terrainQuery.IsGroundWalkable(spawnPosition))
                {
                    return spawnPosition;
                }
            }

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (terrainQuery.IsGroundWalkable(position))
                    {
                        return position;
                    }
                }
            }

            return null;
        }

        private static HashSet<Vector2Int> FloodFillWalkable(MapDefinition map, MapTerrainQuery terrainQuery, Vector2Int start)
        {
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in CardinalDirections)
                {
                    var next = current + direction;
                    if (visited.Contains(next)
                        || !map.InBounds(next)
                        || !terrainQuery.CanMoveBetween(current, next, DefaultMaxGroundHeightStep))
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return visited;
        }
    }
}
