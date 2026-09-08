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
                ApplyStatus(status, unitType);
                status.ConfigureSupplyCost(GetSupplyCost(unitType));

                root.AddComponent<UnitPathAgent>();
                root.AddComponent<UnitCommandAgent>();
                root.AddComponent<UnitCombat>();
                root.AddComponent<UnitHealth>();
                root.AddComponent<UnitHealthBar>();
                root.AddComponent<TemporaryAttackEffect>();

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

        private static int GetSupplyCost(PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    return 1;
                case PrototypeUnitType.Soldier:
                case PrototypeUnitType.Ranger:
                    return 2;
                case PrototypeUnitType.Spliter:
                    return 3;
                case PrototypeUnitType.Tank:
                    return 3;
                case PrototypeUnitType.Striker:
                case PrototypeUnitType.Swarm:
                    return 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(unitType), unitType, null);
            }
        }

        private static void ApplyStatus(PrototypeUnitStatus status, PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Resource | UnitRole.Builder, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 60f, 3f, 0f,
                        1.2f, 4f, 1f, 3f, 1, Vector2Int.one, true, false, 0f);
                    break;
                case PrototypeUnitType.Soldier:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 100f, 10f, 0f,
                        1.5f, 5f, 1f, 3.2f, 1, Vector2Int.one, false, false, 0f);
                    break;
                case PrototypeUnitType.Spliter:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.AreaAttack, 90f, 8f, 0f,
                        1.4f, 5f, 0.9f, 3f, 3, Vector2Int.one, false, true, 2f);
                    break;
                case PrototypeUnitType.Ranger:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Ranged, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 70f, 8f, 0f,
                        6f, 8f, 0.8f, 2.8f, 1, Vector2Int.one, false, false, 0f);
                    break;
                case PrototypeUnitType.Tank:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 260f, 5f, 0f,
                        1.4f, 4.5f, 0.65f, 2.4f, 1, Vector2Int.one, false, false, 0f);
                    break;
                case PrototypeUnitType.Striker:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 55f, 6f, 0f,
                        0.8f, 4f, 3f, 4.2f, 1, Vector2Int.one, false, false, 0f);
                    break;
                case PrototypeUnitType.Swarm:
                    status.Initialize(UnitTrial.Human, UnitTeam.Team1, unitType, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 45f, 4f, 0f,
                        1.1f, 4.5f, 1.1f, 3.5f, 1, Vector2Int.one, false, false, 0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(unitType), unitType, null);
            }
        }

        private static Sprite CreateOrUpdateTextureSprite(PrototypeUnitType unitType, Sprite sourceSprite)
        {
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

                var texturePath = $"{TextureFolder}/B_{unitType}.png";
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = sourceSprite.pixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
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
