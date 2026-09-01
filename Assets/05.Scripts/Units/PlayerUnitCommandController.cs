using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectS.Units
{
    public sealed class PlayerUnitCommandController : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private Camera commandCamera;
        [SerializeField] private ProjectSTilemapNavigator navigator;
        [SerializeField] private LayerMask unitSelectionMask = ~0;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private bool logCommandWarnings = true;
        [SerializeField] private float commandPlaneZ;
        [SerializeField] private float clickSelectionRadius = 0.35f;
        [SerializeField] private float dragSelectThreshold = 8f;
        [SerializeField] private Color dragFillColor = new Color(0.2f, 0.6f, 1f, 0.18f);
        [SerializeField] private Color dragOutlineColor = new Color(0.2f, 0.65f, 1f, 0.85f);
        [SerializeField] private Color validPlacementColor = new Color(0.1f, 0.85f, 0.25f, 0.35f);
        [SerializeField] private Color invalidPlacementColor = new Color(1f, 0.16f, 0.1f, 0.35f);
        [SerializeField] private Color pendingCommandCursorColor = new Color(1f, 0.92f, 0.2f, 0.8f);
        [SerializeField] private bool logInteractionCommands = true;

        private readonly List<UnitCommandAgent> selectedUnits = new List<UnitCommandAgent>();
        private readonly List<UnitCommandAgent>[] controlGroups = new List<UnitCommandAgent>[10];
        private readonly Collider2D[] unitHitBuffer = new Collider2D[16];
        private readonly Collider2D[] interactableHitBuffer = new Collider2D[16];
        private readonly HashSet<Vector3Int> reservedCommandCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> occupiedCommandCells = new HashSet<Vector3Int>();
        private readonly HashSet<IUnitAttackTarget> highlightedTargets = new HashSet<IUnitAttackTarget>();
        private PendingPointCommand pendingPointCommand = PendingPointCommand.None;
        private Vector2 dragStartScreenPosition;
        private Vector2 dragCurrentScreenPosition;
        private IUnitBuildPlacementService pendingBuildPlacementService;
        private IUnitBuildPlacementService defaultBuildPlacementService;
        private IUnitRallyPointService pendingRallyQueue;
        private string buildPlacementFeedback;
        private bool isBuildMenuOpen;
        private bool isLeftMousePressed;
        private bool isDraggingSelection;
        private bool warnedMissingCamera;
        private bool warnedNoSelectedUnits;
        private static Sprite fallbackRingSprite;

        private const float ResourcePanelX = 12f;
        private const float ResourcePanelTopY = 12f;
        private const float ResourcePanelWidth = 260f;
        private const float ResourcePanelHeight = 64f;
        private const float SelectionPanelX = 12f;
        private const float SelectionPanelBottomMargin = 12f;
        private const float SelectionPanelWidth = 360f;
        private const float SelectionPanelHeight = 220f;
        private const float CommandPanelRightMargin = 12f;
        private const float CommandPanelBottomMargin = 12f;
        private const float CommandPanelWidth = 420f;
        private const float CommandPanelHeight = 400f;
        private const float PathStatsPanelRightMargin = 12f;
        private const float PathStatsPanelTopY = 12f;
        private const float PathStatsPanelWidth = 280f;
        private const float PathStatsPanelHeight = 92f;
        private const string FriendlyBuilderRequiredMessage = "Select a friendly builder before placing.";

        public static PlayerUnitCommandController ActiveInstance { get; private set; }
        public UnitTeam PlayerTeam => playerTeam;
        public IReadOnlyList<UnitCommandAgent> SelectedUnits => selectedUnits;
        public IPlayerSelectableTarget PrimarySelection { get; private set; }
        public bool IsBuildPlacementPending => pendingBuildPlacementService != null;
        public bool IsBuildMenuOpen => isBuildMenuOpen;
        public string BuildPlacementStatusMessage
        {
            get
            {
                if (pendingBuildPlacementService == null)
                {
                    return buildPlacementFeedback;
                }

                if (!HasSelectedFriendlyBuilder())
                {
                    return FriendlyBuilderRequiredMessage;
                }

                if (!TryGetBuildPlacementPoint(out var destination))
                {
                    return "Move cursor over the map to place.";
                }

                return pendingBuildPlacementService.CanPlaceDefaultConstructionSite(destination)
                    ? "Left-click to place. Right-click or Esc to cancel."
                    : pendingBuildPlacementService.LastPlacementFailureReason;
            }
        }

        private enum PendingPointCommand
        {
            None,
            Move,
            AttackMove,
            Patrol
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeController()
        {
            if (FindFirstObjectByType<PlayerUnitCommandController>() != null)
            {
                return;
            }

            var controller = new GameObject("PlayerUnitCommandController");
            controller.AddComponent<PlayerUnitCommandController>();
        }

        private void Awake()
        {
            ActiveInstance = this;
            for (var i = 0; i < controlGroups.Length; i++)
            {
                controlGroups[i] = new List<UnitCommandAgent>();
            }

            ResolveSceneReferences(true);
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
            ResolveSceneReferences();

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                BeginLeftMouseInteraction(mouse.position.ReadValue());
            }

            if (mouse.leftButton.isPressed)
            {
                UpdateLeftMouseInteraction(mouse.position.ReadValue());
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndLeftMouseInteraction(mouse.position.ReadValue());
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                HandleRightClick();
            }

            HandleKeyboardCommands();
            RefreshTargetHighlights();
        }

        private void OnGUI()
        {
            if (isDraggingSelection)
            {
                var selectionRect = GetGuiSelectionRect(dragStartScreenPosition, dragCurrentScreenPosition);
                DrawFilledRect(selectionRect, dragFillColor);
                DrawRectOutline(selectionRect, dragOutlineColor);
            }

            DrawPendingCommandPreview();
        }

        private void ResolveSceneReferences(bool allowSceneSearch = false)
        {
            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }

            if (navigator == null)
            {
                navigator = ProjectSTilemapNavigator.ActiveInstance;
            }

            if (navigator == null && allowSceneSearch)
            {
                navigator = FindFirstObjectByType<ProjectSTilemapNavigator>();
            }
        }

        private void BeginLeftMouseInteraction(Vector2 screenPosition)
        {
            if (IsPointerOverRuntimeHud(screenPosition))
            {
                isLeftMousePressed = false;
                isDraggingSelection = false;
                return;
            }

            isLeftMousePressed = true;
            isDraggingSelection = false;
            dragStartScreenPosition = screenPosition;
            dragCurrentScreenPosition = screenPosition;
        }

        private void UpdateLeftMouseInteraction(Vector2 screenPosition)
        {
            if (!isLeftMousePressed)
            {
                return;
            }

            dragCurrentScreenPosition = screenPosition;
            if (pendingPointCommand == PendingPointCommand.None
                && pendingBuildPlacementService == null
                && Vector2.Distance(dragStartScreenPosition, dragCurrentScreenPosition) >= dragSelectThreshold)
            {
                isDraggingSelection = true;
            }
        }

        private void EndLeftMouseInteraction(Vector2 screenPosition)
        {
            if (!isLeftMousePressed)
            {
                return;
            }

            dragCurrentScreenPosition = screenPosition;
            isLeftMousePressed = false;

            if (IsPointerOverRuntimeHud(screenPosition))
            {
                isDraggingSelection = false;
                return;
            }

            if (isDraggingSelection)
            {
                SelectUnitsInDragRect();
                isDraggingSelection = false;
                return;
            }

            HandleLeftClick();
        }

        private void HandleLeftClick()
        {
            if (pendingPointCommand != PendingPointCommand.None)
            {
                CommandPendingPointClick(pendingPointCommand);
                pendingPointCommand = PendingPointCommand.None;
                return;
            }

            if (pendingRallyQueue != null)
            {
                CommandRallyPoint();
                return;
            }

            if (pendingBuildPlacementService != null)
            {
                CommandBuildPlacement();
                return;
            }

            SelectUnitUnderCursor();
        }

        private void HandleRightClick()
        {
            if (IsPointerOverRuntimeHud(GetMousePosition()))
            {
                return;
            }

            if (pendingPointCommand != PendingPointCommand.None)
            {
                CommandPendingPointClick(pendingPointCommand);
                pendingPointCommand = PendingPointCommand.None;
                return;
            }

            if (pendingRallyQueue != null)
            {
                CommandRallyPoint();
                return;
            }

            if (pendingBuildPlacementService != null)
            {
                CancelBuildPlacement();
                return;
            }

            pendingPointCommand = PendingPointCommand.None;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;

            if (TryGetAttackTargetUnderCursor(out var target) && target.Team != playerTeam)
            {
                CommandFocusAttack(target);
                return;
            }

            if (TryGetInteractableUnderCursor(out var interactable))
            {
                CommandInteract(interactable);
                return;
            }

            CommandPointFromCursor(PendingPointCommand.None);
        }

        private void SelectUnitUnderCursor()
        {
            if (commandCamera == null)
            {
                WarnOnce(ref warnedMissingCamera, "Player unit selection ignored because no command camera was found.");
                return;
            }

            if (!TryGetUnitUnderCursor(out var status))
            {
                if (TryGetSelectableUnderCursor(out var selectable) && selectable.Team == playerTeam)
                {
                    SelectNonUnitTarget(selectable);
                }
                else if (!IsAdditiveSelectionPressed())
                {
                    ClearSelection();
                }

                return;
            }

            if (status == null || status.Team != playerTeam || status.MovementDomain != MovementDomain.Ground)
            {
                return;
            }

            var agent = status.GetComponent<UnitCommandAgent>();
            if (agent == null)
            {
                return;
            }

            if (!IsAdditiveSelectionPressed())
            {
                ClearSelection();
            }

            AddSelection(agent);
        }

        private void SelectNonUnitTarget(IPlayerSelectableTarget target)
        {
            ClearSelection();
            PrimarySelection = target;
        }

        private void SelectUnitsInDragRect()
        {
            if (commandCamera == null)
            {
                WarnOnce(ref warnedMissingCamera, "Player drag selection ignored because no command camera was found.");
                return;
            }

            if (!IsAdditiveSelectionPressed())
            {
                ClearSelection();
            }

            var screenRect = GetScreenSelectionRect(dragStartScreenPosition, dragCurrentScreenPosition);
            var units = UnitRegistry.AllAgents;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !TryGetSelectableStatus(unit, out var status))
                {
                    continue;
                }

                var screenPosition = commandCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPosition.z < 0f)
                {
                    continue;
                }

                if (screenRect.Contains(new Vector2(screenPosition.x, screenPosition.y)))
                {
                    AddSelection(unit);
                }
            }
        }

        private void CommandPointFromCursor(PendingPointCommand pointCommand)
        {
            if (commandCamera == null || selectedUnits.Count == 0)
            {
                WarnCommandBlockedReason();
                return;
            }

            if (!TryGetCommandPoint(out var destination))
            {
                Debug.Log("Player move command ignored because the clicked point did not hit the command plane.", this);
                return;
            }

            var commandMode = pointCommand == PendingPointCommand.AttackMove
                ? UnitCommandMode.AttackMove
                : pointCommand == PendingPointCommand.Patrol
                    ? UnitCommandMode.Patrol
                    : UnitCommandMode.Move;

            IssueToSelected(new UnitCommand(commandMode, destination, null, false));
        }

        private void CommandPendingPointClick(PendingPointCommand pointCommand)
        {
            if (pointCommand == PendingPointCommand.AttackMove
                && TryGetAttackTargetUnderCursor(out var target)
                && target.Team != playerTeam)
            {
                CommandFocusAttack(target);
                return;
            }

            CommandPointFromCursor(pointCommand);
        }

        private void CommandFocusAttack(IUnitAttackTarget target)
        {
            if (selectedUnits.Count == 0)
            {
                WarnCommandBlockedReason();
                return;
            }

            IssueToSelected(new UnitCommand(UnitCommandMode.FocusAttack, target.SelectionTransform.position, target, false));
            SetTargetVisible(target, true);
        }

        private void CommandInteract(IUnitInteractableTarget target)
        {
            if (selectedUnits.Count == 0)
            {
                WarnCommandBlockedReason();
                return;
            }

            if (logInteractionCommands)
            {
                Debug.Log(
                    $"Right-click interaction command issued to {selectedUnits.Count} selected unit(s): {FormatInteractableTargetName(target)}.",
                    this);
            }

            IssueToSelected(new UnitCommand(UnitCommandMode.Interact, target.InteractionPoint, null, target, false));
        }

        private void CommandBuildPlacement()
        {
            if (pendingBuildPlacementService == null)
            {
                return;
            }

            if (!HasSelectedFriendlyBuilder())
            {
                buildPlacementFeedback = FriendlyBuilderRequiredMessage;
                WarnPlacementFailure();
                return;
            }

            if (!TryGetBuildPlacementPoint(out var destination))
            {
                return;
            }

            if (pendingBuildPlacementService.TryPlaceDefaultConstructionSite(destination, out var constructionSite)
                && constructionSite != null)
            {
                IssueToSelected(new UnitCommand(
                    UnitCommandMode.Interact,
                    constructionSite.InteractionPoint,
                    null,
                    constructionSite,
                    false));
                pendingBuildPlacementService = null;
                buildPlacementFeedback = string.Empty;
                return;
            }

            WarnPlacementFailure();
        }

        public void CancelBuildPlacement()
        {
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
        }

        private void WarnPlacementFailure()
        {
            if (!logCommandWarnings || pendingBuildPlacementService == null)
            {
                return;
            }

            var reason = pendingBuildPlacementService.LastPlacementFailureReason;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = BuildPlacementStatusMessage;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Building placement failed.";
            }

            Debug.LogWarning(reason, this);
        }

        private void CommandRallyPoint()
        {
            if (pendingRallyQueue == null)
            {
                return;
            }

            if (!TryGetCommandPoint(out var destination))
            {
                return;
            }

            pendingRallyQueue.SetRallyPoint(destination);
            pendingRallyQueue = null;
            pendingPointCommand = PendingPointCommand.None;
            pendingBuildPlacementService = null;
        }

        public void BeginMoveCommand()
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.Move;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
        }

        public void BeginAttackMoveCommand()
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.AttackMove;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
        }

        public void BeginPatrolCommand()
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.Patrol;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
        }

        public void BeginBuildPlacement(IUnitBuildPlacementService placementService)
        {
            pendingPointCommand = PendingPointCommand.None;
            pendingRallyQueue = null;
            if (placementService == null)
            {
                pendingBuildPlacementService = null;
                buildPlacementFeedback = "Building placement is unavailable.";
                return;
            }

            if (!HasSelectedFriendlyBuilder())
            {
                pendingBuildPlacementService = null;
                buildPlacementFeedback = FriendlyBuilderRequiredMessage;
                return;
            }

            pendingBuildPlacementService = placementService;
            buildPlacementFeedback = string.Empty;
        }

        public void SetDefaultBuildPlacementService(IUnitBuildPlacementService placementService)
        {
            defaultBuildPlacementService = placementService;
        }

        public void ToggleBuildMenu()
        {
            isBuildMenuOpen = !isBuildMenuOpen;
            if (!isBuildMenuOpen)
            {
                return;
            }

            pendingPointCommand = PendingPointCommand.None;
            CancelBuildPlacement();
        }

        public void CloseBuildMenu()
        {
            isBuildMenuOpen = false;
        }

        public void BeginRallyPointCommand(IUnitRallyPointService productionQueue)
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.None;
            pendingBuildPlacementService = null;
            pendingRallyQueue = productionQueue;
            buildPlacementFeedback = string.Empty;
        }

        public void StopSelectedUnits()
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.None;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
            foreach (var unit in selectedUnits)
            {
                unit?.Stop();
            }
        }

        public void HoldSelectedUnits()
        {
            CloseBuildMenu();
            pendingPointCommand = PendingPointCommand.None;
            pendingBuildPlacementService = null;
            pendingRallyQueue = null;
            buildPlacementFeedback = string.Empty;
            foreach (var unit in selectedUnits)
            {
                unit?.HoldPosition();
            }
        }

        private void IssueToSelected(UnitCommand command)
        {
            var shouldReserveDestinations = ShouldReserveCommandDestinations(command);
            var destinationIndex = 0;
            var destinationCount = CountLiveSelectedUnits();
            if (shouldReserveDestinations)
            {
                RebuildOccupiedCommandCells();
            }

            for (var i = selectedUnits.Count - 1; i >= 0; i--)
            {
                var unit = selectedUnits[i];
                if (unit == null)
                {
                    selectedUnits.RemoveAt(i);
                    continue;
                }

                if (shouldReserveDestinations)
                {
                    var status = unit.GetComponent<PrototypeUnitStatus>();
                    var footprint = status != null ? status.OccupiedCells : Vector2Int.one;
                    var offsetDestination = GetUniqueTileDestination(command.Destination, destinationIndex, destinationCount, footprint);
                    unit.Issue(new UnitCommand(command.Mode, offsetDestination, command.Target, command.InteractableTarget, false));
                    destinationIndex++;
                }
                else
                {
                    unit.Issue(command);
                }
            }

            if (shouldReserveDestinations)
            {
                reservedCommandCells.Clear();
                occupiedCommandCells.Clear();
            }
        }

        private static bool ShouldReserveCommandDestinations(UnitCommand command)
        {
            return command.Mode == UnitCommandMode.Move
                || command.Mode == UnitCommandMode.AttackMove
                || command.Mode == UnitCommandMode.Patrol
                || command.Mode == UnitCommandMode.FocusAttack
                || command.Mode == UnitCommandMode.Interact;
        }

        private int CountLiveSelectedUnits()
        {
            var count = 0;
            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void HandleKeyboardCommands()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                pendingPointCommand = PendingPointCommand.None;
                CancelBuildPlacement();
                return;
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                BeginMoveCommand();
            }

            if (keyboard.aKey.wasPressedThisFrame)
            {
                BeginAttackMoveCommand();
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                BeginPatrolCommand();
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                StopSelectedUnits();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                HoldSelectedUnits();
            }

            if (keyboard.bKey.wasPressedThisFrame)
            {
                ToggleBuildMenu();
            }

            HandleControlGroups(keyboard);
        }

        private void HandleControlGroups(Keyboard keyboard)
        {
            for (var i = 0; i <= 9; i++)
            {
                var key = GetDigitKey(keyboard, i);
                if (key == null || !key.wasPressedThisFrame)
                {
                    continue;
                }

                if (IsControlGroupAssignPressed())
                {
                    AssignControlGroup(i);
                }
                else if (IsAdditiveSelectionPressed())
                {
                    AddSelectionFromControlGroup(i);
                }
                else
                {
                    SelectControlGroup(i);
                }
            }
        }

        private void WarnCommandBlockedReason()
        {
            if (commandCamera == null)
            {
                WarnOnce(ref warnedMissingCamera, "Player move command ignored because no command camera was found.");
            }
            else if (selectedUnits.Count == 0)
            {
                WarnOnce(ref warnedNoSelectedUnits, "Player move command ignored because no player ground unit is selected.");
            }
        }

        private bool HasSelectedFriendlyBuilder()
        {
            for (var i = selectedUnits.Count - 1; i >= 0; i--)
            {
                var unit = selectedUnits[i];
                if (unit == null)
                {
                    selectedUnits.RemoveAt(i);
                    continue;
                }

                var status = unit.Status;
                if (status != null
                    && status.isActiveAndEnabled
                    && status.IsAlive
                    && status.Team == playerTeam
                    && status.Roles.HasFlag(UnitRole.Builder))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetCommandPoint(out Vector3 destination)
        {
            destination = Vector3.zero;
            if (!TryGetCursorWorldPoint(out destination))
            {
                return false;
            }

            if (navigator != null)
            {
                if (!navigator.TryGetCommandPoint(destination, out var tilemapDestination))
                {
                    return false;
                }

                destination = tilemapDestination;
            }

            return true;
        }

        private bool TryGetBuildPlacementPoint(out Vector3 destination)
        {
            destination = Vector3.zero;
            if (!TryGetCursorWorldPoint(out destination))
            {
                return false;
            }

            var tilemapWorld = navigator != null ? navigator.TilemapWorld : null;
            if (tilemapWorld == null)
            {
                return true;
            }

            var cell = tilemapWorld.WorldToCell(destination);
            if (!tilemapWorld.ContainsCell(cell))
            {
                return false;
            }

            destination = tilemapWorld.GetCellCenterWorld(cell);
            destination.z = commandPlaneZ;
            return true;
        }

        private bool TryGetCursorWorldPoint(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (commandCamera == null)
            {
                return false;
            }

            var ray = commandCamera.ScreenPointToRay(GetMousePosition());
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, commandPlaneZ));
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            worldPosition.z = commandPlaneZ;
            return true;
        }

        private void WarnOnce(ref bool warned, string message)
        {
            if (!logCommandWarnings || warned)
            {
                return;
            }

            Debug.LogWarning(message, this);
            warned = true;
        }

        private bool TryGetUnitUnderCursor(out PrototypeUnitStatus status)
        {
            status = null;
            if (commandCamera == null)
            {
                return false;
            }

            if (!TryGetCursorWorldPoint(out var worldPosition))
            {
                return false;
            }

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = unitSelectionMask,
                useTriggers = true
            };

            var hitCount = Physics2D.OverlapCircle(worldPosition, Mathf.Max(0.01f, clickSelectionRadius), filter, unitHitBuffer);
            if (hitCount <= 0)
            {
                return false;
            }

            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = unitHitBuffer[i].GetComponentInParent<PrototypeUnitStatus>();
                if (candidate == null)
                {
                    continue;
                }

                var distance = Vector2.SqrMagnitude((Vector2)candidate.transform.position - (Vector2)worldPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    status = candidate;
                }
            }

            return status != null;
        }

        private bool TryGetAttackTargetUnderCursor(out IUnitAttackTarget target)
        {
            target = null;
            if (commandCamera == null)
            {
                return false;
            }

            if (!TryGetCursorWorldPoint(out var worldPosition))
            {
                return false;
            }

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = unitSelectionMask,
                useTriggers = true
            };

            var hitCount = Physics2D.OverlapCircle(worldPosition, Mathf.Max(0.01f, clickSelectionRadius), filter, unitHitBuffer);
            if (hitCount <= 0)
            {
                return false;
            }

            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = unitHitBuffer[i].GetComponentInParent<IUnitAttackTarget>();
                if (candidate == null || !candidate.IsAlive || candidate.SelectionTransform == null)
                {
                    continue;
                }

                var distance = Vector2.SqrMagnitude(
                    (Vector2)candidate.SelectionTransform.position - (Vector2)worldPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = candidate;
                }
            }

            return target != null;
        }

        private bool TryGetInteractableUnderCursor(out IUnitInteractableTarget target)
        {
            target = null;
            if (!TryGetCursorWorldPoint(out var worldPosition))
            {
                return false;
            }

            return TryGetInteractableAtWorldPoint(worldPosition, out target);
        }

        public bool TryGetInteractableAtWorldPoint(Vector3 worldPosition, out IUnitInteractableTarget target)
        {
            target = null;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = interactableMask,
                useTriggers = true
            };

            var hitCount = Physics2D.OverlapCircle(
                worldPosition,
                Mathf.Max(0.01f, clickSelectionRadius),
                filter,
                interactableHitBuffer);
            if (hitCount <= 0)
            {
                return false;
            }

            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = interactableHitBuffer[i].GetComponentInParent<IUnitInteractableTarget>();
                if (candidate == null)
                {
                    continue;
                }

                var interactionPoint = candidate.InteractionPoint;
                var distance = Vector2.SqrMagnitude((Vector2)interactionPoint - (Vector2)worldPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = candidate;
                }
            }

            return target != null;
        }

        private bool TryGetSelectableUnderCursor(out IPlayerSelectableTarget target)
        {
            target = null;
            if (commandCamera == null)
            {
                return false;
            }

            if (!TryGetCursorWorldPoint(out var worldPosition))
            {
                return false;
            }

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = unitSelectionMask,
                useTriggers = true
            };

            var hitCount = Physics2D.OverlapCircle(
                worldPosition,
                Mathf.Max(0.01f, clickSelectionRadius),
                filter,
                unitHitBuffer);
            if (hitCount <= 0)
            {
                return false;
            }

            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var candidate = unitHitBuffer[i].GetComponentInParent<IPlayerSelectableTarget>();
                if (candidate == null || candidate is PrototypeUnitStatus)
                {
                    continue;
                }

                var distance = Vector2.SqrMagnitude(
                    (Vector2)candidate.SelectionTransform.position - (Vector2)worldPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = candidate;
                }
            }

            return target != null;
        }

        private bool TryGetSelectableStatus(UnitCommandAgent unit, out PrototypeUnitStatus status)
        {
            status = unit.GetComponent<PrototypeUnitStatus>();
            return status != null
                && status.Team == playerTeam
                && status.MovementDomain == MovementDomain.Ground;
        }

        private void AddSelection(UnitCommandAgent agent)
        {
            if (selectedUnits.Contains(agent))
            {
                return;
            }

            selectedUnits.Add(agent);
            PrimarySelection = agent.Status;
            SetSelectionVisible(agent, true);
        }

        private void ClearSelection()
        {
            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    SetSelectionVisible(unit, false);
                }
            }

            selectedUnits.Clear();
            PrimarySelection = null;
            RefreshTargetHighlights();
        }

        private void SelectControlGroup(int index)
        {
            ClearSelection();
            AddSelectionFromControlGroup(index);
        }

        private void AddSelectionFromControlGroup(int index)
        {
            foreach (var unit in controlGroups[index])
            {
                if (unit != null)
                {
                    AddSelection(unit);
                }
            }
        }

        private void AssignControlGroup(int index)
        {
            controlGroups[index].Clear();
            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    controlGroups[index].Add(unit);
                }
            }
        }

        private static void SetSelectionVisible(UnitCommandAgent agent, bool visible)
        {
            var ring = agent.transform.Find("SelectionRing");
            if (ring != null)
            {
                var renderer = ring.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(0.1f, 0.8f, 1f, 0.85f);
                }

                ring.gameObject.SetActive(visible);
            }
        }

        private void RefreshTargetHighlights()
        {
            foreach (var target in highlightedTargets)
            {
                SetTargetVisible(target, false);
            }

            highlightedTargets.Clear();
            foreach (var unit in selectedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                var target = unit.PriorityTarget;
                if (target == null
                    || target.Team == playerTeam
                    || target.SelectionGameObject == null
                    || !target.SelectionGameObject.activeInHierarchy)
                {
                    continue;
                }

                highlightedTargets.Add(target);
            }

            foreach (var target in highlightedTargets)
            {
                SetTargetVisible(target, true);
            }
        }

        private static void SetTargetVisible(IUnitAttackTarget target, bool visible)
        {
            if (target == null || target.SelectionTransform == null)
            {
                return;
            }

            var ring = target.SelectionTransform.Find("TargetRing");
            if (ring == null)
            {
                ring = CreateTargetRing(target.SelectionTransform);
            }

            ring.gameObject.SetActive(visible);
        }

        private static Transform CreateTargetRing(Transform parent)
        {
            var ringObject = new GameObject("TargetRing");
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            ringObject.transform.localScale = new Vector3(1.05f, 0.58f, 1f);

            var renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRingSprite(parent);
            renderer.color = new Color(1f, 0.12f, 0.08f, 0.9f);
            renderer.sortingOrder = 11;
            return ringObject.transform;
        }

        private static Sprite GetRingSprite(Transform unit)
        {
            var selectionRing = unit.Find("SelectionRing");
            var selectionRenderer = selectionRing != null ? selectionRing.GetComponent<SpriteRenderer>() : null;
            if (selectionRenderer != null && selectionRenderer.sprite != null)
            {
                return selectionRenderer.sprite;
            }

            if (fallbackRingSprite != null)
            {
                return fallbackRingSprite;
            }

            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "FallbackTargetRingSprite",
                filterMode = FilterMode.Point
            };
            var center = new Vector2(7.5f, 7.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var radius = (new Vector2(x, y) - center).magnitude;
                    texture.SetPixel(x, y, radius > 5f && radius < 7f ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            fallbackRingSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            fallbackRingSprite.name = "FallbackTargetRingSprite";
            return fallbackRingSprite;
        }

        private static Vector2 GetMousePosition()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }

        private static bool IsAdditiveSelectionPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private static bool IsControlGroupAssignPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private Vector3 GetUniqueTileDestination(Vector3 destination, int index, int count, Vector2Int footprint)
        {
            footprint = SanitizeFootprint(footprint);
            var tilemapWorld = navigator != null ? navigator.TilemapWorld : null;
            if (tilemapWorld == null)
            {
                return destination + GetFallbackFormationOffset(index, count);
            }

            var origin = tilemapWorld.WorldToCell(destination);
            if (TryReserveCommandFootprint(tilemapWorld, origin, footprint, out var assignedCell))
            {
                return tilemapWorld.GetCellCenterWorld(assignedCell);
            }

            return destination + GetFallbackFormationOffset(index, count);
        }

        private bool TryReserveCommandFootprint(ProjectSTilemapWorld tilemapWorld, Vector3Int origin, Vector2Int footprint, out Vector3Int assignedCell)
        {
            const int maxSearchRadius = 12;
            for (var radius = 0; radius <= maxSearchRadius; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        var candidate = origin + new Vector3Int(x, y, 0);
                        if (!CanReserveFootprint(tilemapWorld, candidate, footprint))
                        {
                            continue;
                        }

                        ReserveFootprint(candidate, footprint);
                        assignedCell = candidate;
                        return true;
                    }
                }
            }

            assignedCell = origin;
            return false;
        }

        private bool CanReserveFootprint(ProjectSTilemapWorld tilemapWorld, Vector3Int centerCell, Vector2Int footprint)
        {
            foreach (var cell in EnumerateFootprintCells(centerCell, footprint))
            {
                if (occupiedCommandCells.Contains(cell)
                    || reservedCommandCells.Contains(cell)
                    || !tilemapWorld.IsWalkable(cell))
                {
                    return false;
                }
            }

            return true;
        }

        private void ReserveFootprint(Vector3Int centerCell, Vector2Int footprint)
        {
            foreach (var cell in EnumerateFootprintCells(centerCell, footprint))
            {
                reservedCommandCells.Add(cell);
            }
        }

        private void RebuildOccupiedCommandCells()
        {
            reservedCommandCells.Clear();
            occupiedCommandCells.Clear();

            var tilemapWorld = navigator != null ? navigator.TilemapWorld : null;
            if (tilemapWorld == null)
            {
                return;
            }

            var units = UnitRegistry.AllAgents;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || selectedUnits.Contains(unit))
                {
                    continue;
                }

                var status = unit.Status;
                var footprint = status != null ? status.OccupiedCells : Vector2Int.one;
                var centerCell = tilemapWorld.WorldToCell(unit.transform.position);
                foreach (var cell in EnumerateFootprintCells(centerCell, footprint))
                {
                    occupiedCommandCells.Add(cell);
                }
            }
        }

        private static IEnumerable<Vector3Int> EnumerateFootprintCells(Vector3Int centerCell, Vector2Int footprint)
        {
            footprint = SanitizeFootprint(footprint);
            var startX = centerCell.x - (footprint.x - 1) / 2;
            var startY = centerCell.y - (footprint.y - 1) / 2;

            for (var y = 0; y < footprint.y; y++)
            {
                for (var x = 0; x < footprint.x; x++)
                {
                    yield return new Vector3Int(startX + x, startY + y, centerCell.z);
                }
            }
        }

        private static Vector2Int SanitizeFootprint(Vector2Int footprint)
        {
            return new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }

        private static Vector3 GetFallbackFormationOffset(int index, int count)
        {
            if (count <= 1 || index == 0)
            {
                return Vector3.zero;
            }

            const float spacing = 1.25f;
            var ring = Mathf.CeilToInt((Mathf.Sqrt(index + 1) - 1f) * 0.5f);
            var sideLength = ring * 2;
            var firstIndexInRing = (sideLength - 1) * (sideLength - 1);
            var offsetInRing = index - firstIndexInRing;
            var side = sideLength == 0 ? 0 : offsetInRing / sideLength;
            var sideOffset = sideLength == 0 ? 0 : offsetInRing % sideLength;

            var x = 0;
            var y = 0;
            switch (side)
            {
                case 0:
                    x = -ring + sideOffset;
                    y = ring;
                    break;
                case 1:
                    x = ring;
                    y = ring - sideOffset;
                    break;
                case 2:
                    x = ring - sideOffset;
                    y = -ring;
                    break;
                default:
                    x = -ring;
                    y = -ring + sideOffset;
                    break;
            }

            return new Vector3(x * spacing, y * spacing, 0f);
        }

        private static Rect GetScreenSelectionRect(Vector2 start, Vector2 end)
        {
            var min = Vector2.Min(start, end);
            var max = Vector2.Max(start, end);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect GetGuiSelectionRect(Vector2 start, Vector2 end)
        {
            var screenRect = GetScreenSelectionRect(start, end);
            return new Rect(
                screenRect.xMin,
                Screen.height - screenRect.yMax,
                screenRect.width,
                screenRect.height);
        }

        private void DrawSelectionRect(Rect rect)
        {
            DrawFilledRect(rect, dragFillColor);
            DrawRectOutline(rect, dragOutlineColor);
        }

        private void DrawPendingCommandPreview()
        {
            if (pendingPointCommand == PendingPointCommand.None
                && pendingBuildPlacementService == null
                && pendingRallyQueue == null)
            {
                return;
            }

            Vector3 destination;
            var hasDestination = pendingBuildPlacementService != null
                ? TryGetBuildPlacementPoint(out destination)
                : TryGetCommandPoint(out destination);
            if (!hasDestination || commandCamera == null)
            {
                return;
            }

            var screenPosition = commandCamera.WorldToScreenPoint(destination);
            if (screenPosition.z < 0f)
            {
                return;
            }

            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (pendingBuildPlacementService != null)
            {
                DrawBuildPlacementPreview(destination, guiPosition);
                return;
            }

            DrawPointCommandPreview(guiPosition);
        }

        private void DrawBuildPlacementPreview(Vector3 destination, Vector2 guiPosition)
        {
            const float cellPixelSize = 18f;
            var previewCells = pendingBuildPlacementService.GetDefaultConstructionSitePreviewCells(destination);
            var canPlace = pendingBuildPlacementService.CanPlaceDefaultConstructionSite(destination);
            var invalidBecauseOfGlobalState = !canPlace && !HasInvalidPreviewCell(previewCells);

            if (previewCells == null || previewCells.Count == 0)
            {
                var fallbackColor = canPlace ? validPlacementColor : invalidPlacementColor;
                var fallbackRect = new Rect(
                    guiPosition.x - cellPixelSize * 0.5f,
                    guiPosition.y - cellPixelSize * 0.5f,
                    cellPixelSize,
                    cellPixelSize);
                DrawFilledRect(fallbackRect, fallbackColor);
                DrawRectOutline(fallbackRect, new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.95f));
                return;
            }

            for (var i = 0; i < previewCells.Count; i++)
            {
                var previewCell = previewCells[i];
                var screenPosition = commandCamera.WorldToScreenPoint(previewCell.WorldCenter);
                if (screenPosition.z < 0f)
                {
                    continue;
                }

                var cellGuiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
                var color = previewCell.CanPlace && !invalidBecauseOfGlobalState
                    ? validPlacementColor
                    : invalidPlacementColor;
                var rect = new Rect(
                    cellGuiPosition.x - cellPixelSize * 0.5f,
                    cellGuiPosition.y - cellPixelSize * 0.5f,
                    cellPixelSize,
                    cellPixelSize);
                DrawFilledRect(rect, color);
                DrawRectOutline(rect, new Color(color.r, color.g, color.b, 0.95f));
            }
        }

        private static bool HasInvalidPreviewCell(IReadOnlyList<UnitBuildPlacementPreviewCell> previewCells)
        {
            if (previewCells == null)
            {
                return false;
            }

            for (var i = 0; i < previewCells.Count; i++)
            {
                if (!previewCells[i].CanPlace)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawPointCommandPreview(Vector2 guiPosition)
        {
            const float size = 18f;
            var horizontal = new Rect(guiPosition.x - size * 0.5f, guiPosition.y - 1f, size, 2f);
            var vertical = new Rect(guiPosition.x - 1f, guiPosition.y - size * 0.5f, 2f, size);
            DrawFilledRect(horizontal, pendingCommandCursorColor);
            DrawFilledRect(vertical, pendingCommandCursorColor);
        }

        private static void DrawFilledRect(Rect rect, Color color)
        {
            var fillTexture = Texture2D.whiteTexture;
            var previousColor = GUI.color;

            GUI.color = color;
            GUI.DrawTexture(rect, fillTexture);
            GUI.color = previousColor;
        }

        private static void DrawRectOutline(Rect rect, Color color)
        {
            var fillTexture = Texture2D.whiteTexture;
            var previousColor = GUI.color;

            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), fillTexture);

            GUI.color = previousColor;
        }

        private static bool IsPointerOverRuntimeHud(Vector2 screenPosition)
        {
            return IsScreenPointInGuiRect(screenPosition, new Rect(ResourcePanelX, ResourcePanelTopY, ResourcePanelWidth, ResourcePanelHeight))
                || IsScreenPointInGuiRect(
                    screenPosition,
                    new Rect(
                        SelectionPanelX,
                        Screen.height - SelectionPanelHeight - SelectionPanelBottomMargin,
                        SelectionPanelWidth,
                        SelectionPanelHeight))
                || IsScreenPointInGuiRect(
                    screenPosition,
                    new Rect(
                        Screen.width - CommandPanelWidth - CommandPanelRightMargin,
                        Screen.height - CommandPanelHeight - CommandPanelBottomMargin,
                        CommandPanelWidth,
                        CommandPanelHeight))
                || IsScreenPointInGuiRect(
                    screenPosition,
                    new Rect(
                        Screen.width - PathStatsPanelWidth - PathStatsPanelRightMargin,
                        PathStatsPanelTopY,
                        PathStatsPanelWidth,
                        PathStatsPanelHeight));
        }

        private static bool IsScreenPointInGuiRect(Vector2 screenPosition, Rect guiRect)
        {
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return guiRect.Contains(guiPosition);
        }

        private static string FormatInteractableTargetName(IUnitInteractableTarget target)
        {
            if (target == null)
            {
                return "MissingTarget";
            }

            return target is Component component && component != null
                ? component.gameObject.name
                : target.GetType().Name;
        }

        private static KeyControl GetDigitKey(Keyboard keyboard, int digit)
        {
            switch (digit)
            {
                case 0: return keyboard.digit0Key;
                case 1: return keyboard.digit1Key;
                case 2: return keyboard.digit2Key;
                case 3: return keyboard.digit3Key;
                case 4: return keyboard.digit4Key;
                case 5: return keyboard.digit5Key;
                case 6: return keyboard.digit6Key;
                case 7: return keyboard.digit7Key;
                case 8: return keyboard.digit8Key;
                case 9: return keyboard.digit9Key;
                default: return null;
            }
        }
    }
}
