using ProjectS.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectS.Visibility
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FogOfWarOverlay : MonoBehaviour
    {
        private static readonly Color UnexploredColor = new Color(0.01f, 0.015f, 0.02f, 1f);
        private static readonly Color ExploredColor = new Color(0.02f, 0.025f, 0.035f, 0.66f);
        private static readonly Color VisibleColor = new Color(0f, 0f, 0f, 0f);

        private FogOfWarManager manager;
        private Mesh overlayMesh;
        private Material overlayMaterial;
        private int renderedRevision = -1;

        public void Attach(FogOfWarManager fogManager)
        {
            if (manager == fogManager)
            {
                return;
            }

            if (manager != null)
            {
                manager.VisibilityChanged -= RebuildOverlay;
            }

            manager = fogManager;
            if (manager != null)
            {
                manager.VisibilityChanged += RebuildOverlay;
            }

            renderedRevision = -1;
            RebuildOverlay();
        }

        private void Awake()
        {
            EnsureRenderingResources();
        }

        private void OnEnable()
        {
            Attach(FogOfWarManager.ActiveInstance);
        }

        private void LateUpdate()
        {
            if (manager != FogOfWarManager.ActiveInstance)
            {
                Attach(FogOfWarManager.ActiveInstance);
            }

            if (manager != null && renderedRevision != manager.VisibilityRevision)
            {
                RebuildOverlay();
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.VisibilityChanged -= RebuildOverlay;
            }

            if (overlayMesh != null)
            {
                Destroy(overlayMesh);
            }

            if (overlayMaterial != null)
            {
                Destroy(overlayMaterial);
            }
        }

        private void RebuildOverlay()
        {
            if (manager == null)
            {
                return;
            }

            var world = manager.TilemapWorld;
            var bounds = manager.VisibilityBounds;
            if (world == null || bounds.size.x < 1 || bounds.size.y < 1)
            {
                return;
            }

            EnsureRenderingResources();
            var width = bounds.size.x;
            var height = bounds.size.y;
            var vertices = new Vector3[(width + 1) * (height + 1)];
            var colors = new Color[vertices.Length];
            var triangles = new int[width * height * 6];
            var triangleIndex = 0;

            for (var y = 0; y <= height; y++)
            {
                for (var x = 0; x <= width; x++)
                {
                    var index = y * (width + 1) + x;
                    var cornerCell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    vertices[index] = transform.InverseTransformPoint(GetCellCorner(world, cornerCell));
                    colors[index] = GetCornerFogColor(bounds, x, y);
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var bottomLeft = y * (width + 1) + x;
                    var bottomRight = bottomLeft + 1;
                    var topLeft = bottomLeft + width + 1;
                    var topRight = topLeft + 1;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                }
            }

            overlayMesh.Clear();
            overlayMesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            overlayMesh.vertices = vertices;
            overlayMesh.colors = colors;
            overlayMesh.triangles = triangles;
            overlayMesh.RecalculateBounds();
            renderedRevision = manager.VisibilityRevision;
        }

        private void EnsureRenderingResources()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (overlayMesh == null)
            {
                overlayMesh = new Mesh { name = "Fog Of War Overlay Mesh" };
                meshFilter.sharedMesh = overlayMesh;
            }

            if (overlayMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    overlayMaterial = new Material(shader) { name = "Fog Of War Overlay Material" };
                    meshRenderer.sharedMaterial = overlayMaterial;
                }
            }

            meshRenderer.sortingOrder = short.MaxValue;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private static Vector3 GetCellCorner(ProjectSTilemapWorld world, Vector3Int cell)
        {
            return world.Grid != null ? world.Grid.CellToWorld(cell) : (Vector3)cell;
        }

        private static Color GetFogColor(FogVisibilityState state)
        {
            switch (state)
            {
                case FogVisibilityState.Visible:
                    return VisibleColor;
                case FogVisibilityState.Explored:
                    return ExploredColor;
                default:
                    return UnexploredColor;
            }
        }

        private Color GetCornerFogColor(BoundsInt bounds, int x, int y)
        {
            var color = Color.clear;
            var samples = 0;
            for (var offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 0; offsetX++)
                {
                    var cell = new Vector3Int(bounds.xMin + x + offsetX, bounds.yMin + y + offsetY, 0);
                    if (cell.x < bounds.xMin || cell.y < bounds.yMin || cell.x >= bounds.xMax || cell.y >= bounds.yMax)
                    {
                        continue;
                    }

                    color += GetFogColor(manager.GetVisibility(cell));
                    samples++;
                }
            }

            return samples > 0 ? color / samples : UnexploredColor;
        }
    }
}
