using System;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS
{
    public enum RtsMatchResult
    {
        InProgress,
        Victory,
        Defeat
    }

    public enum RtsMatchEndReason
    {
        None,
        EnemyMainBaseDestroyed,
        EnemyEliminated,
        PlayerMainBaseDestroyed,
        PlayerEliminated
    }

    public sealed class RtsMatchController : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private UnitTeam enemyTeam = UnitTeam.Team2;
        [SerializeField, Min(0.02f)] private float evaluationInterval = 0.25f;
        [SerializeField] private bool stopActivityOnMatchEnd = true;

        private float nextEvaluationTime;

        public static RtsMatchController ActiveInstance { get; private set; }

        public event Action<RtsMatchController> MatchEnded;

        public UnitTeam PlayerTeam => playerTeam;
        public UnitTeam EnemyTeam => enemyTeam;
        public RtsMatchResult Result { get; private set; } = RtsMatchResult.InProgress;
        public RtsMatchEndReason EndReason { get; private set; } = RtsMatchEndReason.None;
        public bool IsMatchOver => Result != RtsMatchResult.InProgress;
        public int ResolutionCount { get; private set; }
        public string ResultLabel => Result == RtsMatchResult.Victory
            ? "Victory"
            : Result == RtsMatchResult.Defeat
                ? "Defeat"
                : "In Progress";

        private void Awake()
        {
            ActiveInstance = this;
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            nextEvaluationTime = Time.time + evaluationInterval;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        private void Update()
        {
            if (IsMatchOver || Time.time < nextEvaluationTime)
            {
                return;
            }

            nextEvaluationTime = Time.time + evaluationInterval;
            EvaluateMatch();
        }

        public void Configure(UnitTeam player, UnitTeam enemy)
        {
            playerTeam = player;
            enemyTeam = enemy;
        }

        public void ForceEvaluate()
        {
            EvaluateMatch();
        }

        private void EvaluateMatch()
        {
            if (IsMatchOver)
            {
                return;
            }

            var playerDefeated = TryGetDefeatReason(playerTeam, true, out var playerReason);
            var enemyDefeated = TryGetDefeatReason(enemyTeam, false, out var enemyReason);

            if (playerDefeated)
            {
                Resolve(RtsMatchResult.Defeat, playerReason);
                return;
            }

            if (enemyDefeated)
            {
                Resolve(RtsMatchResult.Victory, enemyReason);
            }
        }

        private bool TryGetDefeatReason(UnitTeam team, bool isPlayer, out RtsMatchEndReason reason)
        {
            if (!HasLivingMainBase(team))
            {
                reason = isPlayer
                    ? RtsMatchEndReason.PlayerMainBaseDestroyed
                    : RtsMatchEndReason.EnemyMainBaseDestroyed;
                return true;
            }

            if (!HasLivingUnits(team))
            {
                reason = isPlayer
                    ? RtsMatchEndReason.PlayerEliminated
                    : RtsMatchEndReason.EnemyEliminated;
                return true;
            }

            reason = RtsMatchEndReason.None;
            return false;
        }

        private static bool HasLivingMainBase(UnitTeam team)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || building.Kind != BuildingKind.MainBase
                    || !building.Completed
                    || !building.gameObject.activeInHierarchy
                    || !building.IsAlive)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool HasLivingUnits(UnitTeam team)
        {
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var target = unit.Status as IUnitAttackTarget;
                if (target != null && target.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private void Resolve(RtsMatchResult result, RtsMatchEndReason reason)
        {
            if (IsMatchOver)
            {
                return;
            }

            Result = result;
            EndReason = reason;
            ResolutionCount++;

            if (stopActivityOnMatchEnd)
            {
                StopMatchActivity();
            }

            MatchEnded?.Invoke(this);
        }

        private void StopMatchActivity()
        {
            var agents = UnitRegistry.AllAgents;
            for (var i = 0; i < agents.Count; i++)
            {
                agents[i]?.Stop();
            }

            var aiControllers = FindObjectsByType<SimpleSkirmishAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < aiControllers.Length; i++)
            {
                aiControllers[i].enabled = false;
            }

            var productionQueues = FindObjectsByType<UnitProductionQueue>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < productionQueues.Length; i++)
            {
                productionQueues[i].enabled = false;
            }

            var commandControllers = FindObjectsByType<PlayerUnitCommandController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var i = 0; i < commandControllers.Length; i++)
            {
                commandControllers[i].enabled = false;
            }
        }
    }
}
