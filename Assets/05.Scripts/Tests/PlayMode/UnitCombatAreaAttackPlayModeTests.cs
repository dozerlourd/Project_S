using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitCombatAreaAttackPlayModeTests
    {
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type BuildingHealthType = GetGameplayType("ProjectS.Buildings.BuildingHealth");

        [UnityTest]
        public IEnumerator AreaAttack_IncludesExplicitTargetThenNearestEligibleTargets()
        {
            var attacker = CreateUnit("AreaAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Spliter, 2f, 3);
            var primary = CreateUnit("AreaPrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var nearest = CreateUnit("AreaNearest", new Vector3(1.2f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var building = CreateBuilding("AreaBuilding", new Vector3(1.5f, 0f));
            var tooFar = CreateUnit("AreaTooFar", new Vector3(3.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var ally = CreateUnit("AreaAlly", new Vector3(1.1f, 0f), UnitTeam.Team1, PrototypeUnitType.Worker);
            var alliedBuilding = CreateBuilding("AreaAlliedBuilding", new Vector3(1.3f, 0f), UnitTeam.Team1);
            var dead = CreateUnit("AreaDead", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var inactive = CreateUnit("AreaInactive", new Vector3(1.15f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            dead.GetComponent<PrototypeUnitStatus>().TakeDamage(1000f);
            inactive.SetActive(false);

            ApplyAttack(attacker, primary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(primary), Is.EqualTo(90f));
            Assert.That(Health(nearest), Is.EqualTo(90f));
            Assert.That(GetFloat(building.GetComponent(BuildingHealthType), "CurrentHealth"), Is.EqualTo(640f));
            Assert.That(Health(tooFar), Is.EqualTo(100f));
            Assert.That(Health(ally), Is.EqualTo(100f));
            Assert.That(GetFloat(alliedBuilding.GetComponent(BuildingHealthType), "CurrentHealth"), Is.EqualTo(650f));
            Assert.That(Health(dead), Is.EqualTo(0f));
            Assert.That(Health(inactive), Is.EqualTo(100f));

            Destroy(attacker, primary, nearest, building, tooFar, ally, alliedBuilding, dead, inactive);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AreaAttack_RespectsMaximumTargetsBeforeFartherCandidates()
        {
            var attacker = CreateUnit("LimitedAreaAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Spliter, 2f, 2);
            var primary = CreateUnit("LimitedAreaPrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var nearest = CreateUnit("LimitedAreaNearest", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var farther = CreateUnit("LimitedAreaFarther", new Vector3(1.4f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);

            ApplyAttack(attacker, primary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(primary), Is.EqualTo(90f));
            Assert.That(Health(nearest), Is.EqualTo(90f));
            Assert.That(Health(farther), Is.EqualTo(100f));

            Destroy(attacker, primary, nearest, farther);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AreaAttack_RequiresBothPositiveRadiusAndTargetLimit()
        {
            var zeroRadiusAttacker = CreateUnit("ZeroRadiusAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Soldier, 0f, 3);
            var zeroRadiusPrimary = CreateUnit("ZeroRadiusPrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var zeroRadiusNearby = CreateUnit("ZeroRadiusNearby", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var zeroLimitAttacker = CreateUnit("ZeroLimitAttacker", new Vector3(0f, 4f), UnitTeam.Team1, PrototypeUnitType.Soldier, 2f, 0);
            var zeroLimitPrimary = CreateUnit("ZeroLimitPrimary", new Vector3(1f, 4f), UnitTeam.Team2, PrototypeUnitType.Worker);
            var zeroLimitNearby = CreateUnit("ZeroLimitNearby", new Vector3(1.1f, 4f), UnitTeam.Team2, PrototypeUnitType.Worker);

            ApplyAttack(zeroRadiusAttacker, zeroRadiusPrimary.GetComponent<PrototypeUnitStatus>());
            ApplyAttack(zeroLimitAttacker, zeroLimitPrimary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(zeroRadiusPrimary), Is.EqualTo(90f));
            Assert.That(Health(zeroRadiusNearby), Is.EqualTo(100f));
            Assert.That(Health(zeroLimitPrimary), Is.EqualTo(90f));
            Assert.That(Health(zeroLimitNearby), Is.EqualTo(100f));

            Destroy(zeroRadiusAttacker, zeroRadiusPrimary, zeroRadiusNearby,
                zeroLimitAttacker, zeroLimitPrimary, zeroLimitNearby);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrototypeDefaults_UseCatalogAreaDamageValues()
        {
            foreach (PrototypeUnitType unitType in Enum.GetValues(typeof(PrototypeUnitType)))
            {
                var unit = new GameObject($"Default {unitType}");
                unit.AddComponent<BoxCollider2D>().isTrigger = true;
                var status = unit.AddComponent<PrototypeUnitStatus>();
                status.ConfigurePrototypeDefaults(unitType);

                if (unitType == PrototypeUnitType.Spliter)
                {
                    Assert.That(status.AreaDamageRadius, Is.EqualTo(2f));
                    Assert.That(status.MaxAreaTargets, Is.EqualTo(3));
                    Assert.That(status.HasAreaAttack, Is.True);
                }
                else
                {
                    Assert.That(status.AreaDamageRadius, Is.Zero, unitType.ToString());
                    Assert.That(status.MaxAreaTargets, Is.Zero, unitType.ToString());
                    Assert.That(status.HasAreaAttack, Is.False, unitType.ToString());
                }

                Object.Destroy(unit);
            }

            yield return null;
        }

        [TestCase(PrototypeUnitType.Soldier, PrototypeUnitType.Spliter, UnitCombatRules.AdvantageDamageMultiplier)]
        [TestCase(PrototypeUnitType.Spliter, PrototypeUnitType.Ranger, UnitCombatRules.AdvantageDamageMultiplier)]
        [TestCase(PrototypeUnitType.Ranger, PrototypeUnitType.Soldier, UnitCombatRules.AdvantageDamageMultiplier)]
        [TestCase(PrototypeUnitType.Tank, PrototypeUnitType.Swarm, UnitCombatRules.AdvantageDamageMultiplier)]
        [TestCase(PrototypeUnitType.Striker, PrototypeUnitType.Tank, UnitCombatRules.AdvantageDamageMultiplier)]
        [TestCase(PrototypeUnitType.Swarm, PrototypeUnitType.Striker, UnitCombatRules.AdvantageDamageMultiplier)]
        public void DamageMultiplier_ReturnsAdvantageForEachUnitCounter(PrototypeUnitType attacker, PrototypeUnitType defender, float expected)
        {
            Assert.That(UnitCombatRules.GetDamageMultiplier(attacker, defender), Is.EqualTo(expected));
            Assert.That(UnitCombatRules.GetDamageMultiplier(defender, attacker), Is.EqualTo(UnitCombatRules.DisadvantageDamageMultiplier));
        }

        private static GameObject CreateUnit(
            string name,
            Vector3 position,
            UnitTeam team,
            PrototypeUnitType unitType,
            float areaRadius = 0f,
            int maxAreaTargets = 0)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Combat,
                AttackDistanceType.Melee, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common,
                AttackTargetType.SingleTarget, 100f, 10f, 0f,
                2f, 4f, 1f, 3f, 1, Vector2Int.one, false, false, 0f);
            status.ConfigureAreaDamage(areaRadius, maxAreaTargets);
            return unit;
        }

        private static GameObject CreateBuilding(string name, Vector3 position, UnitTeam team = UnitTeam.Team2)
        {
            var building = new GameObject(name);
            building.transform.position = position;
            building.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = building.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", team, Enum.Parse(BuildingKindType, "MainBase"), Vector2Int.one, true);
            return building;
        }

        private static void ApplyAttack(GameObject attacker, IUnitAttackTarget target)
        {
            var combat = attacker.GetComponent<UnitCombat>();
            Assert.That(combat, Is.Not.Null);
            Invoke(combat, "ApplyAttack", target);
        }

        private static float Health(GameObject unit)
        {
            return unit.GetComponent<UnitHealth>().CurrentHealth;
        }

        private static float GetFloat(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (float)property.GetValue(target);
        }

        private static void Destroy(params Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                Object.Destroy(objects[i]);
            }
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not resolve gameplay type {typeName}.");
            return null;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }
    }
}
