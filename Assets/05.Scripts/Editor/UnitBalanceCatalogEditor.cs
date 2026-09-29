using System.IO;
using ProjectS.Units;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Editor
{
    public static class UnitBalanceCatalogEditor
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string CatalogPath = ResourcesFolder + "/" + UnitBalanceCatalog.ResourceName + ".asset";

        [MenuItem("Tools/Project S/Unit Balance/Create Default Catalog")]
        public static void CreateDefaultCatalog()
        {
            var catalog = LoadOrCreateCatalog();
            catalog.Configure(null, UnitBalanceCatalog.CreateFallbackEntries());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
        }

        [MenuItem("Tools/Project S/Unit Balance/Import Selected CSV")]
        public static void ImportSelectedCsv()
        {
            var csv = Selection.activeObject as TextAsset;
            if (csv == null)
            {
                Debug.LogWarning("Select a unit balance CSV TextAsset before importing.");
                return;
            }

            var catalog = LoadOrCreateCatalog();
            catalog.Configure(csv, UnitBalanceCsvParser.Parse(csv.text));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log($"Imported {catalog.Entries.Count} unit balance entries from {AssetDatabase.GetAssetPath(csv)}.");
        }

        private static UnitBalanceCatalog LoadOrCreateCatalog()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<UnitBalanceCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<UnitBalanceCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }
    }
}
