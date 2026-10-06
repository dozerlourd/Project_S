using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnlockRuleCatalogTests
    {
        private static readonly Type ParserType = GetGameplayType("ProjectS.Unlocks.UnlockRuleCsvParser");
        private static readonly Type CatalogType = GetGameplayType("ProjectS.Unlocks.UnlockRuleCatalog");
        private static readonly Type TeamUnlockStateType = GetGameplayType("ProjectS.Unlocks.TeamUnlockState");
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type UnitProductionDefinitionType = GetGameplayType("ProjectS.Buildings.UnitProductionDefinition");
        private static readonly Type UnitProductionQueueType = GetGameplayType("ProjectS.Buildings.UnitProductionQueue");
        private static readonly Type BuildingConstructionDefinitionType = GetGameplayType("ProjectS.Buildings.BuildingConstructionDefinition");
        private static readonly Type BuildingPlacementServiceType = GetGameplayType("ProjectS.Buildings.BuildingPlacementService");
        private static readonly Type ResourceAmountType = GetGameplayType("ProjectS.Resources.ResourceAmount");

        [Test]
        public void HeaderOnlyTemplateImportsNoRules()
        {
            var csv = "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\r\n";
            var entries = (Array)InvokeStatic(ParserType, "Parse", csv);

            Assert.That(entries.Length, Is.Zero);
        }

        [Test]
        public void InactiveRulePreservesAllEditableRequirementColumns()
        {
            var csv =
                "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\r\n"
                + "scout-rule,유닛,Scout,SignalRelay|Production,recon-research,field-recon,FALSE,비활성 입력 예시\r\n";
            var entries = (Array)InvokeStatic(ParserType, "Parse", csv);
            var entry = entries.GetValue(0);

            Assert.That(entries.Length, Is.EqualTo(1));
            Assert.That(GetProperty(entry, "RuleId"), Is.EqualTo("scout-rule"));
            Assert.That(GetProperty(entry, "TargetType").ToString(), Is.EqualTo("Unit"));
            Assert.That(GetProperty(entry, "TargetId"), Is.EqualTo("Scout"));
            CollectionAssert.AreEqual(
                new[] { "SignalRelay", "Production" },
                (ICollection)GetProperty(entry, "RequiredBuildings"));
            CollectionAssert.AreEqual(
                new[] { "recon-research" },
                (ICollection)GetProperty(entry, "RequiredResearchIds"));
            CollectionAssert.AreEqual(
                new[] { "field-recon" },
                (ICollection)GetProperty(entry, "PrerequisiteUnlockIds"));
            Assert.That(GetProperty(entry, "Active"), Is.False);
        }

        [Test]
        public void EmptyAndInactiveCatalogs_DoNotBlockExistingRuntimeActions()
        {
            var stateObject = new GameObject("Empty Unlock Catalog State");
            var state = stateObject.AddComponent(TeamUnlockStateType);
            var catalog = ScriptableObject.CreateInstance(CatalogType);

            try
            {
                Invoke(state, "Configure", UnitTeam.Team8, Array.Empty<string>());
                Invoke(catalog, "Configure", null, Array.CreateInstance(GetGameplayType("ProjectS.Unlocks.UnlockRuleEntry"), 0));
                Invoke(state, "ConfigureRuleCatalog", catalog);

                Assert.That(CanProduce(UnitTeam.Team8, PrototypeUnitType.Scout, out var emptyReason), Is.True);
                Assert.That(emptyReason, Is.Empty);

                var inactiveCsv =
                    "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\n"
                    + "inactive-scout,유닛,Scout,SignalRelay,recon-research,field-recon,FALSE,ignored";
                Invoke(catalog, "Configure", null, InvokeStatic(ParserType, "Parse", inactiveCsv));

                Assert.That(CanProduce(UnitTeam.Team8, PrototypeUnitType.Scout, out var inactiveReason), Is.True);
                Assert.That(inactiveReason, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(stateObject);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ActiveRules_BlockQueueAndBuildDefinitionUntilAllColumnsAreSatisfied()
        {
            var stateObject = new GameObject("Runtime Unlock State");
            var state = stateObject.AddComponent(TeamUnlockStateType);
            var catalog = ScriptableObject.CreateInstance(CatalogType);
            var productionBuilding = new GameObject("Runtime Unlock Production");
            var soldierPrefab = new GameObject("Runtime Unlock Soldier Prefab");
            var signalRelayPrefab = new GameObject("Runtime Unlock Signal Relay Prefab");
            var placementObject = new GameObject("Runtime Unlock Placement");

            try
            {
                var csv =
                    "규칙ID,대상종류,대상ID,필요건물,필요연구,선행해금,활성,메모\n"
                    + "soldier-runtime,유닛,Soldier,Production,attack-damage,combat-license,TRUE,unit gate\n"
                    + "relay-runtime,건물,SignalRelay,Production,attack-damage,combat-license,TRUE,building gate";
                Invoke(catalog, "Configure", null, InvokeStatic(ParserType, "Parse", csv));
                Invoke(state, "Configure", UnitTeam.Team8, Array.Empty<string>());
                Invoke(state, "ConfigureRuleCatalog", catalog);

                Assert.That(CanProduce(UnitTeam.Team8, PrototypeUnitType.Soldier, out var missingBuildingReason), Is.False);
                StringAssert.Contains("completed building 'Production'", missingBuildingReason);

                var productionKind = Enum.Parse(BuildingKindType, "Production");
                var productionStatus = productionBuilding.AddComponent(BuildingStatusType);
                Invoke(productionStatus, "Initialize", UnitTeam.Team8, productionKind, new Vector2Int(2, 2), true);

                var unitDefinition = Activator.CreateInstance(UnitProductionDefinitionType);
                Invoke(
                    unitDefinition,
                    "Configure",
                    "Catalog Soldier",
                    PrototypeUnitType.Soldier,
                    soldierPrefab,
                    CreateResourceAmount(0, 0),
                    5f,
                    0,
                    1,
                    null);
                var unitDefinitions = Array.CreateInstance(UnitProductionDefinitionType, 1);
                unitDefinitions.SetValue(unitDefinition, 0);
                var queue = productionBuilding.AddComponent(UnitProductionQueueType);
                Invoke(queue, "Configure", null, null, unitDefinitions, 2, Vector3.right, Vector3.right * 2f);

                var signalRelayKind = Enum.Parse(BuildingKindType, "SignalRelay");
                var buildingDefinition = Activator.CreateInstance(BuildingConstructionDefinitionType);
                Invoke(
                    buildingDefinition,
                    "Configure",
                    "Signal Relay",
                    signalRelayKind,
                    CreateResourceAmount(0, 0),
                    5f,
                    new Vector2Int(2, 2),
                    signalRelayPrefab,
                    null);
                var buildingDefinitions = Array.CreateInstance(BuildingConstructionDefinitionType, 1);
                buildingDefinitions.SetValue(buildingDefinition, 0);
                var placement = placementObject.AddComponent(BuildingPlacementServiceType);
                SetField(placement, "team", UnitTeam.Team8);
                Invoke(placement, "ConfigureConstructionDefinitions", buildingDefinitions);

                Assert.That(CanEnqueue(queue, unitDefinition, out var researchReason), Is.False);
                StringAssert.Contains("completed research 'attack-damage'", researchReason);
                Assert.That(CanSelectBuild(placement, buildingDefinition, out var buildResearchReason), Is.False);
                StringAssert.Contains("completed research 'attack-damage'", buildResearchReason);

                Invoke(state, "MarkResearchCompleted", "attack-damage");
                Assert.That(CanEnqueue(queue, unitDefinition, out var prerequisiteReason), Is.False);
                StringAssert.Contains("prerequisite unlock 'combat-license'", prerequisiteReason);

                Invoke(state, "Unlock", "combat-license");
                Assert.That(CanEnqueue(queue, unitDefinition, out var productionReason), Is.True, productionReason);
                Assert.That(CanSelectBuild(placement, buildingDefinition, out var buildingReason), Is.True, buildingReason);
            }
            finally
            {
                Object.DestroyImmediate(placementObject);
                Object.DestroyImmediate(signalRelayPrefab);
                Object.DestroyImmediate(soldierPrefab);
                Object.DestroyImmediate(productionBuilding);
                Object.DestroyImmediate(stateObject);
                Object.DestroyImmediate(catalog);
            }
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.That(type, Is.Not.Null, $"Could not resolve gameplay type {typeName}.");
            return type;
        }

        private static object InvokeStatic(Type targetType, string methodName, params object[] arguments)
        {
            var method = targetType.GetMethod(methodName);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {targetType.Name}.{methodName}.");
            return method.Invoke(null, arguments);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static bool CanProduce(UnitTeam team, PrototypeUnitType unitType, out string failureReason)
        {
            var method = TeamUnlockStateType.GetMethod("CanProduce", BindingFlags.Static | BindingFlags.Public);
            var arguments = new object[] { team, unitType, "produce", unitType.ToString(), null };
            var result = (bool)method.Invoke(null, arguments);
            failureReason = arguments[4] as string;
            return result;
        }

        private static bool CanEnqueue(object queue, object definition, out string failureReason)
        {
            var method = queue.GetType().GetMethod("CanEnqueue");
            var arguments = new[] { definition, null };
            var result = (bool)method.Invoke(queue, arguments);
            failureReason = arguments[1] as string;
            return result;
        }

        private static bool CanSelectBuild(object placement, object definition, out string failureReason)
        {
            var method = placement.GetType().GetMethod("CanSelectConstructionDefinition");
            var arguments = new[] { definition, null };
            var result = (bool)method.Invoke(placement, arguments);
            failureReason = arguments[1] as string;
            return result;
        }

        private static object CreateResourceAmount(int minerals, int gas)
        {
            return Activator.CreateInstance(ResourceAmountType, minerals, gas);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not resolve field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not resolve property {target.GetType().Name}.{propertyName}.");
            return property.GetValue(target);
        }
    }
}
