using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.AI
{
    public sealed class SimpleSkirmishAI : MonoBehaviour
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team2;
        [SerializeField] private UnitTeam enemyTeam = UnitTeam.Team1;
        [SerializeField, Min(0.1f)] private float decisionInterval = 1f;
        [SerializeField, Min(0)] private int desiredWorkers = 4;
        [SerializeField, Min(1)] private int attackGroupSize = 5;
        [SerializeField, Min(0.1f)] private float attackCommandInterval = 8f;
        [SerializeField] private PrototypeUnitType defaultWorkerType = PrototypeUnitType.Worker;
        [SerializeField] private PrototypeUnitType defaultCombatType = PrototypeUnitType.Soldier;
        [SerializeField] private ResourceType preferredResourceType = ResourceType.Minerals;
        [SerializeField] private Vector3 fallbackAttackPoint;

        private float nextDecisionTime;
        private float nextAttackCommandTime;

        public void Configure(
            UnitTeam controlledTeam,
            UnitTeam targetTeam,
            int workerTarget,
            int attackSize,
            Vector3 attackPoint)
        {
            team = controlledTeam;
            enemyTeam = targetTeam;
            desiredWorkers = Mathf.Max(0, workerTarget);
            attackGroupSize = Mathf.Max(1, attackSize);
            fallbackAttackPoint = attackPoint;
        }

        private void Update()
        {
            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            AssignIdleWorkersToResources();
            RunProduction();
            IssueAttackIfReady();
        }

        private void AssignIdleWorkersToResources()
        {
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.Mode != UnitCommandMode.Idle)
                {
                    continue;
                }

                var status = unit.Status;
                if (status == null || !status.CanGatherResources)
                {
                    continue;
                }

                var node = ResourceNode.FindNearestAvailable(unit.transform.position, preferredResourceType);
                if (node != null)
                {
                    unit.Issue(new UnitCommand(UnitCommandMode.Interact, node.InteractionPoint, null, node, false));
                }
            }
        }

        private void RunProduction()
        {
            var workers = CountUnits(defaultWorkerType);
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.Completed)
                {
                    continue;
                }

                var queue = building.GetComponent<UnitProductionQueue>();
                if (queue == null || queue.QueuedCount >= queue.MaxQueueSize)
                {
                    continue;
                }

                if (workers < desiredWorkers && queue.TryEnqueue(defaultWorkerType))
                {
                    workers++;
                    continue;
                }

                queue.TryEnqueue(defaultCombatType);
            }
        }

        private void IssueAttackIfReady()
        {
            if (Time.time < nextAttackCommandTime)
            {
                return;
            }

            var combatCount = CountCombatUnits();
            if (combatCount < attackGroupSize)
            {
                return;
            }

            var target = FindAttackTarget();
            nextAttackCommandTime = Time.time + attackCommandInterval;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var status = unit != null ? unit.Status : null;
                if (status == null || !status.Roles.HasFlag(UnitRole.Combat))
                {
                    continue;
                }

                unit.Issue(new UnitCommand(UnitCommandMode.AttackMove, target, null, false));
            }
        }

        private int CountUnits(PrototypeUnitType unitType)
        {
            var count = 0;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var status = units[i] != null ? units[i].Status : null;
                if (status != null && status.UnitType == unitType)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountCombatUnits()
        {
            var count = 0;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var status = units[i] != null ? units[i].Status : null;
                if (status != null && status.Roles.HasFlag(UnitRole.Combat))
                {
                    count++;
                }
            }

            return count;
        }

        private Vector3 FindAttackTarget()
        {
            var enemyBuildings = BuildingRegistry.GetBuildings(enemyTeam);
            for (var i = 0; i < enemyBuildings.Count; i++)
            {
                var building = enemyBuildings[i];
                if (building != null && building.Completed && building.gameObject.activeInHierarchy)
                {
                    return building.transform.position;
                }
            }

            var enemyUnits = UnitRegistry.GetAgents(enemyTeam);
            for (var i = 0; i < enemyUnits.Count; i++)
            {
                var unit = enemyUnits[i];
                if (unit != null && unit.gameObject.activeInHierarchy)
                {
                    return unit.transform.position;
                }
            }

            return fallbackAttackPoint;
        }
    }
}
