using System.Collections.Generic;
using ProjectS.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectS.Maps.Editor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum BrushMode
        {
            SinglePaint,
            RectangleFill,
            Erase,
            HeightSelect,
            WalkablePaint,
            BuildablePaint
        }

        private MapDefinition map;
        private TileSetDefinition tileSet;
        private PlacedMapObjectType activeLayer = PlacedMapObjectType.Terrain;
        private BrushMode brushMode = BrushMode.SinglePaint;
        private TilePrefabEntry selectedEntry;
        private Vector2 scroll;
        private Vector2 validationScroll;
        private readonly List<MapValidationIssue> validationIssues = new List<MapValidationIssue>();
        private int newMapWidth = 32;
        private int newMapHeight = 32;
        private float newTileSize = 2f;
        private int paintHeightLevel;
        private float rotationY;
        private bool paintWalkable = true;
        private bool paintBuildable = true;
        private bool showGrid = true;
        private bool showHeightOverlay = true;
        private bool showWalkableOverlay;
        private bool showBuildableOverlay;
        private bool isDraggingRect;
        private Vector2Int rectStart;
        private Vector2Int hoverGrid;
        private bool hasHover;
        private Material previewMaterial;

        [MenuItem("Tools/Project S/Map Editor")]
        public static void Open()
        {
            GetWindow<MapEditorWindow>("Project S Map Editor");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawMapSection();
            DrawBrushSection();
            DrawPaletteSection();
            DrawOverlaySection();
            DrawValidationSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMapSection()
        {
            EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    map = (MapDefinition)EditorGUILayout.ObjectField("Map Definition", map, typeof(MapDefinition), false);
                    if (change.changed && map != null)
                    {
                        tileSet = map.TileSet;
                        map.EnsureCells();
                    }
                }

                tileSet = (TileSetDefinition)EditorGUILayout.ObjectField("Tile Set", tileSet, typeof(TileSetDefinition), false);
                newMapWidth = EditorGUILayout.IntField("New Width", newMapWidth);
                newMapHeight = EditorGUILayout.IntField("New Height", newMapHeight);
                newTileSize = EditorGUILayout.FloatField("New Tile Size", newTileSize);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Map Asset"))
                    {
                        CreateMapAsset();
                    }

                    using (new EditorGUI.DisabledScope(map == null))
                    {
                        if (GUILayout.Button("Save"))
                        {
                            SaveMap();
                        }

                        if (GUILayout.Button("Rebuild Preview"))
                        {
                            MapEditorPreviewBuilder.RebuildPreview(map);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Default Tile Set"))
                    {
                        ProjectSMapEditorAssetFactory.CreateDefaultTileSet();
                    }

                    if (GUILayout.Button("Clear Preview"))
                    {
                        MapEditorPreviewBuilder.ClearPreview();
                    }
                }
            }
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    activeLayer = (PlacedMapObjectType)EditorGUILayout.EnumPopup("Layer", activeLayer);
                    brushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", brushMode);
                    paintHeightLevel = EditorGUILayout.IntField("Height Level", paintHeightLevel);
                    rotationY = EditorGUILayout.Slider("Rotation Y", rotationY, 0f, 270f);
                    paintWalkable = EditorGUILayout.Toggle("Paint Walkable", paintWalkable);
                    paintBuildable = EditorGUILayout.Toggle("Paint Buildable", paintBuildable);

                    if (change.changed)
                    {
                        SceneView.RepaintAll();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rotate 90 (R)"))
                    {
                        RotateBrush(90f);
                    }

                    if (GUILayout.Button("Reset Rotation"))
                    {
                        rotationY = 0f;
                        SceneView.RepaintAll();
                    }
                }

                EditorGUILayout.HelpBox("Scene View shortcut: R rotates clockwise, Shift+R rotates counter-clockwise.", MessageType.None);
            }
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (tileSet == null)
                {
                    EditorGUILayout.HelpBox("Assign or create a TileSetDefinition.", MessageType.Info);
                    selectedEntry = null;
                    return;
                }

                foreach (var entry in tileSet.GetEntries(activeLayer))
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var selected = selectedEntry == entry;
                    var label = string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName;
                    if (GUILayout.Toggle(selected, $"{label} ({entry.terrainType})", "Button"))
                    {
                        selectedEntry = entry;
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void DrawOverlaySection()
        {
            EditorGUILayout.LabelField("Scene Overlays", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showGrid = EditorGUILayout.Toggle("Grid", showGrid);
                showHeightOverlay = EditorGUILayout.Toggle("Height", showHeightOverlay);
                showWalkableOverlay = EditorGUILayout.Toggle("Walkable", showWalkableOverlay);
                showBuildableOverlay = EditorGUILayout.Toggle("Buildable", showBuildableOverlay);
            }
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(map == null))
                {
                    if (GUILayout.Button("Validate Map"))
                    {
                        validationIssues.Clear();
                        validationIssues.AddRange(MapValidator.Validate(map));
                    }
                }

                validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
                foreach (var issue in validationIssues)
                {
                    var prefix = issue.severity.ToString().ToUpperInvariant();
                    if (GUILayout.Button($"{prefix}: {issue.message} @ {issue.gridPosition}", EditorStyles.miniButton))
                    {
                        FocusSceneView(issue.gridPosition);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (map == null)
            {
                return;
            }

            sceneView.wantsMouseMove = true;
            map.EnsureCells();
            DrawSceneOverlays();
            HandleSceneInput(sceneView);
        }

        private void HandleSceneInput(SceneView sceneView)
        {
            var currentEvent = Event.current;
            if (HandleRotationShortcut(currentEvent, sceneView))
            {
                return;
            }

            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            var ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            var previousHasHover = hasHover;
            var previousHoverGrid = hoverGrid;
            hasHover = false;

            if (plane.Raycast(ray, out var distance))
            {
                hoverGrid = map.WorldToGrid(ray.GetPoint(distance));
                hasHover = map.InBounds(hoverGrid);
            }

            if (currentEvent.type == EventType.MouseMove || previousHasHover != hasHover || previousHoverGrid != hoverGrid)
            {
                sceneView.Repaint();
            }

            if (!hasHover)
            {
                return;
            }

            if (currentEvent.type == EventType.Repaint)
            {
                DrawHoverPreview();
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && !currentEvent.alt)
            {
                Undo.RecordObject(map, "Paint Map");
                if (brushMode == BrushMode.RectangleFill)
                {
                    isDraggingRect = true;
                    rectStart = hoverGrid;
                }
                else
                {
                    ApplyBrush(hoverGrid);
                    SaveMap();
                    MapEditorPreviewBuilder.RebuildPreview(map);
                }

                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && !currentEvent.alt && brushMode != BrushMode.RectangleFill)
            {
                Undo.RecordObject(map, "Paint Map");
                ApplyBrush(hoverGrid);
                SaveMap();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && isDraggingRect)
            {
                Undo.RecordObject(map, "Fill Map Rectangle");
                FillRectangle(rectStart, hoverGrid);
                isDraggingRect = false;
                SaveMap();
                MapEditorPreviewBuilder.RebuildPreview(map);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && !currentEvent.alt)
            {
                Undo.RecordObject(map, "Erase Map");
                EraseAt(hoverGrid);
                SaveMap();
                MapEditorPreviewBuilder.RebuildPreview(map);
                currentEvent.Use();
            }

            if (isDraggingRect)
            {
                DrawRectPreview(rectStart, hoverGrid);
                sceneView.Repaint();
            }
        }

        private bool HandleRotationShortcut(Event currentEvent, SceneView sceneView)
        {
            if (currentEvent.type != EventType.KeyDown || currentEvent.keyCode != KeyCode.R)
            {
                return false;
            }

            RotateBrush(currentEvent.shift ? -90f : 90f);
            sceneView.Repaint();
            Repaint();
            currentEvent.Use();
            return true;
        }

        private void RotateBrush(float degrees)
        {
            rotationY = Mathf.Repeat(rotationY + degrees, 360f);
            rotationY = Mathf.Round(rotationY / 90f) * 90f;
            if (rotationY >= 360f)
            {
                rotationY = 0f;
            }

            SceneView.RepaintAll();
        }

        private void ApplyBrush(Vector2Int position)
        {
            switch (brushMode)
            {
                case BrushMode.SinglePaint:
                case BrushMode.RectangleFill:
                    PaintEntry(position);
                    break;
                case BrushMode.Erase:
                    EraseAt(position);
                    break;
                case BrushMode.HeightSelect:
                    var heightCell = map.GetCell(position);
                    if (heightCell != null)
                    {
                        heightCell.heightLevel = paintHeightLevel;
                    }
                    break;
                case BrushMode.WalkablePaint:
                    var walkCell = map.GetCell(position);
                    if (walkCell != null)
                    {
                        walkCell.walkable = paintWalkable;
                    }
                    break;
                case BrushMode.BuildablePaint:
                    var buildCell = map.GetCell(position);
                    if (buildCell != null)
                    {
                        buildCell.buildable = paintBuildable;
                    }
                    break;
            }
        }

        private void PaintEntry(Vector2Int position)
        {
            if (selectedEntry == null)
            {
                return;
            }

            if (activeLayer == PlacedMapObjectType.Terrain)
            {
                map.SetTerrainCell(position, selectedEntry, rotationY);
                var cell = map.GetCell(position);
                if (cell != null)
                {
                    cell.heightLevel = paintHeightLevel;
                }
                return;
            }

            if (!CanPlaceObject(position, selectedEntry.size))
            {
                return;
            }

            if (activeLayer == PlacedMapObjectType.Prop)
            {
                map.PlacedObjects.RemoveAll(item => item.gridPosition == position);
                map.PlacedObjects.Add(CreatePlacedObject(position, selectedEntry));
            }
            else if (activeLayer == PlacedMapObjectType.Resource)
            {
                map.ResourceNodes.RemoveAll(item => item.gridPosition == position);
                map.ResourceNodes.Add(new ResourceNodeData
                {
                    id = selectedEntry.id,
                    gridPosition = position,
                    size = selectedEntry.size,
                    heightLevel = paintHeightLevel,
                    prefab = selectedEntry.prefab
                });
            }
            else if (activeLayer == PlacedMapObjectType.Spawn)
            {
                map.SpawnPoints.RemoveAll(item => item.gridPosition == position);
                map.SpawnPoints.Add(new SpawnPointData
                {
                    id = selectedEntry.id,
                    gridPosition = position,
                    playerIndex = map.SpawnPoints.Count,
                    heightLevel = paintHeightLevel
                });
            }
        }

        private PlacedMapObject CreatePlacedObject(Vector2Int position, TilePrefabEntry entry)
        {
            return new PlacedMapObject
            {
                id = entry.id,
                prefab = entry.prefab,
                objectType = activeLayer,
                gridPosition = position,
                size = entry.size,
                heightLevel = paintHeightLevel,
                rotationY = entry.allowRotation ? rotationY : 0f,
                blocksMovement = entry.blocksMovement,
                blocksConstruction = entry.blocksConstruction
            };
        }

        private bool CanPlaceObject(Vector2Int position, Vector2Int size)
        {
            for (var y = 0; y < Mathf.Max(1, size.y); y++)
            {
                for (var x = 0; x < Mathf.Max(1, size.x); x++)
                {
                    if (!map.InBounds(position + new Vector2Int(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void EraseAt(Vector2Int position)
        {
            map.ClearCell(position);
        }

        private void FillRectangle(Vector2Int start, Vector2Int end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var maxX = Mathf.Max(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxY = Mathf.Max(start.y, end.y);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    ApplyBrush(new Vector2Int(x, y));
                }
            }
        }

        private void DrawSceneOverlays()
        {
            if (showGrid)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.22f);
                for (var x = 0; x <= map.Width; x++)
                {
                    Handles.DrawLine(new Vector3(x * map.TileSize, 0f, 0f), new Vector3(x * map.TileSize, 0f, map.Height * map.TileSize));
                }

                for (var y = 0; y <= map.Height; y++)
                {
                    Handles.DrawLine(new Vector3(0f, 0f, y * map.TileSize), new Vector3(map.Width * map.TileSize, 0f, y * map.TileSize));
                }
            }

            foreach (var cell in map.Cells)
            {
                if (cell == null)
                {
                    continue;
                }

                var center = map.GridToWorld(cell.Position, cell.heightLevel) + new Vector3(0f, 0.03f, 0f);

                if (showHeightOverlay && cell.terrainType != MapTerrainType.Empty)
                {
                    Handles.Label(center, cell.heightLevel.ToString());
                }

                if (showWalkableOverlay && cell.walkable)
                {
                    DrawCellOverlay(cell.Position, new Color(0.1f, 0.8f, 0.2f, 0.18f));
                }

                if (showBuildableOverlay && cell.buildable)
                {
                    DrawCellOverlay(cell.Position, new Color(0.1f, 0.35f, 1f, 0.14f));
                }
            }
        }

        private void DrawHoverPreview()
        {
            var valid = selectedEntry == null || activeLayer == PlacedMapObjectType.Terrain || CanPlaceObject(hoverGrid, selectedEntry.size);
            DrawCellOverlay(hoverGrid, valid ? new Color(1f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.35f));
            DrawPlacementPreview(valid);
        }

        private void DrawPlacementPreview(bool valid)
        {
            if (selectedEntry == null || selectedEntry.prefab == null || brushMode == BrushMode.Erase)
            {
                return;
            }

            var color = valid ? new Color(0.2f, 0.85f, 1f, 0.38f) : new Color(1f, 0.15f, 0.1f, 0.38f);
            var worldPosition = map.GridToWorld(hoverGrid, paintHeightLevel);
            var worldRotation = Quaternion.Euler(0f, selectedEntry.allowRotation ? rotationY : 0f, 0f);
            var rootMatrix = Matrix4x4.TRS(worldPosition, worldRotation, Vector3.one);
            var prefabRoot = selectedEntry.prefab.transform;
            var meshFilters = selectedEntry.prefab.GetComponentsInChildren<MeshFilter>();

            EnsurePreviewMaterial();
            previewMaterial.color = color;
            previewMaterial.SetPass(0);

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                var relativeMatrix = prefabRoot.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                Graphics.DrawMeshNow(meshFilter.sharedMesh, rootMatrix * relativeMatrix);
            }
        }

        private void EnsurePreviewMaterial()
        {
            if (previewMaterial != null)
            {
                return;
            }

            previewMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            previewMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            previewMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            previewMaterial.SetInt("_Cull", (int)CullMode.Off);
            previewMaterial.SetInt("_ZWrite", 0);
        }

        private void DrawRectPreview(Vector2Int start, Vector2Int end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var maxX = Mathf.Max(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxY = Mathf.Max(start.y, end.y);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    DrawCellOverlay(new Vector2Int(x, y), new Color(1f, 0.8f, 0f, 0.18f));
                }
            }
        }

        private void DrawCellOverlay(Vector2Int position, Color color)
        {
            var min = map.GridToWorldCorner(position);
            var max = min + new Vector3(map.TileSize, 0f, map.TileSize);
            var verts = new[]
            {
                min,
                new Vector3(max.x, min.y, min.z),
                max,
                new Vector3(min.x, min.y, max.z)
            };

            Handles.DrawSolidRectangleWithOutline(verts, color, new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a + 0.3f)));
        }

        private void FocusSceneView(Vector2Int position)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || map == null)
            {
                return;
            }

            sceneView.LookAt(map.GridToWorld(position) + Vector3.up * 3f, sceneView.rotation, 12f);
        }

        private void CreateMapAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Map Definition", "MapDefinition", "asset", "Choose where to save the map definition.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<MapDefinition>();
            asset.Initialize(newMapWidth, newMapHeight, newTileSize);
            asset.TileSet = tileSet;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            map = asset;
            Selection.activeObject = asset;
        }

        private void SaveMap()
        {
            if (map == null)
            {
                return;
            }

            map.TileSet = tileSet;
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }
    }
}
