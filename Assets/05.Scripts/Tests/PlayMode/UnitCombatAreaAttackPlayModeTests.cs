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
            var attacker = CreateUnit("AreaAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Spliter, true, 3, 2f);
            var primary = CreateUnit("AreaPrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var nearest = CreateUnit("AreaNearest", new Vector3(1.2f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var building = CreateBuilding("AreaBuilding", new Vector3(1.5f, 0f));
            var tooFar = CreateUnit("AreaTooFar", new Vector3(3.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var ally = CreateUnit("AreaAlly", new Vector3(1.1f, 0f), UnitTeam.Team1, PrototypeUnitType.Worker, false, 1, 0f);
            var dead = CreateUnit("AreaDead", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var inactive = CreateUnit("AreaInactive", new Vector3(1.15f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            dead.GetComponent<PrototypeUnitStatus>().TakeDamage(1000f);
            inactive.SetActive(false);

            yield return null;
            ApplyAttack(attacker, primary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(primary), Is.EqualTo(90f));
            Assert.That(Health(nearest), Is.EqualTo(90f));
            Assert.That(GetFloat(building.GetComponent(BuildingHealthType), "CurrentHealth"), Is.EqualTo(640f));
            Assert.That(Health(tooFar), Is.EqualTo(100f));
            Assert.That(Health(ally), Is.EqualTo(100f));
            Assert.That(Health(dead), Is.EqualTo(0f));
            Assert.That(Health(inactive), Is.EqualTo(100f));

            Destroy(attacker, primary, nearest, building, tooFar, ally, dead, inactive);
        }

        [UnityTest]
        public IEnumerator AreaAttack_RespectsMaximumTargetsBeforeFartherCandidates()
        {
            var attacker = CreateUnit("LimitedAreaAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Spliter, true, 2, 2f);
            var primary = CreateUnit("LimitedAreaPrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var nearest = CreateUnit("LimitedAreaNearest", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var farther = CreateUnit("LimitedAreaFarther", new Vector3(1.4f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);

            yield return null;
            ApplyAttack(attacker, primary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(primary), Is.EqualTo(90f));
            Assert.That(Health(nearest), Is.EqualTo(90f));
            Assert.That(Health(farther), Is.EqualTo(100f));

            Destroy(attacker, primary, nearest, farther);
        }

        [UnityTest]
        public IEnumerator SingleTargetAttack_DamagesOnlyItsExplicitTarget()
        {
            var attacker = CreateUnit("SingleAttacker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Soldier, false, 3, 2f);
            var primary = CreateUnit("SinglePrimary", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);
            var nearby = CreateUnit("SingleNearby", new Vector3(1.1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, false, 1, 0f);

            yield return null;
            ApplyAttack(attacker, primary.GetComponent<PrototypeUnitStatus>());

            Assert.That(Health(primary), Is.EqualTo(90f));
            Assert.That(Health(nearby), Is.EqualTo(100f));

            Destroy(attacker, primary, nearby);
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

        private static GameObject CreateUnit(string name, Vector3 position, UnitTeam team, PrototypeUnitType unitType, bool areaAttack, int maxTargets, float area)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Combat,
                AttackDistanceType.Melee, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common,
                areaAttack ? AttackTargetType.AreaAttack : AttackTargetType.SingleTarget, 100f, 10f, 0f,
                2f, 4f, 1f, 3f, maxTargets, Vector2Int.one, false, areaAttack, area);
            return unit;
        }

        private static GameObject CreateBuilding(string name, Vector3 position)
        {
            var building = new GameObject(name);
            building.transform.position = position;
            building.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = building.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", UnitTeam.Team2, Enum.Parse(BuildingKindType, "MainBase"), Vector2Int.one, true);
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
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }
    }
}
