using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Maps
{
    [Serializable]
    public sealed class TilePrefabEntry
    {
        public string id = "tile";
        public string displayName = "Tile";
        public GameObject prefab;
        public PlacedMapObjectType objectType = PlacedMapObjectType.Terrain;
        public MapTerrainType terrainType = MapTerrainType.Flat;
        public Vector2Int size = Vector2Int.one;
        public int heightLevel;
        public bool defaultWalkable = true;
        public bool defaultBuildable = true;
        public bool allowRotation = true;
        public bool blocksMovement;
        public bool blocksConstruction;
    }

    [CreateAssetMenu(menuName = "Project S/Maps/Tile Set Definition", fileName = "TileSetDefinition")]
    public sealed class TileSetDefinition : ScriptableObject
    {
        [SerializeField] private List<TilePrefabEntry> terrainTiles = new List<TilePrefabEntry>();
        [SerializeField] private List<TilePrefabEntry> rampTiles = new List<TilePrefabEntry>();
        [SerializeField] private List<TilePrefabEntry> cliffTiles = new List<TilePrefabEntry>();
        [SerializeField] private List<TilePrefabEntry> propPrefabs = new List<TilePrefabEntry>();
        [SerializeField] private List<TilePrefabEntry> resourcePrefabs = new List<TilePrefabEntry>();
        [SerializeField] private List<TilePrefabEntry> spawnPrefabs = new List<TilePrefabEntry>();

        public IReadOnlyList<TilePrefabEntry> TerrainTiles => terrainTiles;
        public IReadOnlyList<TilePrefabEntry> RampTiles => rampTiles;
        public IReadOnlyList<TilePrefabEntry> CliffTiles => cliffTiles;
        public IReadOnlyList<TilePrefabEntry> PropPrefabs => propPrefabs;
        public IReadOnlyList<TilePrefabEntry> ResourcePrefabs => resourcePrefabs;
        public IReadOnlyList<TilePrefabEntry> SpawnPrefabs => spawnPrefabs;

        public IEnumerable<TilePrefabEntry> GetEntries(PlacedMapObjectType objectType)
        {
            switch (objectType)
            {
                case PlacedMapObjectType.Terrain:
                    foreach (var entry in terrainTiles) yield return entry;
                    foreach (var entry in rampTiles) yield return entry;
                    foreach (var entry in cliffTiles) yield return entry;
                    break;
                case PlacedMapObjectType.Prop:
                    foreach (var entry in propPrefabs) yield return entry;
                    break;
                case PlacedMapObjectType.Resource:
                    foreach (var entry in resourcePrefabs) yield return entry;
                    break;
                case PlacedMapObjectType.Spawn:
                    foreach (var entry in spawnPrefabs) yield return entry;
                    break;
            }
        }

        public TilePrefabEntry FindEntry(string id, PlacedMapObjectType objectType)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var entry in GetEntries(objectType))
            {
                if (entry != null && entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
