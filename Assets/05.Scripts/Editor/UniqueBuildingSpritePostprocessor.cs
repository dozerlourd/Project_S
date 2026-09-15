using System;
using UnityEditor;

namespace ProjectS.Editor
{
    [InitializeOnLoad]
    public sealed class UniqueBuildingSpritePostprocessor : AssetPostprocessor
    {
        private const string UniqueTexturePrefix = "Assets/01.Textures/Buildings/Unique/";
        private static bool isApplyQueued;

        static UniqueBuildingSpritePostprocessor()
        {
            QueueSpriteApplication();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (isApplyQueued || !ContainsUniqueBuildingTexture(importedAssets))
            {
                return;
            }

            QueueSpriteApplication();
        }

        private static bool ContainsUniqueBuildingTexture(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                if (assetPath.StartsWith(UniqueTexturePrefix, StringComparison.Ordinal)
                    && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void QueueSpriteApplication()
        {
            if (isApplyQueued)
            {
                return;
            }

            isApplyQueued = true;
            EditorApplication.delayCall += ApplySpritesToPrefabs;
        }

        private static void ApplySpritesToPrefabs()
        {
            isApplyQueued = false;
            MapCreateSceneSetupBuilder.ApplyUniqueCoreBuildingSprites();
        }
    }
}
