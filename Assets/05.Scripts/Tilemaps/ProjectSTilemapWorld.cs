using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Tilemaps
{
    public sealed class ProjectSTilemapWorld : MonoBehaviour
    {
        private const string ObstacleLayerName = "Obstacle";

        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap overlayTilemap;
        [SerializeField] private Tilemap obstacleTilemap;
        [SerializeField] private Tilemap stairTilemap;
        [SerializeField] private bool useExplicitBounds;
        [SerializeField] private BoundsInt explicitBounds = new BoundsInt(0, 0, 0, 64, 64, 1);
        [SerializeField] private ProjectSTerrainType fallbackTerrainType = ProjectSTerrainType.Ground;
        [SerializeField] private bool emptyCellsWalkable;
        [SerializeField] private bool emptyCellsBuildable;

        private readonly List<Tilemap> queryTilemaps = new List<Tilemap>();

        public static ProjectSTilemapWorld ActiveInstance { get; private set; }

        public Grid Grid => grid;
        public Tilemap GroundTilemap => groundTilemap;
        public Tilemap StairTilemap => stairTilemap;
        public BoundsInt CellBounds => useExplicitBounds ? explicitBounds : CalculateTilemapBounds();

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

        private void OnValidate()
        {
            if (explicitBounds.size.x < 1)
            {
                explicitBounds.size = new Vector3Int(1, explicitBounds.size.y, explicitBounds.size.z);
            }

            if (explicitBounds.size.y < 1)
            {
                explicitBounds.size = new Vector3Int(explicitBounds.size.x, 1, explicitBounds.size.z);
            }

            if (explicitBounds.size.z < 1)
            {
                explicitBounds.size = new Vector3Int(explicitBounds.size.x, explicitBounds.size.y, 1);
            }
        }

        public void ResolveReferences()
        {
            if (grid == null)
            {
                grid = GetComponentInParent<Grid>();
            }

            if (groundTilemap == null)
            {
                groundTilemap = GetComponentInChildren<Tilemap>();
            }

            RebuildQueryTilemaps();
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            ResolveReferences();
            if (grid != null)
            {
                return grid.WorldToCell(worldPosition);
            }

            return groundTilemap != null
                ? groundTilemap.WorldToCell(worldPosition)
                : Vector3Int.FloorToInt(worldPosition);
        }

        public Vector3 GetCellCenterWorld(Vector3Int cell)
        {
            ResolveReferences();
            if (groundTilemap != null)
            {
                return groundTilemap.GetCellCenterWorld(cell);
            }

            return grid != null ? grid.GetCellCenterWorld(cell) : cell + new Vector3(0.5f, 0.5f, 0f);
        }

        public bool ContainsCell(Vector3Int cell)
        {
            var bounds = CellBounds;
            return cell.x >= bounds.xMin
                && cell.y >= bounds.yMin
                && cell.x < bounds.xMax
                && cell.y < bounds.yMax;
        }

        public bool TrySample(Vector3Int cell, out ProjectSTileSample sample)
        {
            sample = default;
            if (!ContainsCell(cell))
            {
                return false;
            }

            ResolveReferences();
            var hasTile = false;
            var terrainType = fallbackTerrainType;
            var walkable = emptyCellsWalkable;
            var buildable = emptyCellsBuildable;
            var blocksVision = false;
            var movementCost = 1f;
            var hasObstacleTile = false;
            TileBase sourceTile = null;

            foreach (var tilemap in queryTilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                var tile = tilemap.GetTile(cell);
                if (tile == null)
                {
                    continue;
                }

                hasTile = true;
                sourceTile = tile;
                var isObstacleTilemap = IsObstacleTilemap(tilemap);
                hasObstacleTile |= isObstacleTilemap;
                if (tile is ProjectSTile projectTile)
                {
                    terrainType = projectTile.TerrainType;
                    walkable = projectTile.Walkable;
                    buildable = projectTile.Buildable;
                    blocksVision |= projectTile.BlocksVision;
                    movementCost = Mathf.Max(movementCost, projectTile.MovementCost);
                }
                else if (!walkable)
                {
                    walkable = true;
                    buildable = true;
                }

            }

            if (hasObstacleTile)
            {
                walkable = false;
                buildable = false;
            }

            if (!hasTile && !emptyCellsWalkable && !emptyCellsBuildable)
            {
                return false;
            }

            sample = new ProjectSTileSample(cell, sourceTile, terrainType, walkable, buildable, blocksVision, movementCost);
            return true;
        }

        public bool IsWalkable(Vector3Int cell)
        {
            return TrySample(cell, out var sample) && sample.Walkable;
        }

        public bool IsBuildable(Vector3Int cell)
        {
            return TrySample(cell, out var sample) && sample.Buildable;
        }

        public bool TryGetWorldPoint(Vector3 worldPosition, out Vector3 snappedPoint)
        {
            var cell = WorldToCell(worldPosition);
            if (!ContainsCell(cell))
            {
                snappedPoint = default;
                return false;
            }

            snappedPoint = GetCellCenterWorld(cell);
            return true;
        }

        private void RebuildQueryTilemaps()
        {
            queryTilemaps.Clear();
            AddTilemap(groundTilemap);
            AddTilemap(stairTilemap);
            AddTilemap(overlayTilemap);
            AddTilemap(obstacleTilemap);
            AddObstacleLayerTilemaps();
        }

        private void AddTilemap(Tilemap tilemap)
        {
            if (tilemap != null && !queryTilemaps.Contains(tilemap))
            {
                queryTilemaps.Add(tilemap);
            }
        }

        private void AddObstacleLayerTilemaps()
        {
            var obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
            if (obstacleLayer < 0)
            {
                return;
            }

            foreach (var tilemap in GetComponentsInChildren<Tilemap>(true))
            {
                if (tilemap != null && tilemap.gameObject.layer == obstacleLayer)
                {
                    AddTilemap(tilemap);
                }
            }
        }

        private bool IsObstacleTilemap(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return false;
            }

            var obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
            return tilemap == obstacleTilemap
                || (obstacleLayer >= 0 && tilemap.gameObject.layer == obstacleLayer);
        }

        private BoundsInt CalculateTilemapBounds()
        {
            ResolveReferences();
            var hasBounds = false;
            var min = Vector3Int.zero;
            var max = Vector3Int.zero;

            foreach (var tilemap in queryTilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                var bounds = tilemap.cellBounds;
                if (!hasBounds)
                {
                    min = bounds.min;
                    max = bounds.max;
                    hasBounds = true;
                    continue;
                }

                min = Vector3Int.Min(min, bounds.min);
                max = Vector3Int.Max(max, bounds.max);
            }

            if (!hasBounds)
            {
                return explicitBounds;
            }

            return new BoundsInt(min, max - min);
        }
    }
}
