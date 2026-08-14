using UnityEngine;

namespace ProjectS.Maps
{
    public sealed class MapRuntimeBuilder : MonoBehaviour
    {
        [SerializeField] private MapDefinition mapDefinition;
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
            var root = new GameObject($"{mapDefinition.MapName}_Runtime");
            root.transform.SetParent(transform, false);

            BuildTerrain(root.transform);
            BuildPlacedObjects(root.transform);
            BuildResources(root.transform);
            BuildSpawns(root.transform);
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
                CreateInstance(entry?.prefab, terrainRoot, mapDefinition.GridToWorld(cell.Position, cell.heightLevel), cell.rotationY, entry?.displayName ?? cell.tileId);
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
    }
}
