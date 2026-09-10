using System.IO;
using ProjectS.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Editor
{
    public static class ObstacleTileAssetFactory
    {
        private const string TilemapFolder = "Assets/Assets/Tilemaps";
        private const string ObstacleTileFolder = "Assets/Assets/Tilemaps/Obstacle Tiles";
        private const string ObstacleTexturePath = "Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.png";
        private const string ObstacleTilePath = "Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.asset";
        private const string ObstacleLayerName = "Obstacle";

        [InitializeOnLoadMethod]
        private static void CreateMissingObstacleAssetsAfterEditorLoad()
        {
            EditorApplication.delayCall += EnsureObstacleAssets;
        }

        [MenuItem("Tools/Project S/Tilemaps/Prepare Obstacle Tile Palette")]
        public static void PrepareObstacleTilePalette()
        {
            EnsureObstacleAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ProjectSTile>(ObstacleTilePath);
        }

        [MenuItem("Tools/Project S/Tilemaps/Add Obstacle Tilemap To Active Scene")]
        public static void AddObstacleTilemapToActiveScene()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("Cannot add an Obstacle Tilemap because the active scene has no Grid.");
                return;
            }

            var existingTilemap = FindObstacleTilemap(grid);
            if (existingTilemap != null)
            {
                ConfigureObstacleTilemap(existingTilemap);
                RefreshWorldCache(grid);
                Selection.activeGameObject = existingTilemap.gameObject;
                EditorSceneManager.MarkSceneDirty(existingTilemap.gameObject.scene);
                return;
            }

            var obstacleObject = new GameObject(ObstacleLayerName);
            Undo.RegisterCreatedObjectUndo(obstacleObject, "Create Obstacle Tilemap");
            obstacleObject.transform.SetParent(grid.transform, false);
            var tilemap = obstacleObject.AddComponent<Tilemap>();
            obstacleObject.AddComponent<TilemapRenderer>();
            ConfigureObstacleTilemap(tilemap);
            RefreshWorldCache(grid);
            Selection.activeGameObject = obstacleObject;
            EditorSceneManager.MarkSceneDirty(obstacleObject.scene);
        }

        private static void EnsureObstacleAssets()
        {
            EnsureFolder("Assets/Assets", "Tilemaps");
            EnsureFolder(TilemapFolder, "Obstacle Tiles");
            var sprite = GetOrCreateObstacleSprite();
            if (sprite == null)
            {
                Debug.LogError("Could not create the temporary Obstacle tile sprite.");
                return;
            }

            var tile = AssetDatabase.LoadAssetAtPath<ProjectSTile>(ObstacleTilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<ProjectSTile>();
                AssetDatabase.CreateAsset(tile, ObstacleTilePath);
            }

            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            var serializedTile = new SerializedObject(tile);
            serializedTile.FindProperty("terrainType").enumValueIndex = (int)ProjectSTerrainType.Prop;
            serializedTile.FindProperty("walkable").boolValue = false;
            serializedTile.FindProperty("buildable").boolValue = false;
            serializedTile.FindProperty("blocksMovement").boolValue = true;
            serializedTile.FindProperty("blocksConstruction").boolValue = true;
            serializedTile.FindProperty("blocksVision").boolValue = true;
            serializedTile.FindProperty("movementCost").floatValue = 1f;
            serializedTile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
        }

        private static Sprite GetOrCreateObstacleSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ObstacleTexturePath);
            if (sprite != null)
            {
                return sprite;
            }

            var absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ObstacleTexturePath);
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.SetPixels(CreateStonePixels(texture.width, texture.height));
            texture.Apply(false, false);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(ObstacleTexturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ObstacleTexturePath) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(ObstacleTexturePath);
        }

        private static Color[] CreateStonePixels(int width, int height)
        {
            var pixels = new Color[width * height];
            var border = new Color(0.13f, 0.16f, 0.18f, 1f);
            var darkStone = new Color(0.27f, 0.31f, 0.32f, 1f);
            var stone = new Color(0.43f, 0.48f, 0.47f, 1f);
            var highlight = new Color(0.62f, 0.66f, 0.62f, 1f);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var edge = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                    var diagonal = (x + y) % 11 < 2 || (x - y + width) % 13 == 0;
                    var highlightBand = x > 7 && x < 24 && y > 18 && y < 25;
                    pixels[y * width + x] = edge ? border : highlightBand ? highlight : diagonal ? darkStone : stone;
                }
            }

            return pixels;
        }

        private static Tilemap FindObstacleTilemap(Grid grid)
        {
            foreach (var tilemap in grid.GetComponentsInChildren<Tilemap>(true))
            {
                if (tilemap != null && tilemap.gameObject.name == ObstacleLayerName)
                {
                    return tilemap;
                }
            }

            return null;
        }

        private static void ConfigureObstacleTilemap(Tilemap tilemap)
        {
            var obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
            if (obstacleLayer >= 0)
            {
                tilemap.gameObject.layer = obstacleLayer;
            }

            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = "FrontProps";
                renderer.sortingOrder = 0;
            }
        }

        private static void RefreshWorldCache(Grid grid)
        {
            var tilemapWorld = grid.GetComponent<ProjectSTilemapWorld>()
                ?? grid.GetComponentInChildren<ProjectSTilemapWorld>(true);
            if (tilemapWorld == null)
            {
                return;
            }

            tilemapWorld.ResolveReferences();
            tilemapWorld.MarkNavigationCacheDirty();
            EditorUtility.SetDirty(tilemapWorld);
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = Path.Combine(parent, child).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
