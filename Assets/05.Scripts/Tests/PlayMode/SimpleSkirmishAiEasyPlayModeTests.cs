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

            Invoke(wallet, "Add", addResource, 25);
            RunResearchDecision(ai);
            Assert.That(GetProperty(research, "ActiveDefinition"), Is.EqualTo(mobility));

            Object.Destroy(aiObject);
            Object.Destroy(researchObject);
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
