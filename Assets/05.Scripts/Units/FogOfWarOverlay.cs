using ProjectS.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectS.Visibility
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FogOfWarOverlay : MonoBehaviour
    {
        private static readonly Color UnexploredColor = new Color(0.01f, 0.015f, 0.02f, 0.98f);
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
            var cellCount = bounds.size.x * bounds.size.y;
            var vertices = new Vector3[cellCount * 4];
            var colors = new Color[cellCount * 4];
            var triangles = new int[cellCount * 6];
            var vertexIndex = 0;
            var triangleIndex = 0;

            foreach (var cell in bounds.allPositionsWithin)
            {
                var bottomLeft = GetCellCorner(world, cell);
                var bottomRight = GetCellCorner(world, cell + Vector3Int.right);
                var topLeft = GetCellCorner(world, cell + Vector3Int.up);
                var topRight = GetCellCorner(world, cell + Vector3Int.right + Vector3Int.up);
                vertices[vertexIndex] = transform.InverseTransformPoint(bottomLeft);
                vertices[vertexIndex + 1] = transform.InverseTransformPoint(bottomRight);
                vertices[vertexIndex + 2] = transform.InverseTransformPoint(topRight);
                vertices[vertexIndex + 3] = transform.InverseTransformPoint(topLeft);

                var color = GetFogColor(manager.GetVisibility(cell));
                colors[vertexIndex] = color;
                colors[vertexIndex + 1] = color;
                colors[vertexIndex + 2] = color;
                colors[vertexIndex + 3] = color;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 2;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = vertexIndex + 3;
                triangles[triangleIndex + 5] = vertexIndex + 2;
                vertexIndex += 4;
                triangleIndex += 6;
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
    }
}
