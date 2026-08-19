using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Tilemaps
{
    [CreateAssetMenu(menuName = "Project S/Tilemaps/Project S Tile", fileName = "ProjectSTile")]
    public sealed class ProjectSTile : Tile
    {
        [SerializeField] private ProjectSTerrainType terrainType = ProjectSTerrainType.Ground;
        [SerializeField] private bool walkable = true;
        [SerializeField] private bool buildable = true;
        [SerializeField] private bool blocksMovement;
        [SerializeField] private bool blocksConstruction;
        [SerializeField] private bool blocksVision;
        [SerializeField] private float movementCost = 1f;

        public ProjectSTerrainType TerrainType => terrainType;
        public bool Walkable => walkable && !blocksMovement;
        public bool Buildable => buildable && !blocksConstruction;
        public bool BlocksMovement => blocksMovement;
        public bool BlocksConstruction => blocksConstruction;
        public bool BlocksVision => blocksVision;
        public float MovementCost => Mathf.Max(0.01f, movementCost);
    }
}
