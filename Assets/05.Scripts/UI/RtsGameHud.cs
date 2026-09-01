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
        private ProjectS.RtsMatchController matchController;
        private string productionFeedback;
        private static readonly Texture2D[] CommandIcons = new Texture2D[6];
        private static readonly string[] CommandIconPaths =
        {
            "Temp/Commands/Command_Move",
            "Temp/Commands/Command_AttackMove",
            "Temp/Commands/Command_Patrol",
            "Temp/Commands/Command_HoldPosition",
            "Temp/Commands/Command_Stop",
            "Temp/Commands/Command_Build"
        };

        private const float ResourcePanelX = 12f;
        private const float ResourcePanelY = 12f;
        private const float ResourcePanelWidth = 260f;
        private const float ResourcePanelHeight = 64f;
        private const float SelectionPanelX = 12f;
        private const float SelectionPanelWidth = 360f;
        private const float SelectionPanelHeight = 220f;
        private const float CommandPanelWidth = 420f;
        private const float CommandPanelHeight = 400f;
        private const float CommandButtonSize = 64f;
        private const float CommandButtonGap = 8f;

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

            ResolvePlayerWallet();

            if (buildingPlacementService == null)
            {
                buildingPlacementService = BuildingPlacementService.ActiveInstance;
            }

            if (commandController != null)
            {
                commandController.SetDefaultBuildPlacementService(buildingPlacementService);
            }

            if (matchController == null)
            {
                matchController = ProjectS.RtsMatchController.ActiveInstance;
            }
        }

        public void Configure(UnitTeam team, BuildingPlacementService placementService)
        {
            playerTeam = team;
            buildingPlacementService = placementService;
            commandController = null;
            wallet = null;
        }

        private void ResolvePlayerWallet()
        {
            var activeWallet = PlayerResourceWallet.FindForTeam(playerTeam);
            if (wallet != activeWallet)
            {
                wallet = activeWallet;
            }
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

            DrawMatchResultOverlay();
        }

        private void DrawResourcePanel()
        {
            GUI.Box(new Rect(ResourcePanelX, ResourcePanelY, ResourcePanelWidth, ResourcePanelHeight), string.Empty);
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

            var rect = new Rect(SelectionPanelX, Screen.height - SelectionPanelHeight - 12f, SelectionPanelWidth, SelectionPanelHeight);
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
            else
            {
                var buildingHealth = selectionObject.GetComponent<BuildingHealth>();
                if (buildingHealth != null)
                {
                    GUI.Label(
                        new Rect(rect.x + 12f, rect.y + 60f, 320f, 22f),
                        $"HP: {buildingHealth.CurrentHealth:0}/{buildingHealth.MaxHealth:0}");
                }
            }

            var constructionSite = selectionObject.GetComponent<ConstructionSite>();
            if (constructionSite != null && !constructionSite.Completed)
            {
                GUI.Label(
                    new Rect(rect.x + 12f, rect.y + 84f, 320f, 22f),
                    $"Build: {constructionSite.BuildProgress01 * 100f:0}%");
                return;
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
                ? $"Producing: {active.DisplayName} ({productionQueue.ActiveProgress01 * 100f:0}%)"
                : "Production: Idle";
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 84f, 320f, 22f), progressLabel);
            if (active != null)
            {
                var barRect = new Rect(panelRect.x + 12f, panelRect.y + 106f, 220f, 14f);
                GUI.Box(barRect, string.Empty);
                DrawFilledRect(
                    new Rect(barRect.x + 2f, barRect.y + 2f, (barRect.width - 4f) * productionQueue.ActiveProgress01, barRect.height - 4f),
                    new Color(0.35f, 0.78f, 0.42f, 0.9f));
            }

            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 124f, 320f, 22f),
                $"Queue: {productionQueue.QueuedCount}/{productionQueue.MaxQueueSize}  Pending: {productionQueue.PendingCount}");
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 146f, 320f, 22f),
                $"Rally: {productionQueue.RallyPoint.x:0.0}, {productionQueue.RallyPoint.y:0.0}");

            var queueText = BuildPendingQueueText(productionQueue);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 168f, 330f, 42f), queueText);
        }

        private void DrawCommandPanel()
        {
            if (commandController == null || IsMatchOver())
            {
                return;
            }

            var width = CommandPanelWidth;
            var height = CommandPanelHeight;
            var rect = new Rect(Screen.width - width - 12f, Screen.height - height - 12f, width, height);
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, 320f, 22f), "Commands");

            if (DrawCommandButton(CommandButtonRect(rect, 0), 0, "Move M"))
            {
                commandController.BeginMoveCommand();
            }

            if (DrawCommandButton(CommandButtonRect(rect, 1), 1, "Attack A"))
            {
                commandController.BeginAttackMoveCommand();
            }

            if (DrawCommandButton(CommandButtonRect(rect, 2), 2, "Patrol P"))
            {
                commandController.BeginPatrolCommand();
            }

            if (DrawCommandButton(CommandButtonRect(rect, 3), 3, "Hold H"))
            {
                commandController.HoldSelectedUnits();
            }

            if (DrawCommandButton(CommandButtonRect(rect, 4), 4, "Stop S"))
            {
                commandController.StopSelectedUnits();
            }

            if (DrawCommandButton(CommandButtonRect(rect, 5), 5, "Build B"))
            {
                commandController.ToggleBuildMenu();
            }

            DrawBuildPlacementStatus(rect);
            if (commandController.IsBuildMenuOpen)
            {
                DrawBuildMenu(rect);
            }
            else
            {
                DrawCommandProductionButtons(rect);
            }
        }

        private void DrawBuildMenu(Rect panelRect)
        {
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 190f, 280f, 22f), "Build structure (select a Builder first)");
            DrawBuildOption(new Rect(panelRect.x + 12f, panelRect.y + 216f, 192f, 44f), BuildingKind.Production, "Combat Production\n150M");
            DrawBuildOption(new Rect(panelRect.x + 216f, panelRect.y + 216f, 192f, 44f), BuildingKind.SpliterProduction, "Spliter Production\n175M");
            DrawBuildOption(new Rect(panelRect.x + 12f, panelRect.y + 268f, 192f, 44f), BuildingKind.AutoTurret, "Auto Turret\n125M");
            DrawBuildOption(new Rect(panelRect.x + 216f, panelRect.y + 268f, 192f, 44f), BuildingKind.SpeedAura, "Speed Aura\n125M/25G");
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 322f, 396f, 38f), "Choose a building, then left-click a valid tile. Esc cancels placement.");
        }

        private void DrawBuildOption(Rect buttonRect, BuildingKind buildingKind, string label)
        {
            if (!GUI.Button(buttonRect, label))
            {
                return;
            }

            if (buildingPlacementService == null || !buildingPlacementService.SelectBuilding(buildingKind))
            {
                productionFeedback = buildingPlacementService != null
                    ? buildingPlacementService.LastPlacementFailureReason
                    : "No building placement service is available.";
                return;
            }

            commandController.BeginBuildPlacement(buildingPlacementService);
            commandController.CloseBuildMenu();
        }

        private static bool DrawCommandButton(Rect rect, int iconIndex, string fallbackLabel)
        {
            var clicked = GUI.Button(rect, GUIContent.none);
            if (CommandIcons[iconIndex] == null)
            {
                CommandIcons[iconIndex] = UnityEngine.Resources.Load<Texture2D>(CommandIconPaths[iconIndex]);
            }

            var icon = CommandIcons[iconIndex];
            if (icon == null)
            {
                GUI.Label(rect, fallbackLabel);
                return clicked;
            }

            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), icon, ScaleMode.ScaleToFit, true);
            return clicked;
        }

        private static Rect CommandButtonRect(Rect panelRect, int index)
        {
            var column = index < 4 ? index : index - 4;
            var row = index < 4 ? 0 : 1;
            return new Rect(
                panelRect.x + 12f + column * (CommandButtonSize + CommandButtonGap),
                panelRect.y + 38f + row * (CommandButtonSize + CommandButtonGap),
                CommandButtonSize,
                CommandButtonSize);
        }

        private void DrawBuildPlacementStatus(Rect panelRect)
        {
            var message = commandController.BuildPlacementStatusMessage;
            if (!string.IsNullOrWhiteSpace(message))
            {
                GUI.Label(new Rect(panelRect.x + 164f, panelRect.y + 110f, 244f, 64f), ShortenFailureReason(message));
            }
        }

        private void DrawCommandProductionButtons(Rect panelRect)
        {
            var selection = commandController.PrimarySelection;
            var selectionObject = selection != null ? selection.SelectionGameObject : null;
            var productionQueue = selectionObject != null ? selectionObject.GetComponent<UnitProductionQueue>() : null;
            if (productionQueue == null)
            {
                productionFeedback = string.Empty;
                return;
            }

            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 190f, 170f, 22f), "Production");
            if (GUI.Button(new Rect(panelRect.x + 330f, panelRect.y + 188f, 78f, 26f), "Rally"))
            {
                commandController.BeginRallyPointCommand(productionQueue);
                productionFeedback = "Click the map to set rally point.";
            }

            var definitions = productionQueue.ProducibleUnits;
            for (var i = 0; i < definitions.Count && i < 6; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var column = i % 3;
                var row = i / 3;
                var x = panelRect.x + 12f + column * 132f;
                var y = panelRect.y + 216f + row * 60f;
                var canEnqueue = productionQueue.CanEnqueue(definition, out var failureReason);
                var buttonLabel = $"{definition.DisplayName}\n{FormatCost(definition.Cost)}";
                if (GUI.Button(new Rect(x, y, 124f, 44f), buttonLabel))
                {
                    if (productionQueue.TryEnqueue(i))
                    {
                        productionFeedback = $"Queued {definition.DisplayName}.";
                    }
                    else
                    {
                        productionFeedback = productionQueue.LastEnqueueFailureReason;
                    }
                }

                if (!canEnqueue)
                {
                    GUI.Label(new Rect(x, y + 45f, 124f, 18f), ShortenFailureReason(failureReason));
                }
            }

            var feedback = !string.IsNullOrWhiteSpace(productionQueue.LastEnqueueFailureReason)
                ? productionQueue.LastEnqueueFailureReason
                : productionFeedback;
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 348f, 396f, 18f), feedback);
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

        private void DrawMatchResultOverlay()
        {
            if (!IsMatchOver())
            {
                return;
            }

            var width = 360f;
            var height = 132f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold
            };
            var reasonStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };

            GUI.Label(new Rect(rect.x + 16f, rect.y + 24f, rect.width - 32f, 44f), matchController.ResultLabel, titleStyle);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 74f, rect.width - 32f, 28f),
                FormatEndReason(matchController.EndReason),
                reasonStyle);
        }

        private bool IsMatchOver()
        {
            return matchController != null && matchController.IsMatchOver;
        }

        private static string FormatEndReason(ProjectS.RtsMatchEndReason reason)
        {
            switch (reason)
            {
                case ProjectS.RtsMatchEndReason.EnemyMainBaseDestroyed:
                    return "Enemy main base destroyed";
                case ProjectS.RtsMatchEndReason.EnemyEliminated:
                    return "Enemy forces eliminated";
                case ProjectS.RtsMatchEndReason.PlayerMainBaseDestroyed:
                    return "Player main base destroyed";
                case ProjectS.RtsMatchEndReason.PlayerEliminated:
                    return "Player forces eliminated";
                default:
                    return string.Empty;
            }
        }

        private static string BuildPendingQueueText(UnitProductionQueue productionQueue)
        {
            if (productionQueue.PendingCount <= 0)
            {
                return "Pending: none";
            }

            var text = "Pending:";
            for (var i = 0; i < productionQueue.PendingCount && i < 3; i++)
            {
                var pending = productionQueue.GetPendingProduction(i);
                if (pending != null)
                {
                    text += $" {i + 1}.{pending.DisplayName}";
                }
            }

            if (productionQueue.PendingCount > 3)
            {
                text += $" +{productionQueue.PendingCount - 3}";
            }

            return text;
        }

        private static string FormatCost(ResourceAmount cost)
        {
            if (cost.IsEmpty)
            {
                return "Free";
            }

            return $"{cost.Minerals}M/{cost.Gas}G";
        }

        private static string ShortenFailureReason(string failureReason)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                return string.Empty;
            }

            var normalizedReason = failureReason.ToLowerInvariant();
            if (normalizedReason.Contains("left-click")
                || normalizedReason.Contains("select a builder")
                || normalizedReason.Contains("friendly builder")
                || normalizedReason.Contains("move cursor"))
            {
                return failureReason;
            }

            if (normalizedReason.Contains("insufficient resources"))
            {
                return "Need resources";
            }

            if (normalizedReason.Contains("resource wallet"))
            {
                return "No wallet";
            }

            if (normalizedReason.Contains("not buildable"))
            {
                return "Cannot build here";
            }

            if (normalizedReason.Contains("resource node"))
            {
                return "Resource occupied";
            }

            if (normalizedReason.Contains("construction site"))
            {
                return "Site occupied";
            }

            if (normalizedReason.Contains("building"))
            {
                return "Building occupied";
            }

            if (normalizedReason.Contains("unit"))
            {
                return "Unit occupied";
            }

            if (normalizedReason.Contains("queue is full"))
            {
                return "Queue full";
            }

            if (normalizedReason.Contains("not completed"))
            {
                return "Incomplete";
            }

            return "Unavailable";
        }

        private static void DrawFilledRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
