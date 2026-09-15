using System;
using System.IO;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Units.Editor
{
    public static class PrototypeUnitAssetFactory
    {
        private const string PrefabFolder = "Assets/03.Prefabs/Units";
        private const string TextureFolder = PrefabFolder + "/Textures";
        private const string PlayerSpriteSheetPath = "Assets/Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Player.png";
        private const float ConceptArtPixelsPerUnit = 800f;
        private const float LegacyPixelsPerUnit = 32f;

        [MenuItem("Tools/Project S/Create B Prototype Unit Assets")]
        public static void CreatePrototypeUnitAssets()
        {
            EnsureFolder("Assets/03.Prefabs", "Units");
            EnsureFolder(PrefabFolder, "Textures");

            var unitSprite = FindSprite("TX Player F");
            var selectionSprite = FindSprite("TX Shadow Player");
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Worker, CreateOrUpdateTextureSprite(PrototypeUnitType.Worker, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Soldier, CreateOrUpdateTextureSprite(PrototypeUnitType.Soldier, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Spliter, CreateOrUpdateTextureSprite(PrototypeUnitType.Spliter, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Ranger, CreateOrUpdateTextureSprite(PrototypeUnitType.Ranger, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Tank, CreateOrUpdateTextureSprite(PrototypeUnitType.Tank, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Striker, CreateOrUpdateTextureSprite(PrototypeUnitType.Striker, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Swarm, CreateOrUpdateTextureSprite(PrototypeUnitType.Swarm, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Medic, CreateOrUpdateTextureSprite(PrototypeUnitType.Medic, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Siege, CreateOrUpdateTextureSprite(PrototypeUnitType.Siege, unitSprite), selectionSprite);
            CreateOrUpdateUnitPrefab(PrototypeUnitType.Scout, CreateOrUpdateTextureSprite(PrototypeUnitType.Scout, unitSprite), selectionSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Project S/Apply Baked Unit Textures")]
        public static void ApplyBakedUnitTextures()
        {
            EnsureFolder("Assets/03.Prefabs", "Units");
            EnsureFolder(PrefabFolder, "Textures");

            var sourceSprite = FindSprite("TX Player F");
            foreach (PrototypeUnitType unitType in Enum.GetValues(typeof(PrototypeUnitType)))
            {
                var prefabPath = $"{PrefabFolder}/B_{unitType}.prefab";
                if (!File.Exists(prefabPath))
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var renderer = root.GetComponent<SpriteRenderer>();
                    if (renderer == null)
                    {
                        throw new InvalidOperationException($"{prefabPath} is missing its root SpriteRenderer.");
                    }

                    renderer.sprite = CreateOrUpdateTextureSprite(unitType, sourceSprite);
                    renderer.color = Color.white;
                    foreach (var teamIndicator in root.GetComponentsInChildren<UnitTeamIndicator>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(teamIndicator);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Project S/Repair Unit Texture Sprite References")]
        public static void RepairUnitTextureSpriteReferences()
        {
            foreach (PrototypeUnitType unitType in Enum.GetValues(typeof(PrototypeUnitType)))
            {
                var texturePath = $"{TextureFolder}/B_{unitType}.png";
                var prefabPath = $"{PrefabFolder}/B_{unitType}.prefab";
                if (!File.Exists(texturePath) || !File.Exists(prefabPath))
                {
                    continue;
                }

                var sprite = ConfigureTextureSprite(texturePath, GetPixelsPerUnitForTexture(texturePath));
                if (sprite == null)
                {
                    Debug.LogError($"[UnitAssets] Could not load a sprite from {texturePath}.");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var renderer = root.GetComponent<SpriteRenderer>();
                    if (renderer == null)
                    {
                        Debug.LogError($"[UnitAssets] {prefabPath} is missing its root SpriteRenderer.");
                        continue;
                    }

                    renderer.sprite = sprite;
                    renderer.color = Color.white;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.sortingLayerName = "Units";
                    renderer.sortingOrder = 20;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void CreateOrUpdateUnitPrefab(
            PrototypeUnitType unitType,
            Sprite unitSprite,
            Sprite selectionSprite)
        {
            var unitName = $"B_{unitType}";
            var root = new GameObject(unitName);

            try
            {
                var spriteRenderer = root.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = unitSprite;
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingLayerName = "Units";
                spriteRenderer.sortingOrder = 20;

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.77f, 1f);
                collider.offset = new Vector2(0f, 0.1f);
                collider.isTrigger = true;

                var rigidbody = root.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.gravityScale = 0f;
                rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;

                CreateSelectionRing(root.transform, selectionSprite);

                var status = root.AddComponent<PrototypeUnitStatus>();
                status.ConfigurePrototypeDefaults(unitType);

                root.AddComponent<UnitPathAgent>();
                root.AddComponent<UnitCommandAgent>();
                root.AddComponent<UnitHealth>();
                root.AddComponent<UnitHealthBar>();

                if (status.Roles.HasFlag(UnitRole.Combat) || status.PhysicalAttackPower > 0f || status.MagicalAttackPower > 0f)
                {
                    root.AddComponent<UnitCombat>();
                    root.AddComponent<TemporaryAttackEffect>();
                }

                if (unitType == PrototypeUnitType.Worker)
                {
                    root.AddComponent<WorkerGatherController>();
                    root.AddComponent<WorkerConstructionController>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{unitName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateSelectionRing(Transform parent, Sprite selectionSprite)
        {
            var selectionRing = new GameObject("SelectionRing");
            selectionRing.transform.SetParent(parent, false);
            selectionRing.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            selectionRing.transform.localScale = new Vector3(0.85f, 0.45f, 1f);
            selectionRing.SetActive(false);

            var renderer = selectionRing.AddComponent<SpriteRenderer>();
            renderer.sprite = selectionSprite;
            renderer.color = new Color(1f, 1f, 1f, 0.75f);
            renderer.sortingLayerName = "Units";
            renderer.sortingOrder = 18;
        }

        private static Sprite FindSprite(string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PlayerSpriteSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new InvalidOperationException($"Could not find '{spriteName}' in {PlayerSpriteSheetPath}.");
        }

        private static Sprite CreateOrUpdateTextureSprite(PrototypeUnitType unitType, Sprite sourceSprite)
        {
            var texturePath = $"{TextureFolder}/B_{unitType}.png";
            if (File.Exists(texturePath))
            {
                // Generated concept art is authoritative. The fallback source sprite is only for missing assets.
                return ConfigureTextureSprite(texturePath, GetPixelsPerUnitForTexture(texturePath));
            }

            var sourcePath = AssetDatabase.GetAssetPath(sourceSprite.texture);
            var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            var wasReadable = sourceImporter != null && sourceImporter.isReadable;
            if (sourceImporter != null && !wasReadable)
            {
                sourceImporter.isReadable = true;
                sourceImporter.SaveAndReimport();
            }

            try
            {
                var rect = sourceSprite.textureRect;
                var width = Mathf.RoundToInt(rect.width);
                var height = Mathf.RoundToInt(rect.height);
                var readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                var pixels = readableTexture.GetPixels(
                    Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
                var tint = GetTextureColor(unitType);
                for (var index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a > 0f)
                    {
                        pixels[index] = Color.Lerp(pixels[index], tint, 0.6f);
                    }
                }

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                texture.SetPixels(pixels);
                texture.Apply();

                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                return ConfigureTextureSprite(texturePath, sourceSprite.pixelsPerUnit);
            }
            finally
            {
                if (sourceImporter != null && !wasReadable)
                {
                    sourceImporter.isReadable = false;
                    sourceImporter.SaveAndReimport();
                }
            }
        }

        private static Sprite ConfigureTextureSprite(string texturePath, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        private static float GetPixelsPerUnitForTexture(string texturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            return texture != null && texture.width > 256
                ? ConceptArtPixelsPerUnit
                : LegacyPixelsPerUnit;
        }

        private static Color GetTextureColor(PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker: return new Color(0.62f, 0.68f, 0.55f, 1f);
                case PrototypeUnitType.Soldier: return new Color(0.58f, 0.48f, 0.38f, 1f);
                case PrototypeUnitType.Spliter: return new Color(0.55f, 0.38f, 0.28f, 1f);
                case PrototypeUnitType.Ranger: return new Color(0.46f, 0.42f, 0.58f, 1f);
                case PrototypeUnitType.Tank: return new Color(0.42f, 0.57f, 0.7f, 1f);
                case PrototypeUnitType.Striker: return new Color(0.9f, 0.42f, 0.22f, 1f);
                case PrototypeUnitType.Swarm: return new Color(0.72f, 0.78f, 0.28f, 1f);
                case PrototypeUnitType.Medic: return new Color(0.25f, 0.78f, 0.62f, 1f);
                case PrototypeUnitType.Siege: return new Color(0.68f, 0.32f, 0.2f, 1f);
                case PrototypeUnitType.Scout: return new Color(0.25f, 0.65f, 0.86f, 1f);
                default: throw new ArgumentOutOfRangeException(nameof(unitType), unitType, null);
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
