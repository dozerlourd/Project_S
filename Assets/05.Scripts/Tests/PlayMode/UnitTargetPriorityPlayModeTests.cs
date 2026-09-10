using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitTargetPriorityPlayModeTests
    {
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type SimpleSkirmishAIType = GetGameplayType("ProjectS.AI.SimpleSkirmishAI");

        [UnityTest]
        public IEnumerator AttackMove_PrefersCombatUnitOverCloserWorker()
        {
            var attacker = CreateUnit("PriorityAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var worker = CreateUnit("PriorityWorker", new Vector3(0.7f, 0f, 0f), UnitTeam.Team2, UnitRole.Resource, 0f);
            var combatUnit = CreateUnit("PriorityCombat", new Vector3(1.3f, 0f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var combatStatus = combatUnit.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(5f, 0f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(combatStatus));

            Destroy(attacker, worker, combatUnit);
        }

        [UnityTest]
        public IEnumerator AttackMove_PrefersWorkerOverCloserDefensiveBuilding()
        {
            var attacker = CreateUnit("WorkerPriorityAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var worker = CreateUnit("WorkerPriorityTarget", new Vector3(1.3f, 0f), UnitTeam.Team2, UnitRole.Resource, 0f);
            var defensiveBuilding = CreateBuilding("CloserDefensiveBuilding", "AutoTurret", new Vector3(0.7f, 0f));
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var workerStatus = worker.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(5f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(workerStatus));

            Destroy(attacker, worker, defensiveBuilding);
        }

        [UnityTest]
        public IEnumerator AttackMove_PrefersHigherThreatCombatUnitAtSimilarDistance()
        {
            var attacker = CreateUnit("ThreatPriorityAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 1f);
            var weaker = CreateUnit("ThreatPriorityWeak", new Vector3(0.8f, 0f), UnitTeam.Team2, UnitRole.Combat, 2f);
            var stronger = CreateUnit("ThreatPriorityStrong", new Vector3(1.3f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var strongerStatus = stronger.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(5f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(strongerStatus));

            Destroy(attacker, weaker, stronger);
        }

        [UnityTest]
        public IEnumerator AttackMove_PrefersRecentAttackerOverCombatUnit()
        {
            var defender = CreateUnit("RecentAttackerDefender", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var workerAttacker = CreateUnit("RecentAttackerWorker", new Vector3(1.3f, 0f, 0f), UnitTeam.Team2, UnitRole.Resource, 0f);
            var combatUnit = CreateUnit("RecentAttackerCombat", new Vector3(0.7f, 0f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var commandAgent = defender.GetComponent<UnitCommandAgent>();
            var defenderStatus = defender.GetComponent<PrototypeUnitStatus>();
            var workerStatus = workerAttacker.GetComponent<PrototypeUnitStatus>();

            UnitTargetPriority.RecordRecentAttacker(defenderStatus, workerStatus);
            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(5f, 0f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(workerStatus));

            Destroy(defender, workerAttacker, combatUnit);
        }

        [UnityTest]
        public IEnumerator UnitCombat_DamageRecordsRecentAttackerOnTarget()
        {
            var attacker = CreateUnit("DamageRecorderAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var defender = CreateUnit("DamageRecorderDefender", new Vector3(0.7f, 0f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var attackerStatus = attacker.GetComponent<PrototypeUnitStatus>();
            var defenderStatus = defender.GetComponent<PrototypeUnitStatus>();
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.FocusAttack, defender.transform.position, defenderStatus, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(UnitTargetPriority.GetRecentAttacker(defenderStatus), Is.EqualTo(attackerStatus));

            Destroy(attacker, defender);
        }

        [UnityTest]
        public IEnumerator BuildingDamageOverload_RecordsRecentAttacker()
        {
            var attacker = CreateUnit("BuildingDamageAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var building = CreateBuilding("BuildingDamageTarget", "MainBase", new Vector3(0.7f, 0f, 0f));
            var attackerStatus = attacker.GetComponent<PrototypeUnitStatus>();
            var buildingTarget = (IUnitAttackTarget)building.GetComponent(BuildingStatusType);

            buildingTarget.TakeDamage(1f, attackerStatus);
            yield return null;

            Assert.That(UnitTargetPriority.GetRecentAttacker(buildingTarget), Is.EqualTo(attackerStatus));

            Destroy(attacker, building);
        }

        [UnityTest]
        public IEnumerator RecentAttacker_ExpiresAfterConfiguredMemoryDuration()
        {
            var defender = CreateUnit("ExpiredAttackerDefender", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var attacker = CreateUnit("ExpiredAttackerSource", new Vector3(0.7f, 0f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var defenderStatus = defender.GetComponent<PrototypeUnitStatus>();
            var attackerStatus = attacker.GetComponent<PrototypeUnitStatus>();

            UnitTargetPriority.RecordRecentAttacker(defenderStatus, attackerStatus);
            yield return new WaitForSeconds(UnitTargetPriority.RecentAttackerMemoryDuration + 0.1f);

            Assert.That(UnitTargetPriority.GetRecentAttacker(defenderStatus), Is.Null);

            Destroy(defender, attacker);
        }

        [UnityTest]
        public IEnumerator FocusAttack_KeepsExplicitWorkerTargetWhenCombatUnitIsNearby()
        {
            var attacker = CreateUnit("FocusPriorityAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 20f);
            var worker = CreateUnit("FocusPriorityWorker", new Vector3(0.7f, 0f, 0f), UnitTeam.Team2, UnitRole.Resource, 0f);
            var combatUnit = CreateUnit("FocusPriorityCombat", new Vector3(1.3f, 0f, 0f), UnitTeam.Team2, UnitRole.Combat, 20f);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var workerStatus = worker.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.FocusAttack, worker.transform.position, workerStatus, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(workerStatus));

            Destroy(attacker, worker, combatUnit);
        }

        [UnityTest]
        public IEnumerator SpatialQuery_VisitsOnlyNearbyEnemyBuckets()
        {
            var seeker = CreateUnit("SpatialSeeker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 0f);
            var nearby = CreateUnit("SpatialNearby", new Vector3(1f, 0f), UnitTeam.Team2, UnitRole.Combat, 10f);
            var distant = CreateUnit("SpatialDistant", new Vector3(40f, 0f), UnitTeam.Team2, UnitRole.Combat, 10f);
            var ally = CreateUnit("SpatialAlly", new Vector3(1f, 1f), UnitTeam.Team1, UnitRole.Combat, 10f);
            yield return null;

            UnitAttackTargetRegistry.ResetQueryStatistics();
            var results = new List<IUnitAttackTarget>();
            UnitAttackTargetRegistry.QueryNearbyEnemies(UnitTeam.Team1, Vector3.zero, 2f, results);
            var statistics = UnitAttackTargetRegistry.GetQueryStatistics();

            Assert.That(results.Contains(nearby.GetComponent<PrototypeUnitStatus>()), Is.True);
            Assert.That(results.Contains(distant.GetComponent<PrototypeUnitStatus>()), Is.False);
            Assert.That(results.Contains(ally.GetComponent<PrototypeUnitStatus>()), Is.False);
            Assert.That(statistics.QueryCount, Is.EqualTo(1));
            Assert.That(statistics.VisitedCandidateCount, Is.EqualTo(1));

            Destroy(seeker, nearby, distant, ally);
        }

        [UnityTest]
        public IEnumerator SpatialQuery_RefreshesTargetAfterItChangesCells()
        {
            var target = CreateUnit("MovingSpatialTarget", new Vector3(20f, 0f), UnitTeam.Team2, UnitRole.Combat, 10f);
            var targetStatus = target.GetComponent<PrototypeUnitStatus>();
            yield return null;

            target.transform.position = new Vector3(1f, 0f);
            UnitAttackTargetRegistry.RefreshPosition(targetStatus);
            var results = new List<IUnitAttackTarget>();
            UnitAttackTargetRegistry.QueryNearbyEnemies(UnitTeam.Team1, Vector3.zero, 2f, results);

            Assert.That(results.Contains(targetStatus), Is.True);
            Assert.That(UnitAttackTargetRegistry.GetQueryStatistics().PositionUpdateCount, Is.GreaterThan(0));

            Destroy(target);
        }

        [UnityTest]
        public IEnumerator AttackMove_KeepsCurrentTargetWhenSamePriorityCandidateIsOnlySlightlyCloser()
        {
            var attacker = CreateUnit("HysteresisAttacker", Vector3.zero, UnitTeam.Team1, UnitRole.Combat, 1f);
            var current = CreateUnit("HysteresisCurrent", new Vector3(1.2f, 0f), UnitTeam.Team2, UnitRole.Combat, 1f);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var currentStatus = current.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(5f, 0f), null, false));
            yield return new WaitForSeconds(0.3f);
            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(currentStatus));

            var slightlyCloser = CreateUnit(
                "HysteresisSlightlyCloser",
                new Vector3(1f, 0f),
                UnitTeam.Team2,
                UnitRole.Combat,
                1f);
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(currentStatus));

            Destroy(attacker, current, slightlyCloser);
        }

        [UnityTest]
        public IEnumerator AiAttackTarget_PrefersDefensiveThenProductionThenMainBase()
        {
            var aiObject = new GameObject("PriorityAi");
            var ai = aiObject.AddComponent(SimpleSkirmishAIType);
            Invoke(ai, "Configure", UnitTeam.Team1, UnitTeam.Team2, 0, 1, Vector3.zero);
            var mainBase = CreateBuilding("PriorityMainBase", "MainBase", new Vector3(1f, 0f, 0f));
            var production = CreateBuilding("PriorityProduction", "Production", new Vector3(2f, 0f, 0f));
            var turret = CreateBuilding("PriorityTurret", "AutoTurret", new Vector3(3f, 0f, 0f));

            yield return null;

            var target = (Vector3)Invoke(ai, "FindAttackTarget");
            Assert.That(target, Is.EqualTo(turret.transform.position));

            Object.Destroy(turret);
            yield return null;
            target = (Vector3)Invoke(ai, "FindAttackTarget");
            Assert.That(target, Is.EqualTo(production.transform.position));

            Destroy(aiObject, mainBase, production);
        }

        private static GameObject CreateUnit(string name, Vector3 position, UnitTeam team, UnitRole roles, float attackPower)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            var collider = unit.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.5f;
            collider.isTrigger = true;

            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                roles.HasFlag(UnitRole.Combat) ? PrototypeUnitType.Soldier : PrototypeUnitType.Worker,
                MovementDomain.Ground,
                roles,
                AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                100f,
                attackPower,
                0f,
                2f,
                6f,
                5f,
                3f,
                1,
                Vector2Int.one,
                !roles.HasFlag(UnitRole.Combat),
                false,
                0f);
            return unit;
        }

        private static GameObject CreateBuilding(string name, string kind, Vector3 position)
        {
            var building = new GameObject(name);
            building.transform.position = position;
            building.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = building.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", UnitTeam.Team2, Enum.Parse(BuildingKindType, kind), Vector2Int.one, true);
            return building;
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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
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
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }
    }
}
