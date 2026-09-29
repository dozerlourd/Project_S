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
        private const string TemplateCsvPath = ResourcesFolder + "/UnitBalanceCatalog_Template.csv";

        [MenuItem("Tools/Project S/Unit Balance/Create Default Catalog")]
        public static void CreateDefaultCatalog()
        {
            var template = LoadOrCreateTemplateCsv();
            var catalog = LoadOrCreateCatalog();
            catalog.Configure(template, UnitBalanceCatalog.CreateFallbackEntries());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
        }

        [MenuItem("Tools/Project S/Unit Balance/Create Korean CSV Template")]
        public static void CreateKoreanCsvTemplate()
        {
            Selection.activeObject = LoadOrCreateTemplateCsv();
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

        public static void CreateDefaultCatalogAndTemplate()
        {
            CreateDefaultCatalog();
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

        private static TextAsset LoadOrCreateTemplateCsv()
        {
            EnsureResourcesFolder();
            if (!File.Exists(TemplateCsvPath))
            {
                File.WriteAllText(TemplateCsvPath, CreateTemplateCsv(), System.Text.Encoding.UTF8);
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
            return
                "unitType,유닛명,roles,공격거리분류,체력,공격력,사거리,감지거리,공격속도,이동속도,시야,인구,고급인구,자원채집,광역피해반경,최대대상수,미네랄,가스,생산시간,생산수,생산건물,생산조건\n"
                + "Worker,Worker,Resource|Builder,Melee,60,3,1.2,4,1,3,6,1,0,TRUE,0,0,50,0,5,1,MainBase,\n"
                + "Soldier,Soldier,Combat,Melee,100,10,1.5,5,1,3.2,7,2,0,FALSE,0,0,100,0,7,1,Production,\n"
                + "Spliter,Spliter,Combat,Melee,90,8,1.4,5,0.9,3,7,3,0,FALSE,2,3,125,0,8,1,SpliterProduction,\n"
                + "Ranger,Ranger,Combat,Ranged,70,8,6,8,0.8,2.8,9,2,0,FALSE,0,0,100,25,8,1,Production,\n"
                + "Tank,Tank,Combat,Ranged,260,26,5.5,7,0.55,2,8,3,0,FALSE,0,0,150,0,10,1,Production,\n"
                + "Striker,Striker,Combat,Melee,55,6,0.8,4,3,4.2,6,1,0,FALSE,0,0,75,0,6,1,Production,\n"
                + "Swarm,Swarm x3,Combat,Melee,45,4,1.1,4.5,1.1,3.5,5.5,1,0,FALSE,0,0,120,0,8,3,Production,\n"
                + "Medic,Medic,Support,Ranged,90,0,0,0,0,3.6,10,2,0,FALSE,0,0,50,0,6,1,MaintenanceBay,\n"
                + "Siege,Siege,Combat|Siege,Ranged,180,36,8.5,9.5,0.45,1.7,8,4,0,FALSE,2.5,6,50,0,6,1,VehicleFactory,\n"
                + "Scout,Scout,Combat,Ranged,55,5,4.5,7,1.2,4.8,11,1,0,FALSE,0,0,50,0,6,1,SignalRelay,\n";
        }
    }
}
