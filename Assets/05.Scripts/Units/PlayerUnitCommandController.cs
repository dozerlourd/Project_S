using System.Collections.Generic;
using ProjectS.Maps;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectS.Units
{
    public sealed class PlayerUnitCommandController : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private Camera commandCamera;
        [SerializeField] private MapPathfinder pathfinder;
        [SerializeField] private LayerMask unitSelectionMask = ~0;
        [SerializeField] private bool logCommandWarnings = true;
        [SerializeField] private float dragSelectThreshold = 8f;
        [SerializeField] private Color dragFillColor = new Color(0.2f, 0.6f, 1f, 0.18f);
        [SerializeField] private Color dragOutlineColor = new Color(0.2f, 0.65f, 1f, 0.85f);

        private readonly List<UnitCommandAgent> selectedUnits = new List<UnitCommandAgent>();
        private readonly List<UnitCommandAgent>[] controlGroups = new List<UnitCommandAgent>[10];
        private PendingPointCommand pendingPointCommand = PendingPointCommand.None;
        private Vector2 dragStartScreenPosition;
        private Vector2 dragCurrentScreenPosition;
        private bool isLeftMousePressed;
        private bool isDraggingSelection;
        private bool warnedMissingCamera;
        private bool warnedMissingPathfinder;
        private bool warnedMissingMapDefinition;
        private bool warnedNoSelectedUnits;

        private enum PendingPointCommand
        {
            None,
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
            for (var i = 0; i < controlGroups.Length; i++)
            {
                controlGroups[i] = new List<UnitCommandAgent>();
            }

            ResolveSceneReferences();
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
        }

        private void OnGUI()
        {
            if (!isDraggingSelection)
            {
                return;
            }

            var selectionRect = GetGuiSelectionRect(dragStartScreenPosition, dragCurrentScreenPosition);
            DrawSelectionRect(selectionRect);
        }

        private void ResolveSceneReferences()
        {
            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }

            if (pathfinder == null)
            {
                pathfinder = FindFirstObjectByType<MapPathfinder>();
            }

            if (pathfinder == null)
            {
                var runtimeBuilder = FindFirstObjectByType<MapRuntimeBuilder>();
                if (runtimeBuilder != null)
                {
                    pathfinder = runtimeBuilder.GetComponent<MapPathfinder>();
                    if (pathfinder == null)
                    {
                        pathfinder = runtimeBuilder.gameObject.AddComponent<MapPathfinder>();
                    }

                    pathfinder.MapDefinition = runtimeBuilder.MapDefinition;
                }
            }

            if (pathfinder == null)
            {
                var pathfinderObject = new GameObject("MapPathfinder_Runtime");
                pathfinder = pathfinderObject.AddComponent<MapPathfinder>();
                pathfinder.ResolveMapDefinition();
            }
        }

        private void BeginLeftMouseInteraction(Vector2 screenPosition)
        {
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
                && Vector2.Distance(dragStartScreenPosition, dragCurrentScreenPosition) >= dragSelectThreshold)
            {
                isDraggingSelection = true;
            }
        }

        private void EndLeftMouseInteraction(Vector2 screenPosition)
        {
            dragCurrentScreenPosition = screenPosition;
            isLeftMousePressed = false;

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
                CommandPointFromCursor(pendingPointCommand);
                pendingPointCommand = PendingPointCommand.None;
                return;
            }

            SelectUnitUnderCursor();
        }

        private void HandleRightClick()
        {
            pendingPointCommand = PendingPointCommand.None;

            if (TryGetUnitUnderCursor(out var target) && target.Team != playerTeam)
            {
                CommandFocusAttack(target);
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
                if (!IsAdditiveSelectionPressed())
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
            var units = FindObjectsByType<UnitCommandAgent>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
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
            if (commandCamera == null || pathfinder == null || selectedUnits.Count == 0)
            {
                WarnCommandBlockedReason();
                return;
            }

            if (!pathfinder.HasMapDefinition && !pathfinder.ResolveMapDefinition())
            {
                WarnOnce(ref warnedMissingMapDefinition, "Player move command ignored because MapPathfinder has no MapDefinition. Add a MapRuntimeBuilder with a MapDefinition, or assign the MapDefinition on MapPathfinder.");
                return;
            }

            var queue = IsCommandQueuePressed();
            var ray = commandCamera.ScreenPointToRay(GetMousePosition());
            if (!pathfinder.TryGetMapPoint(ray, out var destination))
            {
                Debug.Log("Player move command ignored because the clicked point is outside the map grid.", this);
                return;
            }

            var commandMode = pointCommand == PendingPointCommand.AttackMove
                ? UnitCommandMode.AttackMove
                : pointCommand == PendingPointCommand.Patrol
                    ? UnitCommandMode.Patrol
                    : UnitCommandMode.Move;

            IssueToSelected(new UnitCommand(commandMode, destination, null, queue));
        }

        private void CommandFocusAttack(PrototypeUnitStatus target)
        {
            if (selectedUnits.Count == 0)
            {
                WarnCommandBlockedReason();
                return;
            }

            IssueToSelected(new UnitCommand(UnitCommandMode.FocusAttack, target.transform.position, target, IsCommandQueuePressed()));
        }

        private void IssueToSelected(UnitCommand command)
        {
            var pointCommand = command.Target == null
                && (command.Mode == UnitCommandMode.Move
                    || command.Mode == UnitCommandMode.AttackMove
                    || command.Mode == UnitCommandMode.Patrol);
            var destinationIndex = 0;
            var destinationCount = CountLiveSelectedUnits();

            for (var i = selectedUnits.Count - 1; i >= 0; i--)
            {
                var unit = selectedUnits[i];
                if (unit == null)
                {
                    selectedUnits.RemoveAt(i);
                    continue;
                }

                if (pointCommand)
                {
                    var offsetDestination = command.Destination + GetFormationOffset(destinationIndex, destinationCount);
                    unit.Issue(new UnitCommand(command.Mode, offsetDestination, null, command.Queue));
                    destinationIndex++;
                }
                else
                {
                    unit.Issue(command);
                }
            }
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

            if (keyboard.aKey.wasPressedThisFrame)
            {
                pendingPointCommand = PendingPointCommand.AttackMove;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                pendingPointCommand = PendingPointCommand.Patrol;
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                pendingPointCommand = PendingPointCommand.None;
                foreach (var unit in selectedUnits)
                {
                    unit?.Stop();
                }
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                pendingPointCommand = PendingPointCommand.None;
                foreach (var unit in selectedUnits)
                {
                    unit?.HoldPosition();
                }
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
            else if (pathfinder == null)
            {
                WarnOnce(ref warnedMissingPathfinder, "Player move command ignored because no MapPathfinder or MapRuntimeBuilder was found in the scene.");
            }
            else if (selectedUnits.Count == 0)
            {
                WarnOnce(ref warnedNoSelectedUnits, "Player move command ignored because no player ground unit is selected.");
            }
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

            var ray = commandCamera.ScreenPointToRay(GetMousePosition());
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, unitSelectionMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            status = hit.collider.GetComponentInParent<PrototypeUnitStatus>();
            return status != null;
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
                ring.gameObject.SetActive(visible);
            }
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

        private static bool IsCommandQueuePressed()
        {
            return IsAdditiveSelectionPressed();
        }

        private static bool IsControlGroupAssignPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static Vector3 GetFormationOffset(int index, int count)
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
            var z = 0;
            switch (side)
            {
                case 0:
                    x = -ring + sideOffset;
                    z = ring;
                    break;
                case 1:
                    x = ring;
                    z = ring - sideOffset;
                    break;
                case 2:
                    x = ring - sideOffset;
                    z = -ring;
                    break;
                default:
                    x = -ring;
                    z = -ring + sideOffset;
                    break;
            }

            return new Vector3(x * spacing, 0f, z * spacing);
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
            var fillTexture = Texture2D.whiteTexture;
            var previousColor = GUI.color;

            GUI.color = dragFillColor;
            GUI.DrawTexture(rect, fillTexture);

            GUI.color = dragOutlineColor;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), fillTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), fillTexture);

            GUI.color = previousColor;
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
