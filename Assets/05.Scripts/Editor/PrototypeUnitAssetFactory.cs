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
                var collider = root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 1f, 0f);
                collider.height = 2f;
                collider.radius = 0.5f;

                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());

                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                selectionRing.name = "SelectionRing";
                selectionRing.transform.SetParent(root.transform, false);
                selectionRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                selectionRing.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
                Object.DestroyImmediate(selectionRing.GetComponent<Collider>());
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
