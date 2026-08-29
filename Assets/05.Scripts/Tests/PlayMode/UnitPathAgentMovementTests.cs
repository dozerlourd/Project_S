using System.Collections;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitPathAgentMovementTests
    {
        private const float MovementSpeed = 3f;
        private const float MovementTolerance = 0.0025f;

        [UnityTest]
        public IEnumerator MoveTo_DoesNotExceedConfiguredSpeedNearDestination()
        {
            var unit = CreateMovableUnit("SmoothSingleUnit", Vector3.zero);
            var agent = unit.GetComponent<UnitPathAgent>();
            var previousPosition = unit.transform.position;
            var previousTime = Time.time;
            var maxSpeed = 0f;

            Assert.That(agent.MoveTo(new Vector3(0.07f, 0f, 0f)), Is.True);

            for (var i = 0; i < 6; i++)
            {
                yield return null;

                var deltaTime = Time.time - previousTime;
                if (deltaTime > 0f)
                {
                    var distance = Vector3.Distance(previousPosition, unit.transform.position);
                    maxSpeed = Mathf.Max(maxSpeed, distance / deltaTime);
                }

                previousPosition = unit.transform.position;
                previousTime = Time.time;
            }

            Object.Destroy(unit);
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(MovementSpeed + MovementTolerance));
        }

        [UnityTest]
        public IEnumerator MoveTo_WithNearbyMovingUnit_DoesNotAddSeparationSpeedOnTopOfMovementSpeed()
        {
            var leader = CreateMovableUnit("SmoothLeader", Vector3.zero);
            var neighbor = CreateMovableUnit("SmoothNeighbor", new Vector3(0.35f, 0.1f, 0f));
            var leaderAgent = leader.GetComponent<UnitPathAgent>();
            var neighborAgent = neighbor.GetComponent<UnitPathAgent>();
            var previousPosition = leader.transform.position;
            var previousTime = Time.time;
            var maxSpeed = 0f;

            Assert.That(leaderAgent.MoveTo(new Vector3(3f, 0f, 0f)), Is.True);
            Assert.That(neighborAgent.MoveTo(new Vector3(3.35f, 0.1f, 0f)), Is.True);

            for (var i = 0; i < 30; i++)
            {
                yield return null;

                var deltaTime = Time.time - previousTime;
                if (deltaTime > 0f)
                {
                    var distance = Vector3.Distance(previousPosition, leader.transform.position);
                    maxSpeed = Mathf.Max(maxSpeed, distance / deltaTime);
                }

                previousPosition = leader.transform.position;
                previousTime = Time.time;
            }

            Object.Destroy(leader);
            Object.Destroy(neighbor);
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(MovementSpeed + MovementTolerance));
        }

        [UnityTest]
        public IEnumerator AttackMove_AcquiresNearbyEnemyAndEntersAttackState()
        {
            var attacker = CreateMovableUnit("AttackMoveAttacker", Vector3.zero, UnitTeam.Team1);
            var enemy = CreateMovableUnit("AttackMoveEnemy", new Vector3(0.75f, 0f, 0f), UnitTeam.Team2);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var enemyStatus = enemy.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(4f, 0f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(enemyStatus));
            Assert.That(commandAgent.ActionState, Is.EqualTo(UnitActionState.AttackingTarget));

            Object.Destroy(attacker);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator UnitRegistry_TracksAgentsByInitializedTeam()
        {
            var friendly = CreateMovableUnit("RegistryFriendly", Vector3.zero, UnitTeam.Team1);
            var enemy = CreateMovableUnit("RegistryEnemy", Vector3.right, UnitTeam.Team2);
            var friendlyAgent = friendly.GetComponent<UnitCommandAgent>();
            var enemyAgent = enemy.GetComponent<UnitCommandAgent>();

            yield return null;

            Assert.That(UnitRegistry.GetAgents(UnitTeam.Team1), Does.Contain(friendlyAgent));
            Assert.That(UnitRegistry.GetAgents(UnitTeam.Team2), Does.Contain(enemyAgent));

            Object.Destroy(friendly);
            Object.Destroy(enemy);
        }

        private static GameObject CreateMovableUnit(string name, Vector3 position)
        {
            return CreateMovableUnit(name, position, UnitTeam.Team1);
        }

        private static GameObject CreateMovableUnit(string name, Vector3 position, UnitTeam team)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;

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
                MovementSpeed,
                1,
                Vector2Int.one,
                false,
                false,
                0f);

            if (unit.GetComponent<UnitPathAgent>() == null)
            {
                unit.AddComponent<UnitPathAgent>();
            }

            return unit;
        }
    }
}
