using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitTacticalBehaviorPlayModeTests
    {
        [TestCase(PrototypeUnitType.Soldier, UnitEngagementStyle.Standard)]
        [TestCase(PrototypeUnitType.Spliter, UnitEngagementStyle.AreaPressure)]
        [TestCase(PrototypeUnitType.Ranger, UnitEngagementStyle.KeepDistance)]
        [TestCase(PrototypeUnitType.Tank, UnitEngagementStyle.Artillery)]
        [TestCase(PrototypeUnitType.Striker, UnitEngagementStyle.CloseAssault)]
        [TestCase(PrototypeUnitType.Swarm, UnitEngagementStyle.CloseAssault)]
        [TestCase(PrototypeUnitType.Siege, UnitEngagementStyle.Artillery)]
        [TestCase(PrototypeUnitType.Scout, UnitEngagementStyle.Standard)]
        public void TacticalProfile_MapsEachCombatUnitToItsDocumentedStyle(
            PrototypeUnitType unitType,
            UnitEngagementStyle expectedStyle)
        {
            Assert.That(UnitTacticalBehaviorProfiles.Get(unitType).EngagementStyle, Is.EqualTo(expectedStyle));
        }

        [UnityTest]
        public IEnumerator Ranger_RetreatsFromCloseTargetThenReengagesAtSafeDistance()
        {
            var ranger = CreateUnit("Retreating Ranger", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Ranger, 6f);
            var target = CreateUnit("Ranger Pressure Target", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, 1f, false);
            var agent = ranger.GetComponent<UnitCommandAgent>();
            var targetStatus = target.GetComponent<PrototypeUnitStatus>();

            agent.Issue(new UnitCommand(UnitCommandMode.FocusAttack, target.transform.position, targetStatus, false));
            Assert.That(agent.ActionState, Is.EqualTo(UnitActionState.RetreatingFromTarget));

            ranger.transform.position = new Vector3(-4.5f, 0f);
            InvokePrivate(agent, "UpdateTargetEngagement");

            Assert.That(agent.PriorityTarget, Is.EqualTo(targetStatus));
            Assert.That(agent.ActionState, Is.EqualTo(UnitActionState.AttackingTarget));

            Object.Destroy(ranger);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ranger_HoldPositionDisablesRetreatMovement()
        {
            var ranger = CreateUnit("Holding Ranger", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Ranger, 6f);
            var target = CreateUnit("Holding Ranger Target", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, 1f, false);
            var agent = ranger.GetComponent<UnitCommandAgent>();
            var targetStatus = target.GetComponent<PrototypeUnitStatus>();

            agent.HoldPosition();
            Assert.That(agent.TryRetaliate(targetStatus), Is.True);
            Assert.That(agent.ActionState, Is.EqualTo(UnitActionState.AttackingTarget));
            Assert.That(agent.ActionState, Is.Not.EqualTo(UnitActionState.RetreatingFromTarget));

            Object.Destroy(ranger);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseAssault_ChasesDeeperWhileSoldierAttacksAtRangeEdge()
        {
            var striker = CreateUnit("Entry Striker", Vector3.zero, UnitTeam.Team1, PrototypeUnitType.Striker, 1f);
            var soldier = CreateUnit("Entry Soldier", new Vector3(0f, 3f), UnitTeam.Team1, PrototypeUnitType.Soldier, 1f);
            var strikerTarget = CreateUnit("Striker Entry Target", new Vector3(1f, 0f), UnitTeam.Team2, PrototypeUnitType.Worker, 1f, false);
            var soldierTarget = CreateUnit("Soldier Entry Target", new Vector3(1f, 3f), UnitTeam.Team2, PrototypeUnitType.Worker, 1f, false);
            var strikerAgent = striker.GetComponent<UnitCommandAgent>();
            var soldierAgent = soldier.GetComponent<UnitCommandAgent>();

            strikerAgent.Issue(new UnitCommand(
                UnitCommandMode.FocusAttack,
                strikerTarget.transform.position,
                strikerTarget.GetComponent<PrototypeUnitStatus>(),
                false));
            soldierAgent.Issue(new UnitCommand(
                UnitCommandMode.FocusAttack,
                soldierTarget.transform.position,
                soldierTarget.GetComponent<PrototypeUnitStatus>(),
                false));

            Assert.That(strikerAgent.ActionState, Is.EqualTo(UnitActionState.ChasingTarget));
            Assert.That(soldierAgent.ActionState, Is.EqualTo(UnitActionState.AttackingTarget));

            Object.Destroy(striker);
            Object.Destroy(soldier);
            Object.Destroy(strikerTarget);
            Object.Destroy(soldierTarget);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtendedUnitDefaults_ConfigureDocumentedRolesStatsAndCombatComponents()
        {
            var medic = CreateDefaultUnit("Default Medic", PrototypeUnitType.Medic);
            var siege = CreateDefaultUnit("Default Siege", PrototypeUnitType.Siege);
            var scout = CreateDefaultUnit("Default Scout", PrototypeUnitType.Scout);
            yield return null;

            var medicStatus = medic.GetComponent<PrototypeUnitStatus>();
            Assert.That(medicStatus.Team, Is.EqualTo(UnitTeam.Team3));
            Assert.That(medicStatus.Roles, Is.EqualTo(UnitRole.Support));
            Assert.That(medicStatus.MaxHealth, Is.EqualTo(90f));
            Assert.That(medicStatus.PhysicalAttackPower, Is.Zero);
            Assert.That(medicStatus.AttackRange, Is.Zero);
            Assert.That(medicStatus.DetectionRange, Is.Zero);
            Assert.That(medicStatus.AttackSpeed, Is.Zero);
            Assert.That(medicStatus.MovementSpeed, Is.EqualTo(3.6f));
            Assert.That(medicStatus.VisionRadius, Is.EqualTo(10f));
            Assert.That(medicStatus.SupplyCost, Is.EqualTo(2));
            Assert.That(medic.GetComponent<UnitCombat>(), Is.Null);
            Assert.That(medic.GetComponent<TemporaryAttackEffect>(), Is.Null);

            var siegeStatus = siege.GetComponent<PrototypeUnitStatus>();
            Assert.That(siegeStatus.Roles, Is.EqualTo(UnitRole.Combat | UnitRole.Siege));
            Assert.That(siegeStatus.MaxHealth, Is.EqualTo(180f));
            Assert.That(siegeStatus.PhysicalAttackPower, Is.EqualTo(36f));
            Assert.That(siegeStatus.AttackRange, Is.EqualTo(8.5f));
            Assert.That(siegeStatus.DetectionRange, Is.EqualTo(9.5f));
            Assert.That(siegeStatus.AttackSpeed, Is.EqualTo(0.45f));
            Assert.That(siegeStatus.MovementSpeed, Is.EqualTo(1.7f));
            Assert.That(siegeStatus.VisionRadius, Is.EqualTo(8f));
            Assert.That(siegeStatus.SupplyCost, Is.EqualTo(4));
            Assert.That(siege.GetComponent<UnitCombat>(), Is.Not.Null);

            var scoutStatus = scout.GetComponent<PrototypeUnitStatus>();
            Assert.That(scoutStatus.Roles, Is.EqualTo(UnitRole.Combat));
            Assert.That(scoutStatus.MaxHealth, Is.EqualTo(55f));
            Assert.That(scoutStatus.PhysicalAttackPower, Is.EqualTo(5f));
            Assert.That(scoutStatus.AttackRange, Is.EqualTo(4.5f));
            Assert.That(scoutStatus.DetectionRange, Is.EqualTo(7f));
            Assert.That(scoutStatus.AttackSpeed, Is.EqualTo(1.2f));
            Assert.That(scoutStatus.MovementSpeed, Is.EqualTo(4.8f));
            Assert.That(scoutStatus.VisionRadius, Is.EqualTo(11f));
            Assert.That(scoutStatus.SupplyCost, Is.EqualTo(1));
            Assert.That(scout.GetComponent<UnitCombat>(), Is.Not.Null);

            foreach (var unit in new[] { medic, siege, scout })
            {
                Assert.That(unit.GetComponent<UnitPathAgent>(), Is.Not.Null);
                Assert.That(unit.GetComponent<UnitCommandAgent>(), Is.Not.Null);
                Assert.That(unit.GetComponent<UnitHealth>(), Is.Not.Null);
                Assert.That(unit.GetComponent<ProjectS.Visibility.FogVisibilityTarget>(), Is.Not.Null);
                Object.Destroy(unit);
            }

            yield return null;
        }

        private static GameObject CreateUnit(
            string name,
            Vector3 position,
            UnitTeam team,
            PrototypeUnitType unitType,
            float attackRange,
            bool canAttack = true)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            var collider = unit.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.2f;
            collider.isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                unitType,
                MovementDomain.Ground,
                canAttack ? UnitRole.Combat : UnitRole.Resource,
                attackRange > 2f ? AttackDistanceType.Ranged : AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                100f,
                canAttack ? 1f : 0f,
                0f,
                attackRange,
                Mathf.Max(attackRange, 8f),
                1f,
                3f,
                1,
                Vector2Int.one,
                !canAttack,
                false,
                0f);
            return unit;
        }

        private static GameObject CreateDefaultUnit(string name, PrototypeUnitType unitType)
        {
            var unit = new GameObject(name);
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            unit.AddComponent<PrototypeUnitStatus>().ConfigurePrototypeDefaults(unitType, UnitTeam.Team3);
            return unit;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }
    }
}
