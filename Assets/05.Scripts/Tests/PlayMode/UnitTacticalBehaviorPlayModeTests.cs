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

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }
    }
}
