using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Resources
{
    public sealed class ResourceTilemapNodeSynchronizer : MonoBehaviour
    {
        private const string ResourceTilemapName = "Resource";
        private const string GeneratedRootName = "Generated Resource Nodes";

        [SerializeField] private Tilemap resourceTilemap;
        [SerializeField] private Transform generatedNodeRoot;
        [SerializeField] private bool hideGeneratedNodesInHierarchy;

        private readonly Dictionary<Vector3Int, ResourceNode> generatedNodes = new Dictionary<Vector3Int, ResourceNode>();

        public Tilemap ResourceTilemap => resourceTilemap;
        public int GeneratedNodeCount => generatedNodes.Count;
        public bool HasResourceTiles => CountResourceTiles(resourceTilemap) > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneSynchronizers()
        {
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < tilemaps.Length; i++)
            {
                var tilemap = tilemaps[i];
                if (tilemap == null || !IsResourceTilemap(tilemap) || CountResourceTiles(tilemap) == 0)
                {
                    continue;
                }

                if (tilemap.GetComponent<ResourceTilemapNodeSynchronizer>() == null)
                {
                    tilemap.gameObject.AddComponent<ResourceTilemapNodeSynchronizer>();
                }
            }
        }

        private void Awake()
        {
            ResolveReferences();
            Synchronize();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Synchronize();
        }

        private void OnDisable()
        {
            ClearGeneratedNodes();
        }

        public void Configure(Tilemap tilemap, Transform nodeRoot = null)
        {
            resourceTilemap = tilemap;
            generatedNodeRoot = nodeRoot;
            Synchronize();
        }

        public void Synchronize()
        {
            ResolveReferences();
            if (resourceTilemap == null)
            {
                ClearGeneratedNodes();
                return;
            }

            var activeCells = new HashSet<Vector3Int>();
            foreach (var cell in resourceTilemap.cellBounds.allPositionsWithin)
            {
                var resourceTile = resourceTilemap.GetTile<ResourceTile>(cell);
                if (resourceTile == null)
                {
                    continue;
                }

                activeCells.Add(cell);
                if (!generatedNodes.TryGetValue(cell, out var node) || node == null)
                {
                    node = CreateNode(cell);
                    generatedNodes[cell] = node;
                }

                ConfigureNode(node, resourceTile);
            }

            var removedCells = new List<Vector3Int>();
            foreach (var pair in generatedNodes)
            {
                if (activeCells.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }

                removedCells.Add(pair.Key);
            }

            for (var i = 0; i < removedCells.Count; i++)
            {
                generatedNodes.Remove(removedCells[i]);
            }
        }

        public static bool SceneHasResourceTiles()
        {
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < tilemaps.Length; i++)
            {
                if (IsResourceTilemap(tilemaps[i]) && CountResourceTiles(tilemaps[i]) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (resourceTilemap == null)
            {
                resourceTilemap = GetComponent<Tilemap>();
            }

            if (resourceTilemap == null)
            {
                var tilemaps = GetComponentsInChildren<Tilemap>(true);
                for (var i = 0; i < tilemaps.Length; i++)
                {
                    if (IsResourceTilemap(tilemaps[i]))
                    {
                        resourceTilemap = tilemaps[i];
                        break;
                    }
                }
            }

            if (generatedNodeRoot == null)
            {
                var root = transform.Find(GeneratedRootName);
                if (root == null)
                {
                    var rootObject = new GameObject(GeneratedRootName);
                    rootObject.transform.SetParent(transform, false);
                    root = rootObject.transform;
                }

                generatedNodeRoot = root;
                generatedNodeRoot.gameObject.hideFlags = hideGeneratedNodesInHierarchy
                    ? HideFlags.HideInHierarchy
                    : HideFlags.None;
            }
        }

        private ResourceNode CreateNode(Vector3Int cell)
        {
            var nodeObject = new GameObject($"Resource Tile {cell.x}, {cell.y}");
            nodeObject.transform.SetParent(generatedNodeRoot, false);
            nodeObject.transform.position = resourceTilemap.GetCellCenterWorld(cell);
            var resourceLayer = LayerMask.NameToLayer(ResourceTilemapName);
            if (resourceLayer >= 0)
            {
                nodeObject.layer = resourceLayer;
            }

            var collider = nodeObject.AddComponent<BoxCollider2D>();
            var cellSize = resourceTilemap.layoutGrid != null ? resourceTilemap.layoutGrid.cellSize : Vector3.one;
            collider.size = new Vector2(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y));
            collider.isTrigger = true;
            return nodeObject.AddComponent<ResourceNode>();
        }

        private static void ConfigureNode(ResourceNode node, ResourceTile tile)
        {
            if (node == null || tile == null)
            {
                return;
            }

            node.Configure(
                tile.ResourceType == ResourceTileType.Gas ? ResourceType.Gas : ResourceType.Minerals,
                tile.InitialAmount,
                tile.GatherAmountPerTrip,
                tile.GatherDuration,
                tile.InteractionRange,
                true);
        }

        private void ClearGeneratedNodes()
        {
            foreach (var pair in generatedNodes)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            generatedNodes.Clear();
        }

        private static bool IsResourceTilemap(Tilemap tilemap)
        {
            return tilemap != null && tilemap.gameObject.name == ResourceTilemapName;
        }

        private static int CountResourceTiles(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile<ResourceTile>(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
