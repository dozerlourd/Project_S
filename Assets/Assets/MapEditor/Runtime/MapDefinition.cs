using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Maps
{
    [CreateAssetMenu(menuName = "Project S/Maps/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private string mapName = "New RTS Map";
        [SerializeField] private int width = 32;
        [SerializeField] private int height = 32;
        [SerializeField] private float tileSize = 2f;
        [SerializeField] private TileSetDefinition tileSet;
        [SerializeField] private GameObject bakedMapPrefab;
        [SerializeField] private MapRuntimeBuildMode runtimeBuildMode = MapRuntimeBuildMode.PreferBakedPrefab;
        [SerializeField] private List<MapCellData> cells = new List<MapCellData>();
        [SerializeField] private List<PlacedMapObject> placedObjects = new List<PlacedMapObject>();
        [SerializeField] private List<SpawnPointData> spawnPoints = new List<SpawnPointData>();
        [SerializeField] private List<ResourceNodeData> resourceNodes = new List<ResourceNodeData>();

        public string MapName { get => mapName; set => mapName = value; }
        public int Width => width;
        public int Height => height;
        public float TileSize => tileSize;
        public TileSetDefinition TileSet { get => tileSet; set => tileSet = value; }
        public GameObject BakedMapPrefab { get => bakedMapPrefab; set => bakedMapPrefab = value; }
        public MapRuntimeBuildMode RuntimeBuildMode { get => runtimeBuildMode; set => runtimeBuildMode = value; }
        public IReadOnlyList<MapCellData> Cells => cells;
        public List<PlacedMapObject> PlacedObjects => placedObjects;
        public List<SpawnPointData> SpawnPoints => spawnPoints;
        public List<ResourceNodeData> ResourceNodes => resourceNodes;

        public void Initialize(int newWidth, int newHeight, float newTileSize)
        {
            width = Mathf.Max(1, newWidth);
            height = Mathf.Max(1, newHeight);
            tileSize = Mathf.Max(0.1f, newTileSize);
            cells.Clear();
            placedObjects.Clear();
            spawnPoints.Clear();
            resourceNodes.Clear();
            EnsureCells();
        }

        public void EnsureCells()
        {
            var expected = Mathf.Max(1, width) * Mathf.Max(1, height);
            if (cells.Count == expected)
            {
                for (var i = 0; i < cells.Count; i++)
                {
                    var x = i % width;
                    var y = i / width;
                    if (cells[i] == null)
                    {
                        cells[i] = new MapCellData();
                    }
                    cells[i].x = x;
                    cells[i].y = y;
                }

                return;
            }

            var oldCells = new Dictionary<Vector2Int, MapCellData>();
            foreach (var cell in cells)
            {
                if (cell != null)
                {
                    oldCells[cell.Position] = cell;
                }
            }

            cells.Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (oldCells.TryGetValue(position, out var existing))
                    {
                        existing.x = x;
                        existing.y = y;
                        cells.Add(existing);
                    }
                    else
                    {
                        var cell = new MapCellData();
                        cell.Reset(x, y);
                        cells.Add(cell);
                    }
                }
            }
        }

        public bool InBounds(Vector2Int position)
        {
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }

        public int GetIndex(Vector2Int position)
        {
            return position.y * width + position.x;
        }

        public MapCellData GetCell(Vector2Int position)
        {
            if (!InBounds(position))
            {
                return null;
            }

            EnsureCells();
            return cells[GetIndex(position)];
        }

        public void SetTerrainCell(Vector2Int position, TilePrefabEntry entry, float rotationY)
        {
            var cell = GetCell(position);
            if (cell == null || entry == null)
            {
                return;
            }

            cell.heightLevel = entry.heightLevel;
            cell.terrainType = entry.terrainType;
            cell.walkable = entry.defaultWalkable;
            cell.buildable = entry.defaultBuildable;
            cell.occupied = false;
            cell.tileId = entry.id;
            cell.rotationY = rotationY;
        }

        public void ClearCell(Vector2Int position)
        {
            var cell = GetCell(position);
            cell?.Reset(position.x, position.y);
            placedObjects.RemoveAll(item => item.gridPosition == position);
            spawnPoints.RemoveAll(item => item.gridPosition == position);
            resourceNodes.RemoveAll(item => item.gridPosition == position);
        }

        public Vector3 GridToWorld(Vector2Int position, int heightLevel = 0)
        {
            var halfTile = tileSize * 0.5f;
            return new Vector3(position.x * tileSize + halfTile, heightLevel, position.y * tileSize + halfTile);
        }

        public Vector3 GridToWorldCorner(Vector2Int position, int heightLevel = 0)
        {
            return new Vector3(position.x * tileSize, heightLevel, position.y * tileSize);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / tileSize),
                Mathf.FloorToInt(worldPosition.z / tileSize));
        }
    }
}
