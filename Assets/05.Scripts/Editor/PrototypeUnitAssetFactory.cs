using System.IO;
using ProjectS.Units;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Units.Editor
{
    public static class PrototypeUnitAssetFactory
    {
        private enum UnitColorFaction
        {
            Red,
            Blue,
            Green
        }

        private const string MaterialFolder = "Assets/02.Materials/Units";
        private const string PrefabFolder = "Assets/03.Prefabs/Units";

        [MenuItem("Tools/Project S/Create Prototype Unit Assets")]
        public static void CreatePrototypeUnitAssets()
        {
            EnsureFolder("Assets/02.Materials", "Units");
            EnsureFolder("Assets/03.Prefabs", "Units");

            var red = CreateOrUpdateMaterial(UnitColorFaction.Red, new Color(0.86f, 0.18f, 0.16f));
            var blue = CreateOrUpdateMaterial(UnitColorFaction.Blue, new Color(0.16f, 0.35f, 0.9f));
            var green = CreateOrUpdateMaterial(UnitColorFaction.Green, new Color(0.18f, 0.68f, 0.28f));

            CreateOrUpdateFactionUnits(UnitColorFaction.Red, red);
            CreateOrUpdateFactionUnits(UnitColorFaction.Blue, blue);
            CreateOrUpdateFactionUnits(UnitColorFaction.Green, green);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateOrUpdateFactionUnits(UnitColorFaction faction, Material material)
        {
            CreateOrUpdateUnitPrefab(faction, PrototypeUnitType.Worker, material);
            CreateOrUpdateUnitPrefab(faction, PrototypeUnitType.Soldier, material);
            CreateOrUpdateUnitPrefab(faction, PrototypeUnitType.Spliter, material);
            CreateOrUpdateUnitPrefab(faction, PrototypeUnitType.Ranger, material);
        }

        private static Material CreateOrUpdateMaterial(UnitColorFaction faction, Color color)
        {
            var path = $"{MaterialFolder}/{faction}_Unit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindDefaultShader())
                {
                    name = $"{faction}_Unit"
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateOrUpdateUnitPrefab(UnitColorFaction faction, PrototypeUnitType unitType, Material material)
        {
            var unitName = $"{faction}_{unitType}";
            var path = $"{PrefabFolder}/{unitName}.prefab";
            var root = new GameObject(unitName);

            try
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.77f, 1f);
                collider.offset = new Vector2(0f, 0.1f);
                collider.isTrigger = true;

                var rigidbody = root.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.gravityScale = 0f;

                var visual = new GameObject("Visual");
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                var spriteRenderer = visual.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateUnitSprite(unitType);
                spriteRenderer.color = material.color;

                var selectionRing = new GameObject("SelectionRing");
                selectionRing.transform.SetParent(root.transform, false);
                selectionRing.transform.localPosition = Vector3.zero;
                var ringRenderer = selectionRing.AddComponent<SpriteRenderer>();
                ringRenderer.sprite = CreateSelectionSprite();
                ringRenderer.color = new Color(0.2f, 0.8f, 1f, 0.75f);
                ringRenderer.sortingOrder = -1;
                selectionRing.SetActive(false);

                var status = root.AddComponent<PrototypeUnitStatus>();
                ApplyStatus(status, GetTeam(faction), unitType);
                root.AddComponent<UnitPathAgent>();
                root.AddComponent<UnitCommandAgent>();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static UnitTeam GetTeam(UnitColorFaction faction)
        {
            switch (faction)
            {
                case UnitColorFaction.Red:
                    return UnitTeam.Team1;
                case UnitColorFaction.Blue:
                    return UnitTeam.Team2;
                case UnitColorFaction.Green:
                    return UnitTeam.Team3;
                default:
                    return UnitTeam.Team1;
            }
        }

        private static void ApplyStatus(PrototypeUnitStatus status, UnitTeam team, PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    status.Initialize(
                        UnitTrial.Human,
                        team,
                        unitType,
                        MovementDomain.Ground,
                        UnitRole.Resource | UnitRole.Builder,
                        AttackDistanceType.Melee,
                        AttackPowerType.Physical,
                        PlacementType.Movable,
                        UnitGrade.Common,
                        AttackTargetType.SingleTarget,
                        60f,
                        3f,
                        0f,
                        1.2f,
                        1f,
                        3f,
                        1,
                        Vector2Int.one,
                        true,
                        false,
                        0f);
                    break;

                case PrototypeUnitType.Soldier:
                    status.Initialize(
                        UnitTrial.Human,
                        team,
                        unitType,
                        MovementDomain.Ground,
                        UnitRole.Combat,
                        AttackDistanceType.Melee,
                        AttackPowerType.Physical,
                        PlacementType.Movable,
                        UnitGrade.Common,
                        AttackTargetType.SingleTarget,
                        100f,
                        10f,
                        0f,
                        1.5f,
                        1f,
                        3.2f,
                        1,
                        Vector2Int.one,
                        false,
                        false,
                        0f);
                    break;

                case PrototypeUnitType.Spliter:
                    status.Initialize(
                        UnitTrial.Human,
                        team,
                        unitType,
                        MovementDomain.Ground,
                        UnitRole.Combat,
                        AttackDistanceType.Melee,
                        AttackPowerType.Physical,
                        PlacementType.Movable,
                        UnitGrade.Common,
                        AttackTargetType.AreaAttack,
                        90f,
                        8f,
                        0f,
                        1.4f,
                        0.9f,
                        3f,
                        3,
                        Vector2Int.one,
                        false,
                        true,
                        2f);
                    break;

                case PrototypeUnitType.Ranger:
                    status.Initialize(
                        UnitTrial.Human,
                        team,
                        unitType,
                        MovementDomain.Ground,
                        UnitRole.Combat,
                        AttackDistanceType.Ranged,
                        AttackPowerType.Physical,
                        PlacementType.Movable,
                        UnitGrade.Common,
                        AttackTargetType.SingleTarget,
                        70f,
                        8f,
                        0f,
                        6f,
                        0.8f,
                        2.8f,
                        1,
                        Vector2Int.one,
                        false,
                        false,
                        0f);
                    break;
            }
        }

        private static Shader FindDefaultShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        private static Sprite CreateUnitSprite(PrototypeUnitType unitType)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = $"{unitType}_PrototypeSprite",
                filterMode = FilterMode.Point
            };

            var center = new Vector2(15.5f, 15.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var inside = unitType == PrototypeUnitType.Ranger
                        ? Mathf.Abs(delta.x) + Mathf.Abs(delta.y) < 17f
                        : delta.magnitude < 13f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateSelectionSprite()
        {
            var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
            {
                name = "SelectionRing_PrototypeSprite",
                filterMode = FilterMode.Point
            };

            var center = new Vector2(23.5f, 23.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var radius = (new Vector2(x, y) - center).magnitude;
                    texture.SetPixel(x, y, radius > 18f && radius < 22f ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
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
