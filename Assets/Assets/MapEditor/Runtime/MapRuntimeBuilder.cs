using UnityEngine;

namespace ProjectS.Maps
{
    public sealed class MapRuntimeBuilder : MonoBehaviour
    {
        [SerializeField] private MapDefinition mapDefinition;
        [SerializeField] private MapRuntimeBuildMode buildModeOverride = MapRuntimeBuildMode.PreferBakedPrefab;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool clearBeforeBuild = true;

        public MapDefinition MapDefinition { get => mapDefinition; set => mapDefinition = value; }

        private void Start()
        {
            if (buildOnStart)
            {
                Build();
            }
        }

        [ContextMenu("Build Map")]
        public void Build()
        {
            if (mapDefinition == null)
            {
                Debug.LogWarning("MapRuntimeBuilder has no MapDefinition.", this);
                return;
            }

            if (clearBeforeBuild)
            {
                Clear();
            }

            mapDefinition.EnsureCells();
            if (ShouldUseBakedPrefab())
            {
                BuildFromBakedPrefab();
                return;
            }

            var root = new GameObject($"{mapDefinition.MapName}_Runtime");
            root.transform.SetParent(transform, false);

            BuildTerrain(root.transform);
            MapAutoWallBuilder.BuildHeightWalls(mapDefinition, root.transform);
            BuildPlacedObjects(root.transform);
            BuildResources(root.transform);
            BuildSpawns(root.transform);
        }

        private bool ShouldUseBakedPrefab()
        {
            if (mapDefinition.BakedMapPrefab == null)
            {
                return false;
            }

            var mode = buildModeOverride == MapRuntimeBuildMode.PreferBakedPrefab
                ? mapDefinition.RuntimeBuildMode
                : buildModeOverride;

            return mode == MapRuntimeBuildMode.UseBakedPrefab
                || mode == MapRuntimeBuildMode.PreferBakedPrefab;
        }

        private void BuildFromBakedPrefab()
        {
            var instance = Instantiate(mapDefinition.BakedMapPrefab, transform);
            instance.name = $"{mapDefinition.MapName}_Runtime";
        }

        [ContextMenu("Clear Built Map")]
        public void Clear()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        public bool IsWalkable(Vector2Int gridPosition)
        {
            return mapDefinition != null && mapDefinition.GetCell(gridPosition)?.walkable == true;
        }

        public bool IsBuildable(Vector2Int gridPosition)
        {
            return mapDefinition != null && mapDefinition.GetCell(gridPosition)?.buildable == true;
        }

        private void BuildTerrain(Transform root)
        {
            if (mapDefinition.TileSet == null)
            {
                return;
            }

            var terrainRoot = new GameObject("Terrain").transform;
            terrainRoot.SetParent(root, false);

            foreach (var cell in mapDefinition.Cells)
            {
                if (cell == null || string.IsNullOrEmpty(cell.tileId))
                {
                    continue;
                }

                var entry = mapDefinition.TileSet.FindEntry(cell.tileId, PlacedMapObjectType.Terrain);
                var position = GetPlacementWorldPosition(cell.Position, cell.heightLevel, GetRotatedFootprint(entry, cell.rotationY));
                CreateInstance(entry?.prefab, terrainRoot, position, cell.rotationY, entry?.displayName ?? cell.tileId);
            }
        }

        private void BuildPlacedObjects(Transform root)
        {
            var objectRoot = new GameObject("Objects").transform;
            objectRoot.SetParent(root, false);

            foreach (var placedObject in mapDefinition.PlacedObjects)
            {
                if (placedObject == null || placedObject.objectType == PlacedMapObjectType.Resource || placedObject.objectType == PlacedMapObjectType.Spawn)
                {
                    continue;
                }

                CreateInstance(
                    placedObject.prefab,
                    objectRoot,
                    mapDefinition.GridToWorld(placedObject.gridPosition, placedObject.heightLevel),
                    placedObject.rotationY,
                    placedObject.id);
            }
        }

        private void BuildResources(Transform root)
        {
            var resourceRoot = new GameObject("Resources").transform;
            resourceRoot.SetParent(root, false);

            foreach (var resource in mapDefinition.ResourceNodes)
            {
                if (resource == null)
                {
                    continue;
                }

                CreateInstance(resource.prefab, resourceRoot, mapDefinition.GridToWorld(resource.gridPosition, resource.heightLevel), 0f, resource.id);
            }
        }

        private void BuildSpawns(Transform root)
        {
            var spawnRoot = new GameObject("Spawns").transform;
            spawnRoot.SetParent(root, false);

            foreach (var spawn in mapDefinition.SpawnPoints)
            {
                if (spawn == null)
                {
                    continue;
                }

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"{spawn.id}_{spawn.playerIndex}";
                marker.transform.SetParent(spawnRoot, false);
                marker.transform.position = mapDefinition.GridToWorld(spawn.gridPosition, spawn.heightLevel) + new Vector3(0f, 0.05f, 0f);
                marker.transform.localScale = new Vector3(mapDefinition.TileSize * 0.5f, 0.1f, mapDefinition.TileSize * 0.5f);
            }
        }

        private static void CreateInstance(GameObject prefab, Transform parent, Vector3 position, float rotationY, string fallbackName)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab, parent);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(parent, false);
            }

            instance.name = string.IsNullOrEmpty(fallbackName) ? "Map Object" : fallbackName;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
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

        private Vector3 GetPlacementWorldPosition(Vector2Int gridPosition, int heightLevel, Vector2Int footprint)
        {
            var offset = new Vector3(
                (Mathf.Max(1, footprint.x) - 1) * mapDefinition.TileSize * 0.5f,
                0f,
                (Mathf.Max(1, footprint.y) - 1) * mapDefinition.TileSize * 0.5f);
            return mapDefinition.GridToWorld(gridPosition, heightLevel) + offset;
        }
    }
}
