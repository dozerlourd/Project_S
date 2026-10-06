using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class SimpleSkirmishAiEasyPlayModeTests
    {
        private static readonly Type SimpleSkirmishAiType = GetGameplayType("ProjectS.AI.SimpleSkirmishAI");
        private static readonly Type PlayerResourceWalletType = GetGameplayType("ProjectS.Resources.PlayerResourceWallet");
        private static readonly Type ResourceAmountType = GetGameplayType("ProjectS.Resources.ResourceAmount");
        private static readonly Type ResourceTypeType = GetGameplayType("ProjectS.Resources.ResourceType");
        private static readonly Type TeamUpgradeResearchType = GetGameplayType("ProjectS.Upgrades.TeamUpgradeResearch");
        private static readonly Type UnitUpgradeDefinitionType = GetGameplayType("ProjectS.Upgrades.UnitUpgradeDefinition");
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type UnitProductionQueueType = GetGameplayType("ProjectS.Buildings.UnitProductionQueue");
        private static readonly Type UnitProductionDefinitionType = GetGameplayType("ProjectS.Buildings.UnitProductionDefinition");
        private static readonly Type AiBuildingTemplateRegistryType = GetGameplayType("ProjectS.Buildings.AiBuildingTemplateRegistry");
        private static readonly Type UnlockRuleCatalogType = GetGameplayType("ProjectS.Unlocks.UnlockRuleCatalog");
        private static readonly Type UnlockRuleCsvParserType = GetGameplayType("ProjectS.Unlocks.UnlockRuleCsvParser");
        private static readonly Type TeamUnlockStateType = GetGameplayType("ProjectS.Unlocks.TeamUnlockState");

        [Test]
        public void EasyArmyComposition_UsesSoldiersWithAlternatingOccasionalSupportUnits()
        {
            var aiObject = new GameObject("Easy Composition AI");
            var ai = aiObject.AddComponent(SimpleSkirmishAiType);
            ((Behaviour)ai).enabled = false;

            SetField(ai, "minimumCombatUnitsForOccasionalUnit", 5);
            SetField(ai, "occasionalUnitFrequency", 5);

            var selections = new PrototypeUnitType[10];
            for (var produced = 0; produced < selections.Length; produced++)
            {
                SetField(ai, "successfulCombatProductions", produced);
                selections[produced] = (PrototypeUnitType)Invoke(ai, "SelectEasyCombatUnitType", 5);
            }

            Assert.That(selections.Count(type => type == PrototypeUnitType.Soldier), Is.EqualTo(8));
            Assert.That(selections[4], Is.EqualTo(PrototypeUnitType.Ranger));
            Assert.That(selections[9], Is.EqualTo(PrototypeUnitType.Striker));
            Assert.That(selections, Has.None.EqualTo(PrototypeUnitType.Tank));
            Assert.That(selections, Has.None.EqualTo(PrototypeUnitType.Spliter));

            SetField(ai, "successfulCombatProductions", 4);
            Assert.That(
                (PrototypeUnitType)Invoke(ai, "SelectEasyCombatUnitType", 4),
                Is.EqualTo(PrototypeUnitType.Soldier));

            Object.DestroyImmediate(aiObject);
        }

        [Test]
        public void SpecializedProduction_RequiresMatureArmyAndBasicProductionQuota()
        {
            var aiObject = new GameObject("Specialized Production Quota AI");
            var ai = aiObject.AddComponent(SimpleSkirmishAiType);
            ((Behaviour)ai).enabled = false;

            SetField(ai, "minimumCombatUnitsForSpecializedUnit", 6);
            SetField(ai, "specializedUnitFrequency", 4);
            SetField(ai, "successfulCombatProductions", 3);

            Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 6), Is.False);

            SetField(ai, "successfulCombatProductions", 4);
            Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 5), Is.False);
            Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 6), Is.True);

            SetField(ai, "successfulSpecializedProductions", 1);
            Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 6), Is.False);
            SetField(ai, "successfulCombatProductions", 8);
            Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 6), Is.True);

            Object.DestroyImmediate(aiObject);
        }

        [Test]
        public void SpecializedProduction_FallsBackToAnAvailableSpecializedQueue()
        {
            var aiObject = new GameObject("Specialized Production Fallback AI");
            var ai = aiObject.AddComponent(SimpleSkirmishAiType);
            ((Behaviour)ai).enabled = false;
            var medicPrefab = new GameObject("AI Medic Prefab");
            var maintenanceBay = new GameObject("AI Maintenance Bay");

            try
            {
                SetField(ai, "minimumCombatUnitsForSpecializedUnit", 6);
                SetField(ai, "specializedUnitFrequency", 4);
                SetField(ai, "successfulCombatProductions", 4);

                var status = maintenanceBay.AddComponent(BuildingStatusType);
                var maintenanceBayKind = Enum.Parse(BuildingKindType, "MaintenanceBay");
                Invoke(status, "Initialize", UnitTeam.Team8, maintenanceBayKind, new Vector2Int(3, 2), true);

                var definition = Activator.CreateInstance(UnitProductionDefinitionType);
                Invoke(
                    definition,
                    "Configure",
                    "Medic",
                    PrototypeUnitType.Medic,
                    medicPrefab,
                    CreateResourceAmount(0, 0),
                    10f,
                    0,
                    1,
                    null);
                var allowedBuildings = Array.CreateInstance(BuildingKindType, 1);
                allowedBuildings.SetValue(maintenanceBayKind, 0);
                Invoke(definition, "ConfigureAllowedProductionBuildings", allowedBuildings);

                var definitions = Array.CreateInstance(UnitProductionDefinitionType, 1);
                definitions.SetValue(definition, 0);
                var queue = maintenanceBay.AddComponent(UnitProductionQueueType);
                Invoke(queue, "Configure", null, null, definitions, 2, Vector3.right, Vector3.right * 2f);

                var queueListType = typeof(System.Collections.Generic.List<>).MakeGenericType(UnitProductionQueueType);
                var queues = Activator.CreateInstance(queueListType);
                queueListType.GetMethod("Add")?.Invoke(queues, new[] { queue });

                Assert.That((bool)Invoke(ai, "TryEnqueueSpecializedCombatUnit", queues, 6), Is.True);
                var activeDefinition = GetProperty(queue, "ActiveProduction");
                Assert.That(GetProperty(activeDefinition, "UnitType"), Is.EqualTo(PrototypeUnitType.Medic));
                Assert.That(GetField(ai, "successfulSpecializedProductions"), Is.EqualTo(1));
                Assert.That(GetField(ai, "nextSpecializedUnitIndex"), Is.EqualTo(2));
                Assert.That((bool)Invoke(ai, "ShouldAttemptSpecializedProduction", 6), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(maintenanceBay);
                Object.DestroyImmediate(medicPrefab);
                Object.DestroyImmediate(aiObject);
            }
        }

        [Test]
        public void SpecializedConstruction_RespectsUnlockAndStartsOnlyOneMatchingBuilding()
        {
            var stateObject = new GameObject("Specialized Construction Unlocks");
            var state = stateObject.AddComponent(TeamUnlockStateType);
            var catalog = ScriptableObject.CreateInstance(UnlockRuleCatalogType);
            var productionBuilding = new GameObject("Specialized Construction Production");
            var worker = new GameObject("Specialized Construction Worker");
            var completedPrefab = new GameObject("Vehicle Factory Completed Prefab");
            var aiObject = new GameObject("Specialized Construction AI");
            Component pendingSite = null;

            try
            {
                var csv =
                    "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\n"
                    + "vehicle-runtime,건물,VehicleFactory,Production,,vehicle-license,TRUE,AI gate";
                var entries = InvokeStatic(UnlockRuleCsvParserType, "Parse", csv);
                Invoke(catalog, "Configure", null, entries);
                Invoke(state, "Configure", UnitTeam.Team8, Array.Empty<string>());
                Invoke(state, "ConfigureRuleCatalog", catalog);

                var productionStatus = productionBuilding.AddComponent(BuildingStatusType);
                Invoke(
                    productionStatus,
                    "Initialize",
                    UnitTeam.Team8,
                    Enum.Parse(BuildingKindType, "Production"),
                    new Vector2Int(2, 2),
                    true);

                worker.AddComponent<BoxCollider2D>().isTrigger = true;
                var workerStatus = worker.AddComponent<PrototypeUnitStatus>();
                workerStatus.ConfigurePrototypeDefaults(PrototypeUnitType.Worker, UnitTeam.Team8);

                var ai = aiObject.AddComponent(SimpleSkirmishAiType);
                ((Behaviour)ai).enabled = false;
                Invoke(ai, "Configure", UnitTeam.Team8, UnitTeam.Team7, 0, 7, Vector3.zero);
                SetField(ai, "minimumCombatUnitsForSpecializedUnit", 0);
                SetField(ai, "specializedUnitFrequency", 4);
                SetField(ai, "successfulCombatProductions", 4);
                SetField(ai, "specializedConstructionAttemptInterval", 8f);

                var templates = aiObject.AddComponent(AiBuildingTemplateRegistryType);
                Invoke(templates, "Configure", UnitTeam.Team8, null, null, null);
                Invoke(
                    templates,
                    "RegisterTemplate",
                    Enum.Parse(BuildingKindType, "VehicleFactory"),
                    completedPrefab,
                    CreateResourceAmount(0, 0),
                    10f,
                    new Vector2Int(3, 3));

                Assert.That((bool)Invoke(ai, "TryBeginSpecializedProductionBuilding"), Is.False);
                Assert.That(GetField(ai, "pendingConstructionSite"), Is.Null);

                Invoke(state, "Unlock", "vehicle-license");
                SetField(ai, "nextSpecializedConstructionAttemptTime", 0f);
                Assert.That((bool)Invoke(ai, "TryBeginSpecializedProductionBuilding"), Is.True);
                pendingSite = GetField(ai, "pendingConstructionSite") as Component;
                Assert.That(pendingSite, Is.Not.Null);
                Assert.That(GetField(ai, "pendingConstructionKind").ToString(), Is.EqualTo("VehicleFactory"));
                Assert.That((bool)Invoke(ai, "TryBeginSpecializedProductionBuilding"), Is.False);
            }
            finally
            {
                if (pendingSite != null)
                {
                    Object.DestroyImmediate(pendingSite.gameObject);
                }

                Object.DestroyImmediate(aiObject);
                Object.DestroyImmediate(completedPrefab);
                Object.DestroyImmediate(worker);
                Object.DestroyImmediate(productionBuilding);
                Object.DestroyImmediate(stateObject);
                Object.DestroyImmediate(catalog);
            }
        }

        [UnityTest]
        public IEnumerator EasyResearch_RequiresArmyAndReserveThenResearchesWeaponBeforeMobility()
        {
            var walletObject = new GameObject("Easy AI Wallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team8, CreateResourceAmount(250, 100));

            var weapon = CreateUpgrade("Weapon Calibration", UnitUpgradeKind.AttackDamage, 100, 25);
            var mobility = CreateUpgrade("Mobility Tuning", UnitUpgradeKind.MovementSpeed, 75, 25);
            var definitions = Array.CreateInstance(UnitUpgradeDefinitionType, 2);
            definitions.SetValue(weapon, 0);
            definitions.SetValue(mobility, 1);

            var researchObject = new GameObject("Easy AI Research");
            var research = researchObject.AddComponent(TeamUpgradeResearchType);
            Invoke(research, "Configure", UnitTeam.Team8, wallet, definitions);
            var unlockStateObject = new GameObject("Easy AI Research Unlock State");
            var unlockState = unlockStateObject.AddComponent(TeamUnlockStateType);
            Invoke(unlockState, "Configure", UnitTeam.Team8, Array.Empty<string>());

            var aiObject = new GameObject("Easy Research AI");
            var ai = aiObject.AddComponent(SimpleSkirmishAiType);
            ((Behaviour)ai).enabled = false;
            Invoke(ai, "Configure", UnitTeam.Team8, UnitTeam.Team7, 0, 7, Vector3.zero);
            SetField(ai, "minimumCombatUnitsForResearch", 2);
            SetField(ai, "researchResourceReserve", CreateResourceAmount(200, 50));

            RunResearchDecision(ai);
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.Null, "병력이 없으면 연구하면 안 됩니다.");

            var unitOne = CreateCombatUnit("Easy AI Soldier One", UnitTeam.Team8);
            var unitTwo = CreateCombatUnit("Easy AI Soldier Two", UnitTeam.Team8);
            RunResearchDecision(ai);
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.Null, "연구비 외 여유 자원이 없으면 연구하면 안 됩니다.");

            var addResource = Enum.Parse(ResourceTypeType, "Minerals");
            Invoke(wallet, "Add", addResource, 100);
            RunResearchDecision(ai);
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.EqualTo(weapon));

            for (var i = 0; i < 120 && GetProperty(research, "ActiveDefinition") != null; i++)
            {
                yield return null;
            }
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.Null);
            Assert.That(
                (bool)InvokeStatic(TeamUnlockStateType, "IsResearchCompleted", UnitTeam.Team8, "attack-damage"),
                Is.True);

            Invoke(wallet, "Add", addResource, 25);
            RunResearchDecision(ai);
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.EqualTo(mobility));

            Object.Destroy(aiObject);
            Object.Destroy(researchObject);
            Object.Destroy(unlockStateObject);
            Object.Destroy(walletObject);
            Object.Destroy(unitOne);
            Object.Destroy(unitTwo);
            Object.Destroy(weapon);
            Object.Destroy(mobility);
            yield return null;
        }

        private static ScriptableObject CreateUpgrade(
            string name,
            UnitUpgradeKind kind,
            int minerals,
            int gas)
        {
            var definition = (ScriptableObject)ScriptableObject.CreateInstance(UnitUpgradeDefinitionType);
            definition.name = name;
            Invoke(definition, "Configure", name, kind, CreateResourceAmount(minerals, gas), 0.01f, 1f);
            Invoke(
                definition,
                "ConfigureResearchId",
                kind == UnitUpgradeKind.AttackDamage ? "attack-damage" : "movement-speed");
            return definition;
        }

        private static GameObject CreateCombatUnit(string name, UnitTeam team)
        {
            var unit = new GameObject(name);
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                PrototypeUnitType.Soldier,
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
                5f,
                1f,
                3f,
                1,
                Vector2Int.one,
                false,
                false,
                0f);
            return unit;
        }

        private static void RunResearchDecision(Component ai)
        {
            SetField(ai, "nextResearchAttemptTime", 0f);
            Invoke(ai, "RunEasyResearch");
        }

        private static object CreateResourceAmount(int minerals, int gas)
        {
            return Activator.CreateInstance(ResourceAmountType, minerals, gas);
        }

        private static object InvokeStatic(Type targetType, string methodName, params object[] arguments)
        {
            var method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method was not found: {methodName}");
            return method.Invoke(null, arguments);
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Gameplay type was not found: {typeName}");
            return type;
        }

        private static object GetProperty(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
        }

        private static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field was not found: {fieldName}");
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != methodName)
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();
                    if (parameters.Length != arguments.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] != null && !parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                });
            Assert.That(method, Is.Not.Null, $"Method was not found: {methodName}");
            return method.Invoke(target, arguments);
        }
    }
}
