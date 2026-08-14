using ProjectS.Maps;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Maps.Editor
{
    public static class MapEditorPreviewBuilder
    {
        public const string PreviewRootName = "__ProjectS_MapPreview";

        public static GameObject RebuildPreview(MapDefinition map)
        {
            ClearPreview();

            if (map == null)
            {
                return null;
            }

            map.EnsureCells();
            var root = new GameObject(PreviewRootName);
            BuildTerrain(map, root.transform);
            BuildPlacedObjects(map, root.transform);
            BuildResources(map, root.transform);
            BuildSpawns(map, root.transform);
            Selection.activeGameObject = root;
            return root;
        }

        public static void ClearPreview()
        {
            var existing = GameObject.Find(PreviewRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void BuildTerrain(MapDefinition map, Transform root)
        {
            if (map.TileSet == null)
            {
                return;
            }

            var terrainRoot = new GameObject("Terrain").transform;
            terrainRoot.SetParent(root, false);

            foreach (var cell in map.Cells)
            {
                if (cell == null || string.IsNullOrEmpty(cell.tileId))
                {
                    continue;
                }

                var entry = map.TileSet.FindEntry(cell.tileId, PlacedMapObjectType.Terrain);
                InstantiatePreview(entry?.prefab, terrainRoot, map.GridToWorld(cell.Position, cell.heightLevel), cell.rotationY, entry?.displayName ?? cell.tileId);
            }
        }

        private static void BuildPlacedObjects(MapDefinition map, Transform root)
        {
            var objectRoot = new GameObject("Objects").transform;
            objectRoot.SetParent(root, false);

            foreach (var placedObject in map.PlacedObjects)
            {
                if (placedObject == null)
                {
                    continue;
                }

                InstantiatePreview(
                    placedObject.prefab,
                    objectRoot,
                    map.GridToWorld(placedObject.gridPosition, placedObject.heightLevel),
                    placedObject.rotationY,
                    placedObject.id);
            }
        }

        private static void BuildResources(MapDefinition map, Transform root)
        {
            var resourceRoot = new GameObject("Resources").transform;
            resourceRoot.SetParent(root, false);

            foreach (var resource in map.ResourceNodes)
            {
                if (resource == null)
                {
                    continue;
                }

                InstantiatePreview(resource.prefab, resourceRoot, map.GridToWorld(resource.gridPosition, resource.heightLevel), 0f, resource.id);
            }
        }

        private static void BuildSpawns(MapDefinition map, Transform root)
        {
            var spawnRoot = new GameObject("Spawns").transform;
            spawnRoot.SetParent(root, false);

            foreach (var spawn in map.SpawnPoints)
            {
                if (spawn == null)
                {
                    continue;
                }

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"{spawn.id}_{spawn.playerIndex}";
                marker.transform.SetParent(spawnRoot, false);
                marker.transform.position = map.GridToWorld(spawn.gridPosition, spawn.heightLevel) + Vector3.up * 0.1f;
                marker.transform.localScale = new Vector3(map.TileSize * 0.45f, 0.1f, map.TileSize * 0.45f);
            }
        }

        private static GameObject InstantiatePreview(GameObject prefab, Transform parent, Vector3 position, float rotationY, string fallbackName)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(parent, false);
            }

            if (instance == null)
            {
                return null;
            }

            instance.name = string.IsNullOrEmpty(fallbackName) ? "Map Preview Object" : fallbackName;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            return instance;
        }
    }
}
