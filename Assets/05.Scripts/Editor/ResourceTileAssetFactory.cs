using System.Collections.Generic;
using System.IO;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Editor
{
    public static class ResourceTileAssetFactory
    {
        private const string ResourceTexturePath = "Assets/Resources/ResourceNodes.png";
        private const string ResourceTileFolder = "Assets/Assets/Tilemaps/Resource Tiles";
        private const string MineralsSpriteName = "ResourceNodes_Minerals";
        private const string GasSpriteName = "ResourceNodes_Gas";

        [MenuItem("Tools/Project S/Resources/Prepare Resource Tile Palette")]
        public static void PrepareResourceTilePalette()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ResourceTexturePath);
            if (texture == null)
            {
                Debug.LogError($"Missing resource sprite sheet at {ResourceTexturePath}.");
                return;
            }

            ConfigureSpriteSheet(texture);
            EnsureFolder("Assets/Assets", "Tilemaps");
            EnsureFolder("Assets/Assets/Tilemaps", "Resource Tiles");
            CreateOrUpdateResourceTile(MineralsSpriteName, ResourceTileType.Minerals);
            CreateOrUpdateResourceTile(GasSpriteName, ResourceTileType.Gas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Project S/Resources/Add Resource Tilemap To Active Scene")]
        public static void AddResourceTilemapToActiveScene()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("Cannot add a Resource Tilemap because the active scene has no Grid.");
                return;
            }

            var tilemaps = grid.GetComponentsInChildren<Tilemap>(true);
            for (var i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] != null && tilemaps[i].gameObject.name == "Resource")
                {
                    Selection.activeGameObject = tilemaps[i].gameObject;
                    return;
                }
            }

            var resourceObject = new GameObject("Resource");
            resourceObject.transform.SetParent(grid.transform, false);
            resourceObject.AddComponent<Tilemap>();
            var renderer = resourceObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 12;
            resourceObject.AddComponent<ResourceTilemapNodeSynchronizer>();
            Selection.activeGameObject = resourceObject;
            EditorSceneManager.MarkSceneDirty(resourceObject.scene);
        }

        [MenuItem("Assets/Create/Project S/Resources/Resource Tiles From Selected Sprites")]
        public static void CreateResourceTilesFromSelectedSprites()
        {
            EnsureFolder("Assets/Assets", "Tilemaps");
            EnsureFolder("Assets/Assets/Tilemaps", "Resource Tiles");
            foreach (var sprite in CollectSelectedSprites())
            {
                var tile = ScriptableObject.CreateInstance<ResourceTile>();
                tile.sprite = sprite;
                tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
                var resourceType = sprite.name.ToLowerInvariant().Contains("gas")
                    || sprite.name.ToLowerInvariant().Contains("geyser")
                    ? ResourceTileType.Gas
                    : ResourceTileType.Minerals;
                tile.Configure(
                    resourceType,
                    resourceType == ResourceTileType.Gas ? 2500 : 1500,
                    resourceType == ResourceTileType.Gas ? 6 : 8,
                    resourceType == ResourceTileType.Gas ? 1.8f : 1.2f,
                    resourceType == ResourceTileType.Gas ? 1.05f : 0.95f);

                var name = ObjectNames.NicifyVariableName(sprite.name).Replace(" ", "_");
                AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath($"{ResourceTileFolder}/{name}.asset"));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Create/Project S/Resources/Resource Tiles From Selected Sprites", true)]
        public static bool CanCreateResourceTilesFromSelectedSprites()
        {
            return CollectSelectedSprites().Count > 0;
        }

        private static List<Sprite> CollectSelectedSprites()
        {
            var sprites = new List<Sprite>();
            var seen = new HashSet<Sprite>();
            foreach (var selection in Selection.objects)
            {
                if (selection is Sprite sprite)
                {
                    AddSprite(sprites, seen, sprite);
                    continue;
                }

                var path = selection != null ? AssetDatabase.GetAssetPath(selection) : string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite subSprite)
                    {
                        AddSprite(sprites, seen, subSprite);
                    }
                }
            }

            return sprites;
        }

        private static void AddSprite(List<Sprite> sprites, HashSet<Sprite> seen, Sprite sprite)
        {
            if (sprite != null && seen.Add(sprite))
            {
                sprites.Add(sprite);
            }
        }

        private static void ConfigureSpriteSheet(Texture2D texture)
        {
            var importer = AssetImporter.GetAtPath(ResourceTexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not configure sprite importer for {ResourceTexturePath}.");
                return;
            }

            var halfWidth = texture.width / 2;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            var existingSpriteRects = dataProvider.GetSpriteRects();
            dataProvider.SetSpriteRects(new[]
            {
                CreateSpriteRect(
                    MineralsSpriteName,
                    new Rect(0f, 0f, halfWidth, texture.height),
                    FindSpriteId(existingSpriteRects, MineralsSpriteName)),
                CreateSpriteRect(
                    GasSpriteName,
                    new Rect(halfWidth, 0f, texture.width - halfWidth, texture.height),
                    FindSpriteId(existingSpriteRects, GasSpriteName))
            });
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static SpriteRect CreateSpriteRect(string spriteName, Rect rect, GUID spriteId)
        {
            return new SpriteRect
            {
                name = spriteName,
                rect = rect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = spriteId
            };
        }

        private static GUID FindSpriteId(IEnumerable<SpriteRect> spriteRects, string spriteName)
        {
            foreach (var spriteRect in spriteRects)
            {
                if (spriteRect.name == spriteName)
                {
                    return spriteRect.spriteID;
                }
            }

            return GUID.Generate();
        }

        private static void CreateOrUpdateResourceTile(string spriteName, ResourceTileType resourceType)
        {
            var sprite = FindSprite(spriteName);
            if (sprite == null)
            {
                Debug.LogError($"Could not find split sprite {spriteName} in {ResourceTexturePath}.");
                return;
            }

            var path = $"{ResourceTileFolder}/{spriteName}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<ResourceTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<ResourceTile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = sprite;
            tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            tile.Configure(
                resourceType,
                resourceType == ResourceTileType.Gas ? 2500 : 1500,
                resourceType == ResourceTileType.Gas ? 6 : 8,
                resourceType == ResourceTileType.Gas ? 1.8f : 1.2f,
                resourceType == ResourceTileType.Gas ? 1.05f : 0.95f);
            EditorUtility.SetDirty(tile);
        }

        private static Sprite FindSprite(string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ResourceTexturePath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
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
