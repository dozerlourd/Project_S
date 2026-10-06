using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Units;
using ProjectS.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS.UI
{
    public sealed class RtsGameHud : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private bool showPathStats = true;
        [SerializeField] private BuildingPlacementService buildingPlacementService;
        [SerializeField] private WorkerAutoAssignmentManager workerAutoAssignmentManager;

        private PlayerUnitCommandController commandController;
        private PlayerResourceWallet wallet;
        private SupplyManager supplyManager;
        private ProjectS.RtsMatchController matchController;
        private UnitProductionQueue displayedProductionQueue;
        private TeamUpgradeResearch displayedUpgradeResearch;
        private string productionFeedback;
        private string upgradeFeedback;
        private string skillFeedback;
        private int selectedPendingProductionIndex;
        private bool showUpgradeResearch;
        private static readonly Texture2D[] CommandIcons = new Texture2D[6];
        private static readonly Key[] ContextHotkeys =
        {
            Key.Q,
            Key.E,
            Key.R,
            Key.T,
            Key.Y,
            Key.U,
            Key.I,
            Key.O
        };
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
        private const float ResourcePanelHeight = 214f;
        private const float MatchTimerWidth = 132f;
        private const float MatchTimerHeight = 38f;
        private const float MatchTimerTopMargin = 14f;
        private const float BottomPanelMargin = 10f;
        private const float BottomPanelGap = 8f;
        private const float BottomPanelHeight = 154f;
        private const float SelectionPanelMinWidth = 220f;
        private const float SelectionPanelMaxWidth = 300f;
        private const float CommandPanelMinWidth = 280f;
        private const float CommandPanelMaxWidth = 360f;
        private const float CommandButtonMaxSize = 46f;
        private const float CommandButtonGap = 5f;

        public static RtsGameHud ActiveInstance { get; private set; }

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
            ResolveWorkerAutoAssignmentManager();

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

            ResolveDisplayedProductionQueue();
            displayedUpgradeResearch = TeamUpgradeResearch.FindForTeam(playerTeam);
            HandleContextHotkeys();
        }

        private void Awake()
        {
            ActiveInstance = this;
        }

        private void HandleContextHotkeys()
        {
            if (commandController == null || IsMatchOver() || showUpgradeResearch || IsContextHotkeyModifierPressed())
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame && ResolveDisplayedSkillController() != null)
            {
                TryActivateSelectedSkill(0);
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && commandController.TryBeginRallyPointForPrimarySelection())
            {
                productionFeedback = "Click the map to set rally point.";
                return;
            }

            for (var i = 0; i < ContextHotkeys.Length; i++)
            {
                if (!keyboard[ContextHotkeys[i]].wasPressedThisFrame)
                {
                    continue;
                }

                if (commandController.IsBuildMenuOpen)
                {
                    TryBeginBuildPlacement(i);
                }
                else
                {
                    TryQueueProduction(ResolveDisplayedProductionQueue(), i);
                }

                return;
            }
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public void Configure(UnitTeam team, BuildingPlacementService placementService)
        {
            playerTeam = team;
            buildingPlacementService = placementService;
            commandController = null;
            wallet = null;
            supplyManager = null;
            workerAutoAssignmentManager = null;
            displayedProductionQueue = null;
            displayedUpgradeResearch = null;
            showUpgradeResearch = false;
            skillFeedback = string.Empty;
        }

        private void ResolvePlayerWallet()
        {
            var activeWallet = PlayerResourceWallet.FindForTeam(playerTeam);
            if (wallet != activeWallet)
            {
                wallet = activeWallet;
            }

            var activeSupplyManager = SupplyManager.FindForTeam(playerTeam);
            if (supplyManager != activeSupplyManager)
            {
                supplyManager = activeSupplyManager;
            }
        }

        private void OnGUI()
        {
            DrawResourcePanel();
            DrawMatchTimer();
            var bottomLayout = CalculateBottomLayout();
            DrawSelectionPanel(bottomLayout.selectionRect);
            DrawCommandPanel(bottomLayout.commandRect, bottomLayout.contextRect);
            if (showPathStats)
            {
                DrawPathStatsPanel();
            }

            DrawMatchResultOverlay();
        }

        private void DrawMatchTimer()
        {
            if (matchController == null)
            {
                return;
            }

            var timerRect = new Rect(
                (Screen.width - MatchTimerWidth) * 0.5f,
                MatchTimerTopMargin,
                MatchTimerWidth,
                MatchTimerHeight);
            GUI.Box(timerRect, string.Empty);

            var totalSeconds = Mathf.FloorToInt(matchController.ElapsedPlayTime);
            var label = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(timerRect, label, style);
        }

        private static BottomHudLayout CalculateBottomLayout()
        {
            var availableWidth = Mathf.Max(0f, Screen.width - BottomPanelMargin - RtsMinimap.BottomRightReservedWidth);
            var selectionWidth = Mathf.Clamp(availableWidth * 0.24f, SelectionPanelMinWidth, SelectionPanelMaxWidth);
            var commandWidth = Mathf.Clamp(availableWidth * 0.3f, CommandPanelMinWidth, CommandPanelMaxWidth);
            var contextWidth = Mathf.Max(0f, availableWidth - selectionWidth - commandWidth - BottomPanelGap * 2f);
            var y = Screen.height - BottomPanelHeight - BottomPanelMargin;
            var selectionRect = new Rect(BottomPanelMargin, y, selectionWidth, BottomPanelHeight);
            var commandRect = new Rect(selectionRect.xMax + BottomPanelGap, y, commandWidth, BottomPanelHeight);
            var contextRect = new Rect(commandRect.xMax + BottomPanelGap, y, contextWidth, BottomPanelHeight);
            return new BottomHudLayout(selectionRect, commandRect, contextRect);
        }

        public bool IsPointerOverInteractiveHud(Vector2 screenPosition)
        {
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (workerAutoAssignmentManager != null
                && new Rect(24f, 138f, 220f, 24f).Contains(guiPosition))
            {
                return true;
            }

            if (IsMatchOver())
            {
                return IsPointerOverMatchResultButton(guiPosition);
            }

            if (commandController == null)
            {
                return false;
            }

            var bottomLayout = CalculateBottomLayout();
            if (IsPointerOverCommandButton(guiPosition, bottomLayout.commandRect))
            {
                return true;
            }

            if (commandController.IsBuildMenuOpen)
            {
                return IsPointerOverBuildOption(guiPosition, bottomLayout.contextRect);
            }

            var skillController = ResolveDisplayedSkillController();
            if (skillController != null)
            {
                return new Rect(bottomLayout.contextRect.x + 8f, bottomLayout.contextRect.y + 30f, 132f, 46f).Contains(guiPosition);
            }

            return IsPointerOverProductionControl(guiPosition, bottomLayout.contextRect, ResolveDisplayedProductionQueue());
        }

        private static bool IsPointerOverCommandButton(Vector2 guiPosition, Rect commandRect)
        {
            for (var i = 0; i < 6; i++)
            {
                if (CommandButtonRect(commandRect, i).Contains(guiPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointerOverBuildOption(Vector2 guiPosition, Rect panelRect)
        {
            var buttonGap = 6f;
            var columns = panelRect.width >= 560f ? 5 : panelRect.width >= 420f ? 3 : 2;
            var buttonWidth = (panelRect.width - 16f - buttonGap * (columns - 1)) / columns;
            var buttonHeight = columns == 4 ? 46f : 28f;
            for (var i = 0; i < GetBuildOptionCount(); i++)
            {
                if (BuildOptionRect(panelRect, i, columns, buttonWidth, buttonHeight, buttonGap).Contains(guiPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointerOverProductionControl(Vector2 guiPosition, Rect panelRect, UnitProductionQueue productionQueue)
        {
            if (productionQueue == null)
            {
                return false;
            }

            var research = displayedUpgradeResearch ?? TeamUpgradeResearch.FindForTeam(playerTeam);
            if (showUpgradeResearch && research != null)
            {
                return IsPointerOverResearchControl(guiPosition, panelRect, research);
            }

            if (research != null && new Rect(panelRect.x + panelRect.width - 236f, panelRect.y + 4f, 72f, 24f).Contains(guiPosition))
            {
                return true;
            }

            if (new Rect(panelRect.x + panelRect.width - 158f, panelRect.y + 4f, 72f, 24f).Contains(guiPosition))
            {
                return true;
            }

            if (productionQueue.ActiveProduction != null
                && new Rect(panelRect.x + panelRect.width - 80f, panelRect.y + 4f, 72f, 24f).Contains(guiPosition))
            {
                return true;
            }

            var definitions = productionQueue.ProducibleUnits;
            var visibleButtonCount = Mathf.Min(
                definitions.Count,
                Mathf.FloorToInt((panelRect.width - 16f + CommandButtonGap) / (CommandButtonMaxSize + CommandButtonGap)));
            for (var i = 0; i < visibleButtonCount; i++)
            {
                if (definitions[i] == null)
                {
                    continue;
                }

                var rect = new Rect(
                    panelRect.x + 8f + i * (CommandButtonMaxSize + CommandButtonGap),
                    panelRect.y + 42f,
                    CommandButtonMaxSize,
                    CommandButtonMaxSize);
                if (rect.Contains(guiPosition))
                {
                    return true;
                }
            }

            if (productionQueue.PendingCount <= 0)
            {
                return false;
            }

            for (var i = 0; i < productionQueue.PendingCount && i < 3; i++)
            {
                if (new Rect(panelRect.x + 72f + i * 30f, panelRect.y + 88f, 26f, 22f).Contains(guiPosition))
                {
                    return true;
                }
            }

            return new Rect(panelRect.x + 170f, panelRect.y + 88f, 116f, 22f).Contains(guiPosition);
        }

        private static bool IsPointerOverResearchControl(Vector2 guiPosition, Rect panelRect, TeamUpgradeResearch research)
        {
            if (new Rect(panelRect.x + panelRect.width - 80f, panelRect.y + 4f, 72f, 24f).Contains(guiPosition))
            {
                return true;
            }

            var definitions = research.Definitions;
            for (var i = 0; i < definitions.Count && i < 2; i++)
            {
                if (definitions[i] == null)
                {
                    continue;
                }

                var buttonWidth = (panelRect.width - 22f) * 0.5f;
                var buttonRect = new Rect(panelRect.x + 8f + i * buttonWidth, panelRect.y + 58f, buttonWidth, 38f);
                if (buttonRect.Contains(guiPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointerOverMatchResultButton(Vector2 guiPosition)
        {
            var width = 360f;
            var height = 188f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            return new Rect(rect.x + 20f, rect.y + 116f, 96f, 34f).Contains(guiPosition)
                || new Rect(rect.x + 132f, rect.y + 116f, 96f, 34f).Contains(guiPosition)
                || new Rect(rect.x + 244f, rect.y + 116f, 96f, 34f).Contains(guiPosition);
        }

        private void DrawResourcePanel()
        {
            GUI.Box(new Rect(ResourcePanelX, ResourcePanelY, ResourcePanelWidth, ResourcePanelHeight), string.Empty);
            var minerals = wallet != null ? wallet.Minerals : 0;
            var gas = wallet != null ? wallet.Gas : 0;
            GUI.Label(new Rect(24f, 22f, 120f, 22f), $"Minerals: {minerals}");
            GUI.Label(new Rect(144f, 22f, 100f, 22f), $"Gas: {gas}");
            DrawPopulationLabel(
                new Rect(24f, 46f, 236f, 22f),
                "General Population",
                supplyManager != null ? supplyManager.CurrentSupply : 0,
                supplyManager != null ? supplyManager.MaxSupply : 0,
                supplyManager != null ? supplyManager.ReservedSupply : 0,
                new Color(0.5f, 0.86f, 1f));
            DrawPopulationLabel(
                new Rect(24f, 70f, 236f, 22f),
                "Advanced Population",
                supplyManager != null ? supplyManager.CurrentAdvancedSupply : 0,
                supplyManager != null ? supplyManager.MaxAdvancedSupply : 0,
                supplyManager != null ? supplyManager.ReservedAdvancedSupply : 0,
                new Color(1f, 0.76f, 0.34f));
            GUI.Label(new Rect(24f, 96f, 220f, 20f), $"Team: {playerTeam}");
            var autoLabel = workerAutoAssignmentManager == null
                ? "Auto Workers: Unavailable"
                : $"Auto Workers: {(workerAutoAssignmentManager.AutomaticAssignmentEnabled ? "ON" : "OFF")}  ({workerAutoAssignmentManager.AssignedWorkerCount})";
            GUI.Label(new Rect(24f, 117f, 220f, 20f), autoLabel);
            if (workerAutoAssignmentManager != null
                && GUI.Button(new Rect(24f, 138f, 220f, 24f), workerAutoAssignmentManager.AutomaticAssignmentEnabled
                    ? "Disable automatic assignment"
                    : "Enable automatic assignment"))
            {
                ToggleWorkerAutoAssignment();
            }

            var research = TeamUpgradeResearch.FindForTeam(playerTeam);
            var researchLabel = research != null && research.ActiveDefinition != null
                ? $"Research: {research.ActiveDefinition.DisplayName} {research.ActiveProgress01 * 100f:0}%"
                : "Research: Idle";
            GUI.Label(new Rect(24f, 170f, 236f, 18f), researchLabel);
        }

        private static void DrawPopulationLabel(
            Rect rect,
            string label,
            int current,
            int maximum,
            int reserved,
            Color color)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(
                rect,
                $"{label}: {Mathf.Max(0, current)}/{Mathf.Max(0, maximum)} ({Mathf.Max(0, reserved)})");
            GUI.contentColor = previousColor;
        }

        private void DrawSelectionPanel(Rect rect)
        {
            if (commandController == null)
            {
                return;
            }

            GUI.Box(rect, string.Empty);

            var selectedUnits = commandController.SelectedUnits;
            var selection = commandController.PrimarySelection;
            if (selectedUnits.Count > 1)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 22f), $"Selected Units: {selectedUnits.Count}");
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 34f, rect.width - 20f, 22f),
                    DescribeSelectedCommands(selectedUnits));
                return;
            }

            if (selection == null)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 22f), "No Selection");
                return;
            }

            GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 22f), selection.SelectionName);

            var selectionObject = selection.SelectionGameObject;
            if (selectionObject == null)
            {
                return;
            }

            var resourceNode = selectionObject.GetComponent<ResourceNode>();
            if (resourceNode != null)
            {
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 34f, rect.width - 20f, 22f),
                    $"Resource: {resourceNode.ResourceType}");
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 58f, rect.width - 20f, 22f),
                    $"Amount: {resourceNode.RemainingAmount}/{resourceNode.MaximumAmount}");
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 82f, rect.width - 20f, 22f),
                    resourceNode.IsDepleted ? "Status: Depleted" : "Status: Available");
                return;
            }

            GUI.Label(new Rect(rect.x + 10f, rect.y + 32f, rect.width - 20f, 22f), $"Team: {selection.Team}");

            var health = selectionObject.GetComponent<UnitHealth>();
            if (health != null)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 54f, rect.width - 20f, 22f), $"HP: {health.CurrentHealth:0}/{health.MaxHealth:0}");
            }
            else
            {
                var buildingHealth = selectionObject.GetComponent<BuildingHealth>();
                if (buildingHealth != null)
                {
                    GUI.Label(
                        new Rect(rect.x + 10f, rect.y + 54f, rect.width - 20f, 22f),
                        $"HP: {buildingHealth.CurrentHealth:0}/{buildingHealth.MaxHealth:0}");
                }
            }

            var constructionSite = selectionObject.GetComponent<ConstructionSite>();
            if (constructionSite != null && !constructionSite.Completed)
            {
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 76f, rect.width - 20f, 22f),
                    $"Build: {constructionSite.BuildProgress01 * 100f:0}%");
                return;
            }

            var commandAgent = selectionObject.GetComponent<UnitCommandAgent>();
            if (commandAgent != null)
            {
                var unitStatus = selectionObject.GetComponent<PrototypeUnitStatus>();
                if (unitStatus != null)
                {
                    GUI.Label(
                        new Rect(rect.x + 10f, rect.y + 76f, rect.width - 20f, 22f),
                        $"ATK: {unitStatus.PhysicalAttackPower:0.0}  Move: {unitStatus.MovementSpeed:0.00}");
                }
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 98f, rect.width - 20f, 20f),
                    DescribeSelectedCommands(selectedUnits));
                return;
            }

            var productionQueue = selectionObject.GetComponent<UnitProductionQueue>();
            if (productionQueue != null)
            {
                var activeProduction = productionQueue.ActiveProduction;
                var productionLabel = activeProduction != null
                    ? $"Producing: {activeProduction.DisplayName} {productionQueue.ActiveProgress01 * 100f:0}%"
                    : "Production: Idle";
                var rallyPoint = productionQueue.RallyPoint;
                GUI.Label(new Rect(rect.x + 10f, rect.y + 76f, rect.width - 20f, 22f), productionLabel);
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 98f, rect.width - 20f, 22f),
                    $"Rally: {rallyPoint.x:0.0}, {rallyPoint.y:0.0}");
            }
        }

        public static string DescribeSelectedCommands(IReadOnlyList<UnitCommandAgent> units)
        {
            UnitCommandMode? mode = null;
            UnitActionState? state = null;
            var hasMixedMode = false;
            var hasMixedState = false;
            var liveCount = 0;

            if (units != null)
            {
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null)
                    {
                        continue;
                    }

                    liveCount++;
                    if (mode == null)
                    {
                        mode = unit.Mode;
                    }
                    else if (mode.Value != unit.Mode)
                    {
                        hasMixedMode = true;
                    }

                    if (state == null)
                    {
                        state = unit.ActionState;
                    }
                    else if (state.Value != unit.ActionState)
                    {
                        hasMixedState = true;
                    }
                }
            }

            if (liveCount == 0)
            {
                return "Command: None";
            }

            var modeLabel = hasMixedMode ? "Mixed" : FormatCommandMode(mode.Value);
            var stateLabel = hasMixedState ? "Mixed" : FormatActionState(state.Value);
            return $"Command: {modeLabel} | State: {stateLabel}";
        }

        private void DrawCommandPanel(Rect commandRect, Rect contextRect)
        {
            if (commandController == null || IsMatchOver())
            {
                return;
            }

            GUI.Box(commandRect, string.Empty);

            if (DrawCommandButton(CommandButtonRect(commandRect, 0), 0, "Move M"))
            {
                commandController.BeginMoveCommand();
            }

            if (DrawCommandButton(CommandButtonRect(commandRect, 1), 1, "Attack Move A"))
            {
                commandController.BeginAttackMoveCommand();
            }

            if (DrawCommandButton(CommandButtonRect(commandRect, 2), 2, "Patrol P"))
            {
                commandController.BeginPatrolCommand();
            }

            if (DrawCommandButton(CommandButtonRect(commandRect, 3), 3, "Hold H"))
            {
                commandController.HoldSelectedUnits();
            }

            if (DrawCommandButton(CommandButtonRect(commandRect, 4), 4, "Stop S"))
            {
                commandController.StopSelectedUnits();
            }

            var canUseBuildCommands = commandController.CanUseBuildCommands;
            var previousGuiEnabled = GUI.enabled;
            GUI.enabled = canUseBuildCommands;
            var buildButtonClicked = DrawCommandButton(CommandButtonRect(commandRect, 5), 5, "Build B");
            GUI.enabled = previousGuiEnabled;
            if (buildButtonClicked)
            {
                commandController.ToggleBuildMenu();
            }

            DrawPendingCommandStatus(commandRect);
            if (commandController.IsBuildMenuOpen)
            {
                DrawBuildMenu(contextRect);
            }
            else if (ResolveDisplayedSkillController() != null)
            {
                DrawActiveSkillPanel(contextRect);
            }
            else
            {
                DrawProductionPanel(contextRect);
            }
        }

        private void ResolveWorkerAutoAssignmentManager()
        {
            if (workerAutoAssignmentManager == null || workerAutoAssignmentManager.Team != playerTeam)
            {
                workerAutoAssignmentManager = WorkerAutoAssignmentManager.FindForTeam(playerTeam);
            }
        }

        public bool WorkerAutoAssignmentEnabled =>
            workerAutoAssignmentManager != null && workerAutoAssignmentManager.AutomaticAssignmentEnabled;

        public int WorkerAutoAssignmentCount =>
            workerAutoAssignmentManager != null ? workerAutoAssignmentManager.AssignedWorkerCount : 0;

        public bool ToggleWorkerAutoAssignment()
        {
            ResolveWorkerAutoAssignmentManager();
            if (workerAutoAssignmentManager == null)
            {
                return false;
            }

            workerAutoAssignmentManager.ToggleAutomaticAssignment();
            return workerAutoAssignmentManager.AutomaticAssignmentEnabled;
        }

        public bool TryActivateSelectedSkill(int skillIndex)
        {
            if (commandController == null)
            {
                commandController = PlayerUnitCommandController.ActiveInstance;
            }

            if (commandController == null || IsMatchOver())
            {
                skillFeedback = "Active skills are unavailable.";
                return false;
            }

            var selectedUnits = commandController.SelectedUnits;
            var eligibleCount = 0;
            var activatedCount = 0;
            var failedCount = 0;
            var skillName = "Active skill";
            var firstFailureReason = string.Empty;
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                var unit = selectedUnits[i];
                var controller = unit != null ? unit.GetComponent<UnitActiveSkillController>() : null;
                if (controller == null || skillIndex < 0 || skillIndex >= controller.Skills.Count)
                {
                    continue;
                }

                eligibleCount++;
                var definition = controller.Skills[skillIndex];
                if (definition != null)
                {
                    skillName = definition.DisplayName;
                }

                if (controller.TryActivate(skillIndex))
                {
                    activatedCount++;
                }
                else
                {
                    failedCount++;
                    if (string.IsNullOrWhiteSpace(firstFailureReason))
                    {
                        firstFailureReason = controller.LastFailureReason;
                    }
                }
            }

            if (eligibleCount == 0)
            {
                skillFeedback = "No selected unit has an active skill in this slot.";
                return false;
            }

            if (activatedCount == 0)
            {
                skillFeedback = firstFailureReason;
                return false;
            }

            skillFeedback = failedCount > 0
                ? $"Activated {skillName}: {activatedCount} unit(s), {failedCount} unavailable."
                : $"Activated {skillName}: {activatedCount} unit(s).";
            return true;
        }

        private void DrawBuildMenu(Rect panelRect)
        {
            GUI.Box(panelRect, string.Empty);
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 6f, panelRect.width - 16f, 20f), "Build structure");
            var buttonGap = 6f;
            var columns = panelRect.width >= 560f ? 5 : panelRect.width >= 420f ? 3 : 2;
            var buttonWidth = (panelRect.width - 16f - buttonGap * (columns - 1)) / columns;
            var buttonHeight = columns == 4 ? 46f : 28f;
            var optionCount = GetBuildOptionCount();
            for (var i = 0; i < optionCount; i++)
            {
                DrawBuildOption(
                    BuildOptionRect(panelRect, i, columns, buttonWidth, buttonHeight, buttonGap),
                    i,
                    GetBuildDefinition(i));
            }

            var rowCount = Mathf.CeilToInt(optionCount / (float)columns);
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 30f + rowCount * (buttonHeight + 4f), panelRect.width - 16f, 20f), "Choose a tile to place. Esc cancels.");
            if (!string.IsNullOrWhiteSpace(productionFeedback))
            {
                GUI.Label(
                    new Rect(panelRect.x + 8f, panelRect.yMax - 38f, panelRect.width - 16f, 34f),
                    ShortenFailureReason(productionFeedback));
            }
        }

        private static Rect BuildOptionRect(Rect panelRect, int index, int columns, float width, float height, float gap)
        {
            var column = index % columns;
            var row = index / columns;
            return new Rect(panelRect.x + 8f + column * (width + gap), panelRect.y + 28f + row * (height + 4f), width, height);
        }

        private void DrawBuildOption(
            Rect buttonRect,
            int hotkeyIndex,
            BuildingConstructionDefinition definition)
        {
            var lockReason = "No building placement service is available.";
            var isLocked = buildingPlacementService == null
                || !buildingPlacementService.CanSelectConstructionDefinition(definition, out lockReason);
            var buttonLabel = isLocked ? "LOCKED" : FormatBuildOptionLabel(definition);
            if (!GUI.Button(buttonRect, WithHotkeyLabel(buttonLabel, hotkeyIndex)))
            {
                return;
            }

            if (isLocked)
            {
                productionFeedback = lockReason;
                return;
            }

            TryBeginBuildPlacement(hotkeyIndex);
        }

        private bool TryBeginBuildPlacement(int hotkeyIndex)
        {
            var definition = GetBuildDefinition(hotkeyIndex);
            if (definition == null)
            {
                return false;
            }

            if (!commandController.CanUseBuildCommands)
            {
                productionFeedback = "Select exactly one friendly worker before placing.";
                commandController.CloseBuildMenu();
                return false;
            }

            if (buildingPlacementService == null
                || !buildingPlacementService.SelectConstructionDefinition(definition))
            {
                productionFeedback = buildingPlacementService != null
                    ? buildingPlacementService.LastPlacementFailureReason
                    : "No building placement service is available.";
                return false;
            }

            commandController.BeginBuildPlacement(buildingPlacementService);
            return true;
        }

        private int GetBuildOptionCount()
        {
            var definitions = buildingPlacementService != null
                ? buildingPlacementService.ConstructionDefinitions
                : null;
            if (definitions == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private BuildingConstructionDefinition GetBuildDefinition(int optionIndex)
        {
            if (optionIndex < 0 || buildingPlacementService == null)
            {
                return null;
            }

            var definitions = buildingPlacementService.ConstructionDefinitions;
            var currentIndex = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                if (currentIndex == optionIndex)
                {
                    return definition;
                }

                currentIndex++;
            }

            return null;
        }

        private static string FormatBuildOptionLabel(BuildingConstructionDefinition definition)
        {
            return definition != null
                ? $"{definition.DisplayName} {FormatCost(definition.Cost)}"
                : string.Empty;
        }

        public bool TryQueueProduction(UnitProductionQueue productionQueue, int hotkeyIndex)
        {
            if (productionQueue == null || hotkeyIndex < 0 || hotkeyIndex >= ContextHotkeys.Length)
            {
                return false;
            }

            var definitions = productionQueue.ProducibleUnits;
            if (hotkeyIndex >= definitions.Count || definitions[hotkeyIndex] == null)
            {
                return false;
            }

            var definition = definitions[hotkeyIndex];
            if (productionQueue.TryEnqueue(hotkeyIndex))
            {
                productionFeedback = $"Queued {definition.DisplayName}.";
                return true;
            }

            productionFeedback = productionQueue.LastEnqueueFailureReason;
            return false;
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
            var hotkeyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), GetCommandHotkeyLabel(fallbackLabel), hotkeyStyle);
            return clicked;
        }

        private static Rect CommandButtonRect(Rect panelRect, int index)
        {
            var availableWidth = panelRect.width - 16f - CommandButtonGap * 5f;
            var buttonSize = Mathf.Min(CommandButtonMaxSize, availableWidth / 6f);
            return new Rect(
                panelRect.x + 8f + index * (buttonSize + CommandButtonGap),
                panelRect.y + 8f,
                buttonSize,
                buttonSize);
        }

        private static string FormatCommandMode(UnitCommandMode mode)
        {
            switch (mode)
            {
                case UnitCommandMode.AttackMove:
                    return "Attack Move";
                case UnitCommandMode.FocusAttack:
                    return "Focus Attack";
                case UnitCommandMode.HoldPosition:
                    return "Hold";
                default:
                    return mode.ToString();
            }
        }

        private static string FormatActionState(UnitActionState state)
        {
            switch (state)
            {
                case UnitActionState.AttackMoving:
                    return "Advancing";
                case UnitActionState.ChasingTarget:
                    return "Chasing";
                case UnitActionState.AttackingTarget:
                    return "Attacking";
                case UnitActionState.HoldingPosition:
                    return "Holding";
                case UnitActionState.RetreatingFromTarget:
                    return "Repositioning";
                default:
                    return state.ToString();
            }
        }

        private void DrawPendingCommandStatus(Rect panelRect)
        {
            var message = commandController.PendingCommandStatusMessage;
            if (!string.IsNullOrWhiteSpace(message))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 62f, panelRect.width - 16f, 42f), ShortenFailureReason(message));
            }

            var groupHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.LowerLeft
            };
            GUI.Label(
                new Rect(panelRect.x + 8f, panelRect.y + 90f, panelRect.width - 16f, 18f),
                "Groups: 1-0 select | Ctrl set | Shift add",
                groupHintStyle);
        }

        private void DrawProductionPanel(Rect panelRect)
        {
            var productionQueue = ResolveDisplayedProductionQueue();
            if (productionQueue == null)
            {
                productionFeedback = string.Empty;
                selectedPendingProductionIndex = 0;
                showUpgradeResearch = false;
                return;
            }

            var research = displayedUpgradeResearch ?? TeamUpgradeResearch.FindForTeam(playerTeam);
            if (showUpgradeResearch && research != null)
            {
                DrawUpgradeResearchPanel(panelRect, research);
                return;
            }

            GUI.Box(panelRect, string.Empty);
            var active = productionQueue.ActiveProduction;
            var activeLabel = active != null ? $"{active.DisplayName} {productionQueue.ActiveProgress01 * 100f:0}%" : "Production idle";
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 6f, panelRect.width - 252f, 20f), activeLabel);
            if (research != null && GUI.Button(new Rect(panelRect.x + panelRect.width - 236f, panelRect.y + 4f, 72f, 24f), "Research"))
            {
                showUpgradeResearch = true;
                upgradeFeedback = string.Empty;
            }
            if (GUI.Button(new Rect(panelRect.x + panelRect.width - 158f, panelRect.y + 4f, 72f, 24f), "Rally"))
            {
                commandController.BeginRallyPointCommand(productionQueue);
                productionFeedback = "Click the map to set rally point.";
            }
            if (active != null && GUI.Button(new Rect(panelRect.x + panelRect.width - 80f, panelRect.y + 4f, 72f, 24f), "Cancel"))
            {
                productionFeedback = productionQueue.TryCancelActiveProduction()
                    ? $"Cancelled {active.DisplayName}."
                    : productionQueue.LastCancellationFailureReason;
            }

            if (active != null)
            {
                var barRect = new Rect(panelRect.x + 8f, panelRect.y + 28f, panelRect.width - 16f, 8f);
                GUI.Box(barRect, string.Empty);
                DrawFilledRect(new Rect(barRect.x + 1f, barRect.y + 1f, (barRect.width - 2f) * productionQueue.ActiveProgress01, barRect.height - 2f), new Color(0.35f, 0.78f, 0.42f, 0.9f));
            }

            var definitions = productionQueue.ProducibleUnits;
            var buttonSize = CommandButtonMaxSize;
            var buttonGap = CommandButtonGap;
            var visibleButtonCount = Mathf.Min(
                definitions.Count,
                Mathf.FloorToInt((panelRect.width - 16f + buttonGap) / (buttonSize + buttonGap)));
            for (var i = 0; i < visibleButtonCount; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var x = panelRect.x + 8f + i * (buttonSize + buttonGap);
                var y = panelRect.y + 42f;
                var isLocked = !definition.CanMeetAdditionalProductionConditions(
                    productionQueue.Team,
                    "produce",
                    out var lockReason);
                var buttonLabel = isLocked
                    ? WithHotkeyLabel($"LOCKED\n{definition.DisplayName}", i)
                    : WithHotkeyLabel($"{definition.DisplayName}\n{FormatCost(definition.Cost)}", i);
                if (GUI.Button(new Rect(x, y, buttonSize, buttonSize), buttonLabel))
                {
                    if (isLocked)
                    {
                        productionFeedback = lockReason;
                    }
                    else
                    {
                        TryQueueProduction(productionQueue, i);
                    }
                }

            }

            DrawPendingProductionCancelControls(panelRect, productionQueue);

            var feedback = !string.IsNullOrWhiteSpace(productionQueue.LastEnqueueFailureReason)
                ? productionQueue.LastEnqueueFailureReason
                : !string.IsNullOrWhiteSpace(productionQueue.LastCancellationFailureReason)
                    ? productionQueue.LastCancellationFailureReason
                : productionFeedback;
            if (productionQueue.PendingCount <= 0 && !string.IsNullOrWhiteSpace(feedback))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 92f, panelRect.width - 16f, 20f), ShortenFailureReason(feedback));
            }
        }

        private void DrawActiveSkillPanel(Rect panelRect)
        {
            var controller = ResolveDisplayedSkillController();
            if (controller == null || controller.Skills.Count == 0)
            {
                return;
            }

            GUI.Box(panelRect, string.Empty);
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 6f, panelRect.width - 16f, 20f), "Active skill");
            var definition = controller.Skills[0];
            if (definition == null)
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 32f, panelRect.width - 16f, 20f), "Skill unavailable");
                return;
            }

            var cooldownRemaining = controller.GetCooldownRemaining(0);
            var activeRemaining = controller.GetActiveDurationRemaining(0);
            var stateLabel = activeRemaining > 0f
                ? $"Active {activeRemaining:0.0}s | Cooldown {cooldownRemaining:0.0}s"
                : cooldownRemaining > 0f
                    ? $"Cooldown {cooldownRemaining:0.0}s"
                    : "Ready";
            if (GUI.Button(
                    new Rect(panelRect.x + 8f, panelRect.y + 30f, 132f, 46f),
                    $"{definition.DisplayName} [F]\n{stateLabel}"))
            {
                TryActivateSelectedSkill(0);
            }

            if (!string.IsNullOrWhiteSpace(skillFeedback))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 82f, panelRect.width - 16f, 28f), skillFeedback);
            }
        }

        private void DrawUpgradeResearchPanel(Rect panelRect, TeamUpgradeResearch research)
        {
            GUI.Box(panelRect, string.Empty);
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 6f, panelRect.width - 96f, 20f), "Unit research");
            if (GUI.Button(new Rect(panelRect.x + panelRect.width - 80f, panelRect.y + 4f, 72f, 24f), "Back"))
            {
                showUpgradeResearch = false;
                return;
            }

            var active = research.ActiveDefinition;
            var activeLabel = active != null
                ? $"Researching: {active.DisplayName} {research.ActiveProgress01 * 100f:0}%"
                : "Research idle";
            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 28f, panelRect.width - 16f, 18f), activeLabel);
            if (active != null)
            {
                var barRect = new Rect(panelRect.x + 8f, panelRect.y + 46f, panelRect.width - 16f, 6f);
                GUI.Box(barRect, string.Empty);
                DrawFilledRect(new Rect(barRect.x + 1f, barRect.y + 1f, (barRect.width - 2f) * research.ActiveProgress01, barRect.height - 2f), new Color(0.9f, 0.66f, 0.25f, 0.9f));
            }

            var definitions = research.Definitions;
            for (var i = 0; i < definitions.Count && i < 2; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var status = research.GetStatus(definition);
                var label = status == UnitUpgradeResearchStatus.Completed
                    ? $"{definition.DisplayName}\nCompleted"
                    : status == UnitUpgradeResearchStatus.Researching
                        ? $"{definition.DisplayName}\nResearching"
                        : $"{definition.DisplayName}\n{FormatCost(definition.Cost)}  {definition.ResearchDuration:0}s";
                var buttonWidth = (panelRect.width - 22f) * 0.5f;
                var buttonRect = new Rect(panelRect.x + 8f + i * buttonWidth, panelRect.y + 58f, buttonWidth, 38f);
                var wasEnabled = GUI.enabled;
                GUI.enabled = status == UnitUpgradeResearchStatus.Available && active == null;
                if (GUI.Button(buttonRect, label))
                {
                    upgradeFeedback = research.TryStartResearch(i)
                        ? $"Started {definition.DisplayName}."
                        : research.LastFailureReason;
                }

                GUI.enabled = wasEnabled;
            }

            var feedback = string.IsNullOrWhiteSpace(research.LastFailureReason)
                ? upgradeFeedback
                : research.LastFailureReason;
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 98f, panelRect.width - 16f, 18f), ShortenFailureReason(feedback));
            }
        }

        private UnitProductionQueue ResolveDisplayedProductionQueue()
        {
            if (commandController != null)
            {
                var selection = commandController.PrimarySelection;
                var selectionObject = selection != null ? selection.SelectionGameObject : null;
                displayedProductionQueue = selectionObject != null
                    ? selectionObject.GetComponent<UnitProductionQueue>()
                    : null;
            }

            if (displayedProductionQueue != null && !displayedProductionQueue.isActiveAndEnabled)
            {
                displayedProductionQueue = null;
            }

            return displayedProductionQueue;
        }

        private UnitActiveSkillController ResolveDisplayedSkillController()
        {
            if (commandController == null)
            {
                return null;
            }

            var selection = commandController.PrimarySelection;
            var selectionObject = selection != null ? selection.SelectionGameObject : null;
            var controller = selectionObject != null
                ? selectionObject.GetComponent<UnitActiveSkillController>()
                : null;
            if (IsUsableSkillController(controller))
            {
                return controller;
            }

            var selectedUnits = commandController.SelectedUnits;
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                controller = selectedUnits[i] != null
                    ? selectedUnits[i].GetComponent<UnitActiveSkillController>()
                    : null;
                if (IsUsableSkillController(controller))
                {
                    return controller;
                }
            }

            return null;
        }

        private static bool IsUsableSkillController(UnitActiveSkillController controller)
        {
            return controller != null && controller.isActiveAndEnabled && controller.Skills.Count > 0;
        }

        private void DrawPendingProductionCancelControls(Rect panelRect, UnitProductionQueue productionQueue)
        {
            if (productionQueue.PendingCount <= 0)
            {
                selectedPendingProductionIndex = 0;
                return;
            }

            selectedPendingProductionIndex = Mathf.Clamp(
                selectedPendingProductionIndex,
                0,
                productionQueue.PendingCount - 1);

            GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 88f, 64f, 22f), "Pending");
            for (var i = 0; i < productionQueue.PendingCount && i < 3; i++)
            {
                var pending = productionQueue.GetPendingProduction(i);
                var label = pending != null ? $"{i + 1}" : "-";
                if (GUI.Toggle(
                    new Rect(panelRect.x + 72f + i * 30f, panelRect.y + 88f, 26f, 22f),
                    selectedPendingProductionIndex == i,
                    label,
                    GUI.skin.button))
                {
                    selectedPendingProductionIndex = i;
                }
            }

            var selectedPending = productionQueue.GetPendingProduction(selectedPendingProductionIndex);
            var cancelLabel = selectedPending != null ? "Cancel Pending" : "Cancel";
            if (GUI.Button(new Rect(panelRect.x + 170f, panelRect.y + 88f, 116f, 22f), cancelLabel))
            {
                var pendingName = selectedPending != null ? selectedPending.DisplayName : "pending production";
                productionFeedback = productionQueue.TryCancelPendingProduction(selectedPendingProductionIndex)
                    ? $"Cancelled {pendingName}."
                    : productionQueue.LastCancellationFailureReason;
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
            var height = 188f;
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

            var action = matchController.RequestedPostMatchAction;
            var canChooseAction = action == ProjectS.RtsMatchPostAction.None;
            var previousEnabled = GUI.enabled;
            GUI.enabled = canChooseAction;
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 116f, 96f, 34f), "Rematch"))
            {
                matchController.TryRequestRematch();
            }

            if (GUI.Button(new Rect(rect.x + 132f, rect.y + 116f, 96f, 34f), "Main Menu"))
            {
                matchController.TryRequestMainMenu();
            }

            if (GUI.Button(new Rect(rect.x + 244f, rect.y + 116f, 96f, 34f), "End Match"))
            {
                matchController.TryRequestEndMatch();
            }

            GUI.enabled = previousEnabled;
            if (!canChooseAction)
            {
                GUI.Label(
                    new Rect(rect.x + 16f, rect.y + 154f, rect.width - 32f, 18f),
                    FormatPostMatchAction(action),
                    reasonStyle);
            }
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
                case ProjectS.RtsMatchEndReason.EnemyBuildingsDestroyed:
                    return "Enemy buildings destroyed";
                case ProjectS.RtsMatchEndReason.PlayerMainBaseDestroyed:
                    return "Player main base destroyed";
                case ProjectS.RtsMatchEndReason.PlayerBuildingsDestroyed:
                    return "Player buildings destroyed";
                default:
                    return string.Empty;
            }
        }

        private static string FormatPostMatchAction(ProjectS.RtsMatchPostAction action)
        {
            switch (action)
            {
                case ProjectS.RtsMatchPostAction.Rematch:
                    return "Restarting match...";
                case ProjectS.RtsMatchPostAction.MainMenu:
                    return "Returning to main menu...";
                default:
                    return "Ending match...";
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

        private static string WithHotkeyLabel(string label, int hotkeyIndex)
        {
            return hotkeyIndex >= 0 && hotkeyIndex < ContextHotkeys.Length
                ? $"{label} [{ContextHotkeys[hotkeyIndex]}]"
                : label;
        }

        private static string GetCommandHotkeyLabel(string fallbackLabel)
        {
            var separator = fallbackLabel.LastIndexOf(' ');
            return separator >= 0 && separator < fallbackLabel.Length - 1
                ? fallbackLabel.Substring(separator + 1)
                : string.Empty;
        }

        private static bool IsContextHotkeyModifierPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftCtrlKey.isPressed
                    || keyboard.rightCtrlKey.isPressed
                    || keyboard.leftShiftKey.isPressed
                    || keyboard.rightShiftKey.isPressed
                    || keyboard.leftAltKey.isPressed
                    || keyboard.rightAltKey.isPressed);
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

            if (normalizedReason.Contains("supply"))
            {
                return "Need supply";
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

        private readonly struct BottomHudLayout
        {
            public readonly Rect selectionRect;
            public readonly Rect commandRect;
            public readonly Rect contextRect;

            public BottomHudLayout(Rect selectionRect, Rect commandRect, Rect contextRect)
            {
                this.selectionRect = selectionRect;
                this.commandRect = commandRect;
                this.contextRect = contextRect;
            }
        }
    }
}
