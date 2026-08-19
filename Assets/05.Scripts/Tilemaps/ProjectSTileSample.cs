using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Tilemaps
{
    public readonly struct ProjectSTileSample
    {
        public readonly Vector3Int Cell;
        public readonly TileBase Tile;
        public readonly ProjectSTerrainType TerrainType;
        public readonly bool Walkable;
        public readonly bool Buildable;
        public readonly bool BlocksVision;
        public readonly float MovementCost;

        public ProjectSTileSample(
            Vector3Int cell,
            TileBase tile,
            ProjectSTerrainType terrainType,
            bool walkable,
            bool buildable,
            bool blocksVision,
            float movementCost)
        {
            Cell = cell;
            Tile = tile;
            TerrainType = terrainType;
            Walkable = walkable;
            Buildable = buildable;
            BlocksVision = blocksVision;
            MovementCost = Mathf.Max(0.01f, movementCost);
        }
    }
}
