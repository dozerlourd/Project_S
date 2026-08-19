using System.Collections.Generic;
using System.IO;
using ProjectS.Tilemaps;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Editor
{
    public static class ProjectSTileAssetFactory
    {
        private const string DefaultTileFolder = "Assets/Assets/Tilemaps/Tiles";

        [MenuItem("Assets/Create/Project S/Tilemaps/Tiles From Selected Sprites")]
        public static void CreateTilesFromSelectedSprites()
        {
            EnsureFolder("Assets/Assets", "Tilemaps");
            EnsureFolder("Assets/Assets/Tilemaps", "Tiles");

            var sprites = CollectSelectedSprites();
            foreach (var sprite in sprites)
            {
                CreateTileAsset(sprite);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Create/Project S/Tilemaps/Tiles From Selected Sprites", true)]
        public static bool CanCreateTilesFromSelectedSprites()
        {
            return CollectSelectedSprites().Count > 0;
        }

        private static void CreateTileAsset(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<ProjectSTile>();
            tile.sprite = sprite;
            tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;

            var fileName = ObjectNames.NicifyVariableName(sprite.name).Replace(" ", "_");
            var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultTileFolder}/{fileName}.asset");
            AssetDatabase.CreateAsset(tile, path);
        }

        private static List<Sprite> CollectSelectedSprites()
        {
            var sprites = new List<Sprite>();
            var seen = new HashSet<Sprite>();
            foreach (var selection in Selection.objects)
            {
                if (selection == null)
                {
                    continue;
                }

                if (selection is Sprite sprite)
                {
                    AddSprite(sprites, seen, sprite);
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(selection);
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
