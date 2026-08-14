using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Maps
{
    public static class MapAutoWallBuilder
    {
        private const float WallThickness = 0.12f;
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public static void BuildHeightWalls(MapDefinition map, Transform parent)
        {
            if (map == null || parent == null)
            {
                return;
            }

            var wallRoot = new GameObject("Auto Height Walls").transform;
            wallRoot.SetParent(parent, false);
            var rampFootprintCells = CollectRampFootprintCells(map);

            foreach (var cell in map.Cells)
            {
                if (cell == null
                    || !MapTerrainRules.UsesAutoHeightWalls(cell.terrainType)
                    || rampFootprintCells.Contains(cell.Position))
                {
                    continue;
                }

                foreach (var direction in Directions)
                {
                    var neighbor = map.GetCell(cell.Position + direction);
                    if (neighbor == null
                        || MapTerrainRules.IsRamp(neighbor.terrainType)
                        || rampFootprintCells.Contains(neighbor.Position))
                    {
                        continue;
                    }

                    if (cell.heightLevel > neighbor.heightLevel)
                    {
                        CreateWall(map, wallRoot, cell.Position, direction, neighbor.heightLevel, cell.heightLevel);
                    }
                }
            }
        }

        private static HashSet<Vector2Int> CollectRampFootprintCells(MapDefinition map)
        {
            var cells = new HashSet<Vector2Int>();
            if (map.TileSet == null)
            {
                return cells;
            }

            foreach (var cell in map.Cells)
            {
                if (cell == null || !MapTerrainRules.IsRamp(cell.terrainType))
                {
                    continue;
                }

                var entry = map.TileSet.FindEntry(cell.tileId, PlacedMapObjectType.Terrain);
                var footprint = GetRotatedFootprint(entry, cell.rotationY);
                for (var y = 0; y < footprint.y; y++)
                {
                    for (var x = 0; x < footprint.x; x++)
                    {
                        var position = cell.Position + new Vector2Int(x, y);
                        if (map.InBounds(position))
                        {
                            cells.Add(position);
                        }
                    }
                }
            }

            return cells;
        }

        private static Vector2Int GetRotatedFootprint(TilePrefabEntry entry, float rotationY)
        {
            if (entry == null)
            {
                return Vector2Int.one;
            }

            var size = new Vector2Int(Mathf.Max(1, entry.size.x), Mathf.Max(1, entry.size.y));
            var normalizedRotation = Mathf.RoundToInt(Mathf.Repeat(rotationY, 360f));
            if (entry.allowRotation && (normalizedRotation == 90 || normalizedRotation == 270))
            {
                return new Vector2Int(size.y, size.x);
            }

            return size;
        }

        private static void CreateWall(MapDefinition map, Transform parent, Vector2Int cellPosition, Vector2Int direction, int lowerHeight, int upperHeight)
        {
            var height = upperHeight - lowerHeight;
            if (height <= 0)
            {
                return;
            }

            var tileSize = map.TileSize;
            var center = map.GridToWorld(cellPosition);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Height Wall {cellPosition.x},{cellPosition.y} {direction.x},{direction.y}";
            wall.transform.SetParent(parent, false);
            wall.transform.position = GetWallPosition(center, tileSize, direction, lowerHeight, height);
            wall.transform.localScale = GetWallScale(tileSize, direction, height);
        }

        private static Vector3 GetWallPosition(Vector3 cellCenter, float tileSize, Vector2Int direction, int lowerHeight, int height)
        {
            var halfTile = tileSize * 0.5f;
            var y = lowerHeight + height * 0.5f;
            return cellCenter + new Vector3(direction.x * halfTile, y - cellCenter.y, direction.y * halfTile);
        }

        private static Vector3 GetWallScale(float tileSize, Vector2Int direction, int height)
        {
            if (direction.x != 0)
            {
                return new Vector3(WallThickness, height, tileSize);
            }

            return new Vector3(tileSize, height, WallThickness);
        }
    }
}
