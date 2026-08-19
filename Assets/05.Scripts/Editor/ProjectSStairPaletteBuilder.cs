using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Editor
{
    public static class ProjectSStairPaletteBuilder
    {
        private const string PaletteFolder = "Assets/Assets/Map Editor/Tile Pallete";
        private const string PaletteName = "Stair Palette";
        private const string PalettePath = PaletteFolder + "/" + PaletteName + ".prefab";
        private const string StairTileFolder = "Assets/Assets/Cainos/Pixel Art Top Down - Basic/Tile Palette/TP Stair";

        [InitializeOnLoadMethod]
        private static void EnsureStairPaletteExists()
        {
            if (!File.Exists(PalettePath))
            {
                RebuildStairPalette();
            }
        }

        [MenuItem("Assets/Create/Project S/Tilemaps/Rebuild Stair Palette")]
        public static void RebuildStairPalette()
        {
            if (!AssetDatabase.IsValidFolder(PaletteFolder))
            {
                Directory.CreateDirectory(PaletteFolder);
                AssetDatabase.Refresh();
            }

            if (File.Exists(PalettePath))
            {
                AssetDatabase.DeleteAsset(PalettePath);
            }

            var palette = GridPaletteUtility.CreateNewPalette(
                PaletteFolder,
                PaletteName,
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual,
                Vector3.one,
                GridLayout.CellSwizzle.XYZ);

            if (palette == null)
            {
                Debug.LogError("Failed to create Stair Palette.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PalettePath);
            try
            {
                var tilemap = root.GetComponentInChildren<Tilemap>();
                if (tilemap == null)
                {
                    Debug.LogError("Stair Palette does not contain a Tilemap.");
                    return;
                }

                tilemap.name = "Stair";
                tilemap.ClearAllTiles();

                for (var i = 1; i <= 10; i++)
                {
                    var tilePath = $"{StairTileFolder}/TX Tileset Stair_{i}.asset";
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                    if (tile == null)
                    {
                        Debug.LogWarning($"Missing stair tile: {tilePath}");
                        continue;
                    }

                    tilemap.SetTile(new Vector3Int(i - 1, 0, 0), tile);
                }

                tilemap.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
