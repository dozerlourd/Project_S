using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Maps
{
    public sealed class MapTerrainQuery
    {
        private readonly MapDefinition mapDefinition;
        private readonly Dictionary<Vector2Int, MapTerrainSample> rampCoverage = new Dictionary<Vector2Int, MapTerrainSample>();

        public MapTerrainQuery(MapDefinition mapDefinition)
        {
            this.mapDefinition = mapDefinition;
            BuildRampCoverage();
        }

        public bool TryGetCell(Vector2Int position, out MapTerrainSample sample)
        {
            sample = default;
            if (mapDefinition == null || !mapDefinition.InBounds(position))
            {
                return false;
            }

            var rawCell = mapDefinition.GetCell(position);
            if (rawCell != null && (rawCell.terrainType != MapTerrainType.Empty || !string.IsNullOrEmpty(rawCell.tileId)))
            {
                sample = MapTerrainSample.FromMapCell(rawCell);
                return true;
            }

            if (rampCoverage.TryGetValue(position, out sample))
            {
                return true;
            }

            if (rawCell == null)
            {
                return false;
            }

            sample = MapTerrainSample.FromMapCell(rawCell);
            return true;
        }

        public bool IsGroundWalkable(Vector2Int position)
        {
            return TryGetCell(position, out var sample) && sample.Walkable && !sample.Occupied;
        }

        public bool CanMoveBetween(Vector2Int from, Vector2Int to, float maxGroundHeightStep)
        {
            if (!IsGroundWalkable(from) || !IsGroundWalkable(to))
            {
                return false;
            }

            if (!TryGetCell(from, out var fromCell) || !TryGetCell(to, out var toCell))
            {
                return false;
            }

            var direction = to - from;
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
            {
                return CanMoveDiagonally(from, to, direction, maxGroundHeightStep);
            }

            var fromSurfaceY = GetUnitSurfaceYAtEdge(from, fromCell, direction);
            var toSurfaceY = GetUnitSurfaceYAtEdge(to, toCell, -direction);
            var surfaceDelta = Mathf.Abs(fromSurfaceY - toSurfaceY);

            return surfaceDelta <= maxGroundHeightStep + 0.001f;
        }

        public float GetUnitSurfaceY(Vector2Int gridPosition, Vector3 worldPosition)
        {
            if (!TryGetCell(gridPosition, out var cell))
            {
                return 0f;
            }

            if (!cell.IsRamp)
            {
                return cell.FlatSurfaceY;
            }

            var run = GetRampRun(gridPosition, cell);
            var localT = GetRampLocalProgress(cell, worldPosition);
            var runT = (run.IndexFromLowerEnd + localT) / run.Length;
            return cell.HeightLevel + Mathf.Clamp01(runT);
        }

        public float GetUnitSurfaceYAtEdge(Vector2Int gridPosition, MapTerrainSample cell, Vector2Int edgeDirection)
        {
            if (!cell.IsRamp)
            {
                return cell.FlatSurfaceY;
            }

            var run = GetRampRun(gridPosition, cell);
            var lowerToUpper = GetRampLowerToUpperDirection(cell.RotationY);
            float edgeT;
            if (edgeDirection == lowerToUpper)
            {
                edgeT = (run.IndexFromLowerEnd + 1f) / run.Length;
            }
            else if (edgeDirection == -lowerToUpper)
            {
                edgeT = run.IndexFromLowerEnd / (float)run.Length;
            }
            else
            {
                edgeT = (run.IndexFromLowerEnd + 0.5f) / run.Length;
            }

            return cell.HeightLevel + Mathf.Clamp01(edgeT);
        }

        private void BuildRampCoverage()
        {
            rampCoverage.Clear();
            if (mapDefinition == null || mapDefinition.TileSet == null)
            {
                return;
            }

            mapDefinition.EnsureCells();
            foreach (var cell in mapDefinition.Cells)
            {
                if (cell == null || string.IsNullOrEmpty(cell.tileId))
                {
                    continue;
                }

                var entry = mapDefinition.TileSet.FindEntry(cell.tileId, PlacedMapObjectType.Terrain);
                if (entry == null || !MapTerrainRules.IsRamp(entry.terrainType))
                {
                    continue;
                }

                var direction = GetRampLowerToUpperDirection(cell.rotationY);
                var length = GetRampFootprintLength(entry);
                for (var index = 0; index < length; index++)
                {
                    var coveredPosition = cell.Position + direction * index;
                    if (!mapDefinition.InBounds(coveredPosition))
                    {
                        continue;
                    }

                    AddRampCoverage(coveredPosition, cell, entry, length);
                }
            }
        }

        private void AddRampCoverage(Vector2Int coveredPosition, MapCellData anchorCell, TilePrefabEntry entry, int footprintLength)
        {
            var coverage = MapTerrainSample.FromRampCoverage(coveredPosition, anchorCell, entry, footprintLength);
            if (rampCoverage.TryGetValue(coveredPosition, out var existing) && existing.FootprintLength >= coverage.FootprintLength)
            {
                return;
            }

            rampCoverage[coveredPosition] = coverage;
        }

        private bool CanMoveDiagonally(Vector2Int from, Vector2Int to, Vector2Int direction, float maxGroundHeightStep)
        {
            if (Mathf.Abs(direction.x) != 1 || Mathf.Abs(direction.y) != 1)
            {
                return false;
            }

            var horizontalStep = new Vector2Int(direction.x, 0);
            var verticalStep = new Vector2Int(0, direction.y);
            var horizontalFirst = from + horizontalStep;
            var verticalFirst = from + verticalStep;

            return CanMoveBetween(from, horizontalFirst, maxGroundHeightStep) && CanMoveBetween(horizontalFirst, to, maxGroundHeightStep)
                || CanMoveBetween(from, verticalFirst, maxGroundHeightStep) && CanMoveBetween(verticalFirst, to, maxGroundHeightStep);
        }

        private RampRun GetRampRun(Vector2Int gridPosition, MapTerrainSample cell)
        {
            var lowerToUpper = GetRampLowerToUpperDirection(cell.RotationY);
            var lowerEnd = gridPosition;
            while (IsSameRampRunCell(lowerEnd - lowerToUpper, cell))
            {
                lowerEnd -= lowerToUpper;
            }

            var length = 1;
            var cursor = lowerEnd;
            while (IsSameRampRunCell(cursor + lowerToUpper, cell))
            {
                cursor += lowerToUpper;
                length++;
            }

            var index = Mathf.Abs(gridPosition.x - lowerEnd.x) + Mathf.Abs(gridPosition.y - lowerEnd.y);
            return new RampRun(Mathf.Max(1, length), Mathf.Clamp(index, 0, Mathf.Max(0, length - 1)));
        }

        private bool IsSameRampRunCell(Vector2Int gridPosition, MapTerrainSample source)
        {
            if (!TryGetCell(gridPosition, out var candidate) || !candidate.IsRamp)
            {
                return false;
            }

            return candidate.HeightLevel == source.HeightLevel
                && GetRampLowerToUpperDirection(candidate.RotationY) == GetRampLowerToUpperDirection(source.RotationY);
        }

        private static int GetRampFootprintLength(TilePrefabEntry entry)
        {
            var size = entry == null ? Vector2Int.one : entry.size;
            return Mathf.Max(1, Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)));
        }

        public static Vector2Int GetRampLowerToUpperDirection(float rotationY)
        {
            var rotation = Mathf.RoundToInt(Mathf.Repeat(rotationY, 360f));
            switch (rotation)
            {
                case 90:
                    return new Vector2Int(1, 0);
                case 180:
                    return new Vector2Int(0, -1);
                case 270:
                    return new Vector2Int(-1, 0);
                default:
                    return new Vector2Int(0, 1);
            }
        }

        private float GetRampLocalProgress(MapTerrainSample cell, Vector3 worldPosition)
        {
            var normalizedX = Mathf.Clamp01((worldPosition.x - cell.Position.x * mapDefinition.TileSize) / mapDefinition.TileSize);
            var normalizedZ = Mathf.Clamp01((worldPosition.z - cell.Position.y * mapDefinition.TileSize) / mapDefinition.TileSize);
            var rotation = Mathf.RoundToInt(Mathf.Repeat(cell.RotationY, 360f));
            switch (rotation)
            {
                case 90:
                    return normalizedX;
                case 180:
                    return 1f - normalizedZ;
                case 270:
                    return 1f - normalizedX;
                default:
                    return normalizedZ;
            }
        }

        private readonly struct RampRun
        {
            public readonly int Length;
            public readonly int IndexFromLowerEnd;

            public RampRun(int length, int indexFromLowerEnd)
            {
                Length = length;
                IndexFromLowerEnd = indexFromLowerEnd;
            }
        }
    }

    public readonly struct MapTerrainSample
    {
        public readonly Vector2Int Position;
        public readonly int HeightLevel;
        public readonly MapTerrainType TerrainType;
        public readonly bool Walkable;
        public readonly bool Buildable;
        public readonly bool Occupied;
        public readonly string TileId;
        public readonly float RotationY;
        public readonly int FootprintLength;

        public bool IsRamp => MapTerrainRules.IsRamp(TerrainType);
        public float FlatSurfaceY => HeightLevel + MapTerrainRules.GetUnitSurfaceOffset(TerrainType);

        private MapTerrainSample(
            Vector2Int position,
            int heightLevel,
            MapTerrainType terrainType,
            bool walkable,
            bool buildable,
            bool occupied,
            string tileId,
            float rotationY,
            int footprintLength)
        {
            Position = position;
            HeightLevel = heightLevel;
            TerrainType = terrainType;
            Walkable = walkable;
            Buildable = buildable;
            Occupied = occupied;
            TileId = tileId;
            RotationY = rotationY;
            FootprintLength = Mathf.Max(1, footprintLength);
        }

        public static MapTerrainSample FromMapCell(MapCellData cell)
        {
            return new MapTerrainSample(
                cell.Position,
                cell.heightLevel,
                cell.terrainType,
                cell.walkable,
                cell.buildable,
                cell.occupied,
                cell.tileId,
                cell.rotationY,
                1);
        }

        public static MapTerrainSample FromRampCoverage(Vector2Int position, MapCellData anchorCell, TilePrefabEntry entry, int footprintLength)
        {
            return new MapTerrainSample(
                position,
                anchorCell.heightLevel,
                entry.terrainType,
                anchorCell.walkable,
                anchorCell.buildable,
                anchorCell.occupied,
                entry.id,
                anchorCell.rotationY,
                footprintLength);
        }
    }
}
