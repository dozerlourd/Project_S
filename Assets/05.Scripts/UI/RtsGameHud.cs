using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.UI
{
    public sealed class RtsGameHud : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private bool showPathStats = true;
        [SerializeField] private BuildingPlacementService buildingPlacementService;

        private PlayerUnitCommandController commandController;
        private PlayerResourceWallet wallet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeHud()
        {
            if (FindFirstObjectByType<RtsGameHud>() != null)
            {
                return;
            }

            var hudObject = new GameObject("RtsGameHud");
            hudObject.AddComponent<RtsGameHud>();
        }

        private void Update()
        {
            if (commandController == null)
            {
                commandController = PlayerUnitCommandController.ActiveInstance;
                if (commandController != null)
                {
                    playerTeam = commandController.PlayerTeam;
                }
            }

            if (wallet == null)
            {
                wallet = PlayerResourceWallet.FindForTeam(playerTeam);
            }

            if (buildingPlacementService == null)
            {
                buildingPlacementService = BuildingPlacementService.ActiveInstance;
            }
        }

        public void Configure(UnitTeam team, BuildingPlacementService placementService)
        {
            playerTeam = team;
            buildingPlacementService = placementService;
            commandController = null;
            wallet = null;
        }

        private void OnGUI()
        {
            DrawResourcePanel();
            DrawSelectionPanel();
            DrawCommandPanel();
            if (showPathStats)
            {
                DrawPathStatsPanel();
            }
        }

        private void DrawResourcePanel()
        {
            GUI.Box(new Rect(12f, 12f, 260f, 64f), string.Empty);
            var minerals = wallet != null ? wallet.Minerals : 0;
            var gas = wallet != null ? wallet.Gas : 0;
            GUI.Label(new Rect(24f, 22f, 120f, 22f), $"Minerals: {minerals}");
            GUI.Label(new Rect(144f, 22f, 100f, 22f), $"Gas: {gas}");
            GUI.Label(new Rect(24f, 46f, 220f, 22f), $"Team: {playerTeam}");
        }

        private void DrawSelectionPanel()
        {
            if (commandController == null)
            {
                return;
            }

            var rect = new Rect(12f, Screen.height - 172f, 360f, 160f);
            GUI.Box(rect, string.Empty);

            var selectedUnits = commandController.SelectedUnits;
            var selection = commandController.PrimarySelection;
            if (selectedUnits.Count > 1)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, 320f, 22f), $"Selected Units: {selectedUnits.Count}");
                return;
            }

            if (selection == null)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, 320f, 22f), "No Selection");
                return;
            }

            GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, 320f, 22f), selection.SelectionName);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 36f, 320f, 22f), $"Team: {selection.Team}");

            var selectionObject = selection.SelectionGameObject;
            if (selectionObject == null)
            {
                return;
            }

            var health = selectionObject.GetComponent<UnitHealth>();
            if (health != null)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 60f, 320f, 22f), $"HP: {health.CurrentHealth:0}/{health.MaxHealth:0}");
            }

            var constructionSite = selectionObject.GetComponent<ConstructionSite>();
            if (constructionSite != null && !constructionSite.Completed)
            {
                GUI.Label(
                    new Rect(rect.x + 12f, rect.y + 84f, 320f, 22f),
                    $"Build: {constructionSite.BuildProgress01 * 100f:0}%");
            }

            var productionQueue = selectionObject.GetComponent<UnitProductionQueue>();
            if (productionQueue != null)
            {
                DrawProductionStatus(rect, productionQueue);
            }
        }

        private void DrawProductionStatus(Rect panelRect, UnitProductionQueue productionQueue)
        {
            var active = productionQueue.ActiveProduction;
            var progressLabel = active != null
                ? $"{active.DisplayName}: {productionQueue.ActiveProgress01 * 100f:0}%"
                : "Production: Idle";
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 84f, 320f, 22f), progressLabel);
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 106f, 320f, 22f),
                $"Queue: {productionQueue.QueuedCount}/{productionQueue.MaxQueueSize}");
        }

        private void DrawCommandPanel()
        {
            if (commandController == null)
            {
                return;
            }

            var width = 360f;
            var height = 172f;
            var rect = new Rect(Screen.width - width - 12f, Screen.height - height - 12f, width, height);
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, 320f, 22f), "Commands");

            if (GUI.Button(new Rect(rect.x + 12f, rect.y + 38f, 78f, 28f), "Move"))
            {
                commandController.BeginMoveCommand();
            }

            if (GUI.Button(new Rect(rect.x + 96f, rect.y + 38f, 78f, 28f), "Attack"))
            {
                commandController.BeginAttackMoveCommand();
            }

            if (GUI.Button(new Rect(rect.x + 180f, rect.y + 38f, 78f, 28f), "Patrol"))
            {
                commandController.BeginPatrolCommand();
            }

            if (GUI.Button(new Rect(rect.x + 264f, rect.y + 38f, 78f, 28f), "Hold"))
            {
                commandController.HoldSelectedUnits();
            }

            if (GUI.Button(new Rect(rect.x + 12f, rect.y + 72f, 78f, 28f), "Stop"))
            {
                commandController.StopSelectedUnits();
            }

            if (GUI.Button(new Rect(rect.x + 96f, rect.y + 72f, 92f, 28f), "Build")
                && buildingPlacementService != null)
            {
                commandController.BeginBuildPlacement(buildingPlacementService);
            }

            DrawCommandProductionButtons(rect);
        }

        private void DrawCommandProductionButtons(Rect panelRect)
        {
            var selection = commandController.PrimarySelection;
            var selectionObject = selection != null ? selection.SelectionGameObject : null;
            var productionQueue = selectionObject != null ? selectionObject.GetComponent<UnitProductionQueue>() : null;
            if (productionQueue == null)
            {
                return;
            }

            var definitions = productionQueue.ProducibleUnits;
            for (var i = 0; i < definitions.Count && i < 4; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var buttonRect = new Rect(panelRect.x + 12f + i * 82f, panelRect.y + 132f, 76f, 28f);
                if (GUI.Button(buttonRect, definition.DisplayName))
                {
                    productionQueue.TryEnqueue(i);
                }
            }
        }

        private void DrawPathStatsPanel()
        {
            var scheduler = UnitPathRequestScheduler.Instance;
            GUI.Box(new Rect(Screen.width - 292f, 12f, 280f, 92f), string.Empty);
            GUI.Label(new Rect(Screen.width - 280f, 22f, 252f, 22f), $"Path Pending: {scheduler.PendingRequestCount}");
            GUI.Label(
                new Rect(Screen.width - 280f, 46f, 252f, 22f),
                $"Frame E/P/C/F/D: {scheduler.EnqueuedThisFrame}/{scheduler.ProcessedThisFrame}/"
                    + $"{scheduler.CompletedThisFrame}/{scheduler.FailedThisFrame}/{scheduler.DiscardedThisFrame}");
            GUI.Label(new Rect(Screen.width - 280f, 70f, 252f, 22f), $"Peak: {scheduler.PeakPendingRequests}");
        }
    }
}
