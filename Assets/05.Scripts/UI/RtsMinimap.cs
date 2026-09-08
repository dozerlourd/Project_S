using ProjectS.Buildings;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.UI
{
    public sealed class RtsMinimap : MonoBehaviour
    {
        private const float PanelWidth = 224f;
        private const float PanelTop = 116f;
        private const float PanelMargin = 12f;
        private const float BattleInfoHeight = 42f;
        private const float MinimumMapHeight = 84f;
        private const float CommandPanelTopMargin = 412f;
        private const float MapToCommandPanelGap = 46f;
        private const float UnitSize = 4f;
        private const float BuildingSize = 8f;

        private static readonly Color TeamOneColor = new Color(0.2f, 0.7f, 1f);
        private static readonly Color TeamTwoColor = new Color(1f, 0.28f, 0.2f);
        private static readonly Color NeutralColor = new Color(0.78f, 0.78f, 0.78f);
        private static readonly Color ViewportColor = new Color(1f, 0.92f, 0.2f);

        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private RtsCameraController cameraController;
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private UnitTeam enemyTeam = UnitTeam.Team2;

        private bool isDraggingCamera;

        public static RtsMinimap ActiveInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeMinimap()
        {
            if (FindFirstObjectByType<RtsMinimap>() == null)
            {
                new GameObject("RtsMinimap").AddComponent<RtsMinimap>();
            }
        }

        private void Awake()
        {
            ActiveInstance = this;
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
            ResolveReferences();
        }

        private void OnGUI()
        {
            if (!TryGetMapRect(out var mapRect) || !TryGetWorldBounds(out var worldBounds))
            {
                return;
            }

            GUI.Box(new Rect(mapRect.x - 4f, mapRect.y - 4f, mapRect.width + 8f, mapRect.height + 8f), string.Empty);
            DrawFilledRect(mapRect, new Color(0.06f, 0.09f, 0.11f, 0.92f));
            DrawViewport(mapRect, worldBounds);
            DrawUnits(mapRect, worldBounds);
            DrawBuildings(mapRect, worldBounds);
            DrawBattleInfo(mapRect);
            HandleCameraDrag(mapRect, worldBounds);
        }

        public static bool IsScreenPointOverMinimap(Vector2 screenPosition)
        {
            return ActiveInstance != null
                && TryGetMapRect(out var mapRect)
                && IsScreenPointInGuiRect(screenPosition, mapRect);
        }

        public bool TryGetWorldPoint(Vector2 guiPosition, out Vector3 worldPoint)
        {
            worldPoint = default;
            return TryGetMapRect(out var mapRect)
                && TryGetWorldBounds(out var worldBounds)
                && mapRect.Contains(guiPosition)
                && TryMapGuiPointToWorld(guiPosition, mapRect, worldBounds, out worldPoint);
        }

        private void ResolveReferences()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance ?? FindFirstObjectByType<ProjectSTilemapWorld>();
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<RtsCameraController>();
            }

            var commandController = PlayerUnitCommandController.ActiveInstance;
            if (commandController != null)
            {
                playerTeam = commandController.PlayerTeam;
            }
        }

        private bool TryGetWorldBounds(out Bounds worldBounds)
        {
            worldBounds = default;
            return tilemapWorld != null && tilemapWorld.TryGetWorldBounds(out worldBounds);
        }

        private static bool TryGetMapRect(out Rect mapRect)
        {
            var availableHeight = Mathf.Min(
                Screen.height - PanelTop - PanelMargin - BattleInfoHeight,
                Screen.height - CommandPanelTopMargin - PanelTop - MapToCommandPanelGap);
            var mapHeight = Mathf.Min(PanelWidth, availableHeight);
            if (mapHeight < MinimumMapHeight)
            {
                mapRect = default;
                return false;
            }

            mapRect = new Rect(Screen.width - PanelWidth - PanelMargin, PanelTop, PanelWidth, mapHeight);
            return true;
        }

        private void DrawUnits(Rect mapRect, Bounds worldBounds)
        {
            var units = UnitRegistry.AllAgents;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var status = unit != null ? unit.Status : null;
                if (status == null || !status.isActiveAndEnabled || !status.IsAlive)
                {
                    continue;
                }

                DrawMarker(mapRect, worldBounds, unit.transform.position, UnitSize, GetTeamColor(status.Team));
            }
        }

        private void DrawBuildings(Rect mapRect, Bounds worldBounds)
        {
            var buildings = BuildingRegistry.All;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.isActiveAndEnabled || !building.IsAlive)
                {
                    continue;
                }

                DrawMarker(mapRect, worldBounds, building.transform.position, BuildingSize, GetTeamColor(building.Team));
            }
        }

        private void DrawViewport(Rect mapRect, Bounds worldBounds)
        {
            var camera = cameraController != null ? cameraController.TargetCamera : Camera.main;
            if (camera == null || !TryGetViewportWorldRect(camera, out var viewportWorldRect))
            {
                return;
            }

            var min = MapWorldToGui(new Vector3(viewportWorldRect.xMin, viewportWorldRect.yMin), mapRect, worldBounds);
            var max = MapWorldToGui(new Vector3(viewportWorldRect.xMax, viewportWorldRect.yMax), mapRect, worldBounds);
            DrawOutline(Rect.MinMaxRect(min.x, min.y, max.x, max.y), ViewportColor);
        }

        private bool TryGetViewportWorldRect(Camera camera, out Rect viewportWorldRect)
        {
            viewportWorldRect = default;
            var plane = new Plane(Vector3.forward, Vector3.zero);
            var hasPoint = false;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    if (!plane.Raycast(ray, out var distance))
                    {
                        continue;
                    }

                    var point = ray.GetPoint(distance);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                    hasPoint = true;
                }
            }

            if (!hasPoint)
            {
                return false;
            }

            viewportWorldRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void DrawBattleInfo(Rect mapRect)
        {
            var infoRect = new Rect(mapRect.x, mapRect.yMax + 8f, mapRect.width, BattleInfoHeight - 4f);
            GUI.Box(infoRect, string.Empty);
            GUI.Label(new Rect(infoRect.x + 8f, infoRect.y + 3f, infoRect.width - 16f, 18f),
                $"Player  U {CountActiveUnits(playerTeam)}  B {CountActiveBuildings(playerTeam)}");
            GUI.Label(new Rect(infoRect.x + 8f, infoRect.y + 19f, infoRect.width - 16f, 18f),
                $"Enemy   U {CountActiveUnits(enemyTeam)}  B {CountActiveBuildings(enemyTeam)}");
        }

        private void HandleCameraDrag(Rect mapRect, Bounds worldBounds)
        {
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && mapRect.Contains(currentEvent.mousePosition))
            {
                if (TryHandleUnitCommand(currentEvent.mousePosition, mapRect, worldBounds, true))
                {
                    currentEvent.Use();
                    return;
                }

                isDraggingCamera = true;
                MoveCameraToGuiPosition(currentEvent.mousePosition, mapRect, worldBounds);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && mapRect.Contains(currentEvent.mousePosition))
            {
                TryHandleUnitCommand(currentEvent.mousePosition, mapRect, worldBounds, false);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && isDraggingCamera)
            {
                MoveCameraToGuiPosition(currentEvent.mousePosition, mapRect, worldBounds);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && isDraggingCamera)
            {
                isDraggingCamera = false;
                currentEvent.Use();
            }
        }

        private void MoveCameraToGuiPosition(Vector2 guiPosition, Rect mapRect, Bounds worldBounds)
        {
            var clampedGuiPosition = new Vector2(
                Mathf.Clamp(guiPosition.x, mapRect.xMin, mapRect.xMax),
                Mathf.Clamp(guiPosition.y, mapRect.yMin, mapRect.yMax));
            if (TryMapGuiPointToWorld(clampedGuiPosition, mapRect, worldBounds, out var worldPoint) && cameraController != null)
            {
                cameraController.TryMoveToWorldPoint(worldPoint);
            }
        }

        private static bool TryHandleUnitCommand(Vector2 guiPosition, Rect mapRect, Bounds worldBounds, bool isPrimaryButton)
        {
            if (!TryMapGuiPointToWorld(guiPosition, mapRect, worldBounds, out var worldPoint))
            {
                return false;
            }

            var commandController = PlayerUnitCommandController.ActiveInstance;
            return commandController != null && commandController.TryHandleMinimapCommand(worldPoint, isPrimaryButton);
        }

        private static bool TryMapGuiPointToWorld(Vector2 guiPoint, Rect mapRect, Bounds worldBounds, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (worldBounds.size.x <= 0f || worldBounds.size.y <= 0f)
            {
                return false;
            }

            var normalized = new Vector2(
                Mathf.InverseLerp(mapRect.xMin, mapRect.xMax, guiPoint.x),
                Mathf.InverseLerp(mapRect.yMin, mapRect.yMax, guiPoint.y));
            worldPoint = new Vector3(
                Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, normalized.x),
                Mathf.Lerp(worldBounds.max.y, worldBounds.min.y, normalized.y),
                0f);
            return true;
        }

        private static Vector2 MapWorldToGui(Vector3 worldPosition, Rect mapRect, Bounds worldBounds)
        {
            return new Vector2(
                Mathf.Lerp(mapRect.xMin, mapRect.xMax, Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x)),
                Mathf.Lerp(mapRect.yMax, mapRect.yMin, Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPosition.y)));
        }

        private static void DrawMarker(Rect mapRect, Bounds worldBounds, Vector3 worldPosition, float size, Color color)
        {
            var point = MapWorldToGui(worldPosition, mapRect, worldBounds);
            var markerRect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            DrawFilledRect(markerRect, color);
        }

        private static int CountActiveUnits(UnitTeam team)
        {
            var units = UnitRegistry.GetAgents(team);
            var count = 0;
            for (var i = 0; i < units.Count; i++)
            {
                var status = units[i] != null ? units[i].Status : null;
                if (status != null && status.isActiveAndEnabled && status.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveBuildings(UnitTeam team)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            var count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null && building.isActiveAndEnabled && building.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private static Color GetTeamColor(UnitTeam team)
        {
            return team == UnitTeam.Team1 ? TeamOneColor : team == UnitTeam.Team2 ? TeamTwoColor : NeutralColor;
        }

        private static bool IsScreenPointInGuiRect(Vector2 screenPosition, Rect guiRect)
        {
            return guiRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private static void DrawFilledRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
