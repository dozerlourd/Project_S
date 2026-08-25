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

        private static GameObject CreateMovableUnit(string name, Vector3 position)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;

            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                UnitTeam.Team1,
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
