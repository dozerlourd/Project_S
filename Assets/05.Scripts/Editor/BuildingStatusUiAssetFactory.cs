using System.IO;
using System.Linq;
using ProjectS.Buildings;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Editor
{
    [InitializeOnLoad]
    public static class BuildingStatusUiAssetFactory
    {
        private const string TextureFolder = "Assets/01.Textures/UI/Game Scene/Status UI";
        private const string RangeTexturePath = TextureFolder + "/Building_Range_Indicator.png";
        private const string HealthBarTexturePath = TextureFolder + "/Building_HealthBar.png";
        private const string BuildingPrefabFolder = "Assets/03.Prefabs/Buildings";
        private static bool pending;

        static BuildingStatusUiAssetFactory()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ScheduleEnsureAssetsAndBindings();
        }

        [MenuItem("Tools/Project S/Generate Building Status UI Assets")]
        public static void EnsureAssetsAndBindings()
        {
            pending = false;
            EnsureFolder("Assets/01.Textures/UI/Game Scene", "Status UI");
            var createdTexture = CreateRangeTextureIfMissing();
            createdTexture |= CreateHealthBarTextureIfMissing();
            if (createdTexture)
            {
                AssetDatabase.Refresh();
                ScheduleEnsureAssetsAndBindings();
                return;
            }

            var rangeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RangeTexturePath);
            var healthBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HealthBarTexturePath);
            if (rangeSprite == null || healthBarSprite == null)
            {
                Debug.LogError("[BuildingStatusUI] Failed to load generated status UI sprites.");
                return;
            }

            foreach (var path in EnumerateBuildingPrefabPaths())
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                    var building = root.GetComponent<Structure>();
                    if (building == null)
                    {
                        continue;
                    }

                    var healthBar = root.GetComponent<BuildingHealthBar>() ?? root.AddComponent<BuildingHealthBar>();
                    healthBar.ConfigureSprite(healthBarSprite);
                    if (root.GetComponent<BuildingFogVisibilityTarget>() == null)
                    {
                        root.AddComponent<BuildingFogVisibilityTarget>();
                    }

                    var source = GetRangeSource(root, building);
                    if (source.HasValue)
                    {
                        var rangeIndicator = root.GetComponent<BuildingRangeIndicator>() ?? root.AddComponent<BuildingRangeIndicator>();
                        rangeIndicator.Configure(source.Value, rangeSprite);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void ScheduleEnsureAssetsAndBindings()
        {
            if (pending || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            pending = true;
            EditorApplication.delayCall += EnsureAssetsAndBindings;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ScheduleEnsureAssetsAndBindings();
            }
        }

        private static BuildingRangeIndicatorSource? GetRangeSource(GameObject root, Structure building)
        {
            if (root.GetComponent<BuildingAutoTurret>() != null)
            {
                return BuildingRangeIndicatorSource.AutoTurretAttack;
            }

            if (root.GetComponent<BuildingSpeedAura>() != null)
            {
                return BuildingRangeIndicatorSource.SpeedAura;
            }

            return building.Kind == BuildingKind.SignalRelay
                ? BuildingRangeIndicatorSource.StructureVision
                : null;
        }

        private static string[] EnumerateBuildingPrefabPaths()
        {
            return Directory.GetFiles(BuildingPrefabFolder, "*.prefab", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .ToArray();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static bool CreateRangeTextureIfMissing()
        {
            if (File.Exists(RangeTexturePath))
            {
                return false;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var index = y * size + x;
                    pixels[index] = distance > 1f
                        ? new Color32(0, 0, 0, 0)
                        : distance >= 0.9f
                            ? new Color32(12, 58, 188, 235)
                            : new Color32(46, 150, 255, 76);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(RangeTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            ConfigureImporter(RangeTexturePath, FilterMode.Bilinear);
            return true;
        }

        private static bool CreateHealthBarTextureIfMissing()
        {
            if (File.Exists(HealthBarTexturePath))
            {
                return false;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(HealthBarTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            ConfigureImporter(HealthBarTexturePath, FilterMode.Point);
            return true;
        }

        private static void ConfigureImporter(string path, FilterMode filterMode)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.filterMode = filterMode;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
    }
}
