using System;
using UnityEditor;

namespace ProjectS.Units.Editor
{
    [InitializeOnLoad]
    internal sealed class UnitTextureReferencePostprocessor : AssetPostprocessor
    {
        private const string TextureFolder = "Assets/03.Prefabs/Units/Textures/";
        private static bool repairQueued;
        private static bool repairInProgress;

        static UnitTextureReferencePostprocessor()
        {
            QueueRepair();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (repairInProgress)
            {
                return;
            }

            if (ContainsUnitTexture(importedAssets) || ContainsUnitTexture(movedAssets))
            {
                QueueRepair();
            }
        }

        private static bool ContainsUnitTexture(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                if (assetPath.StartsWith(TextureFolder, StringComparison.Ordinal)
                    && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void QueueRepair()
        {
            if (repairQueued)
            {
                return;
            }

            repairQueued = true;
            EditorApplication.delayCall += RepairAfterImports;
        }

        private static void RepairAfterImports()
        {
            repairQueued = false;
            repairInProgress = true;
            try
            {
                PrototypeUnitAssetFactory.RepairUnitTextureSpriteReferences();
            }
            finally
            {
                repairInProgress = false;
            }
        }
    }
}
