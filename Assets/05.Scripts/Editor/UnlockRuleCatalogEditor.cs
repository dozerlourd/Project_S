using System;
using System.IO;
using System.Text;
using ProjectS.Unlocks;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Editor
{
    public static class UnlockRuleCatalogEditor
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string CatalogPath = ResourcesFolder + "/" + UnlockRuleCatalog.ResourceName + ".asset";
        private const string TemplateCsvPath = ResourcesFolder + "/UnlockRuleCatalog_Template.csv";

        [MenuItem("Tools/Project S/Unlock Rules/Select Korean CSV Template")]
        public static void SelectKoreanCsvTemplate()
        {
            Selection.activeObject = LoadOrCreateTemplateCsv();
        }

        [MenuItem("Tools/Project S/Unlock Rules/Import Selected CSV")]
        public static void ImportSelectedCsv()
        {
            var csv = Selection.activeObject as TextAsset;
            if (csv == null || !string.Equals(Path.GetExtension(AssetDatabase.GetAssetPath(csv)), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("Select an unlock rule CSV TextAsset before importing.");
                return;
            }

            try
            {
                var catalog = LoadOrCreateCatalog();
                catalog.Configure(csv, UnlockRuleCsvParser.Parse(csv.text));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                Selection.activeObject = catalog;
                Debug.Log(
                    $"Imported {catalog.Entries.Count} unlock rule entries from {AssetDatabase.GetAssetPath(csv)}. "
                    + "The catalog is not connected to runtime production or construction yet.");
            }
            catch (FormatException exception)
            {
                Debug.LogError($"Unlock rule CSV import failed: {exception.Message}");
            }
        }

        [MenuItem("Tools/Project S/Unlock Rules/Import Default Template")]
        public static void ImportDefaultTemplate()
        {
            Selection.activeObject = LoadOrCreateTemplateCsv();
            ImportSelectedCsv();
        }

        private static UnlockRuleCatalog LoadOrCreateCatalog()
        {
            EnsureResourcesFolder();
            var catalog = AssetDatabase.LoadAssetAtPath<UnlockRuleCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<UnlockRuleCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static TextAsset LoadOrCreateTemplateCsv()
        {
            EnsureResourcesFolder();
            if (!File.Exists(TemplateCsvPath))
            {
                File.WriteAllText(TemplateCsvPath, CreateTemplateCsv(), new UTF8Encoding(true));
                AssetDatabase.ImportAsset(TemplateCsvPath);
            }

            return AssetDatabase.LoadAssetAtPath<TextAsset>(TemplateCsvPath);
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }
        }

        private static string CreateTemplateCsv()
        {
            return "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\r\n";
        }
    }
}
