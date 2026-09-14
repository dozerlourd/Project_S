using System;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        EnemyBuildingsDestroyed,
        PlayerMainBaseDestroyed,
        PlayerBuildingsDestroyed
    }

    public enum RtsMatchPostAction
    {
        None,
        Rematch,
        MainMenu,
        EndMatch
    }

    public sealed class RtsMatchController : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private UnitTeam enemyTeam = UnitTeam.Team2;
        [SerializeField, Min(0.02f)] private float evaluationInterval = 0.25f;
        [SerializeField] private bool stopActivityOnMatchEnd = true;
        [SerializeField] private bool reloadCurrentSceneOnRematch = true;
        [SerializeField] private bool loadMainMenuOnReturn = true;
        [SerializeField] private string mainMenuSceneName = RtsSceneFlow.DefaultMainMenuSceneName;
        [SerializeField] private bool quitApplicationOnEndMatch = true;

        private float nextEvaluationTime;
        private float matchStartTime;
        private float finalElapsedTime;

        public static RtsMatchController ActiveInstance { get; private set; }

        public event Action<RtsMatchController> MatchEnded;
        public event Action<RtsMatchPostAction> PostMatchActionRequested;

        public UnitTeam PlayerTeam => playerTeam;
        public UnitTeam EnemyTeam => enemyTeam;
        public RtsMatchResult Result { get; private set; } = RtsMatchResult.InProgress;
        public RtsMatchEndReason EndReason { get; private set; } = RtsMatchEndReason.None;
        public bool IsMatchOver => Result != RtsMatchResult.InProgress;
        public RtsMatchPostAction RequestedPostMatchAction { get; private set; }
        public int ResolutionCount { get; private set; }
        public float ElapsedPlayTime => IsMatchOver
            ? finalElapsedTime
            : Mathf.Max(0f, Time.unscaledTime - matchStartTime);
        public string ResultLabel => Result == RtsMatchResult.Victory
            ? "Victory"
            : Result == RtsMatchResult.Defeat
                ? "Defeat"
                : "In Progress";

        private void Awake()
        {
            ActiveInstance = this;
            matchStartTime = Time.unscaledTime;
            finalElapsedTime = 0f;
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            nextEvaluationTime = Time.time + evaluationInterval;
            if (!IsMatchOver)
            {
                matchStartTime = Time.unscaledTime;
                finalElapsedTime = 0f;
            }
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

        public void ConfigurePostMatchActions(bool reloadOnRematch, bool quitOnEndMatch)
        {
            reloadCurrentSceneOnRematch = reloadOnRematch;
            quitApplicationOnEndMatch = quitOnEndMatch;
        }

        public void ConfigureMainMenuAction(bool loadOnReturn, string sceneName)
        {
            loadMainMenuOnReturn = loadOnReturn;
            mainMenuSceneName = sceneName;
        }

        public bool TryRequestRematch()
        {
            if (!CanRequestPostMatchAction())
            {
                return false;
            }

            RequestedPostMatchAction = RtsMatchPostAction.Rematch;
            PostMatchActionRequested?.Invoke(RequestedPostMatchAction);
            if (reloadCurrentSceneOnRematch)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.buildIndex >= 0)
                {
                    SceneManager.LoadScene(activeScene.buildIndex);
                }
                else if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.name))
                {
                    SceneManager.LoadScene(activeScene.name);
                }
            }

            return true;
        }

        public bool TryRequestMainMenu()
        {
            if (!CanRequestPostMatchAction())
            {
                return false;
            }

            if (loadMainMenuOnReturn && !RtsSceneFlow.TryLoadScene(mainMenuSceneName))
            {
                return false;
            }

            RequestedPostMatchAction = RtsMatchPostAction.MainMenu;
            PostMatchActionRequested?.Invoke(RequestedPostMatchAction);
            return true;
        }

        public bool TryRequestEndMatch()
        {
            if (!CanRequestPostMatchAction())
            {
                return false;
            }

            RequestedPostMatchAction = RtsMatchPostAction.EndMatch;
            PostMatchActionRequested?.Invoke(RequestedPostMatchAction);
            RtsSceneFlow.RequestApplicationQuit(quitApplicationOnEndMatch);
            return true;
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
            if (!HasLivingBuildings(team))
            {
                reason = isPlayer
                    ? RtsMatchEndReason.PlayerBuildingsDestroyed
                    : RtsMatchEndReason.EnemyBuildingsDestroyed;
                return true;
            }

            reason = RtsMatchEndReason.None;
            return false;
        }

        private static bool HasLivingBuildings(UnitTeam team)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
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

        private void Resolve(RtsMatchResult result, RtsMatchEndReason reason)
        {
            if (IsMatchOver)
            {
                return;
            }

            Result = result;
            EndReason = reason;
            finalElapsedTime = Mathf.Max(0f, Time.unscaledTime - matchStartTime);
            ResolutionCount++;

            if (stopActivityOnMatchEnd)
            {
                StopMatchActivity();
            }

            MatchEnded?.Invoke(this);
        }

        private bool CanRequestPostMatchAction()
        {
            return IsMatchOver && RequestedPostMatchAction == RtsMatchPostAction.None;
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
