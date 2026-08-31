using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    public sealed class ConstructionSite : MonoBehaviour, IUnitInteractableTarget
    {
        private static readonly List<ConstructionSite> ActiveSites = new List<ConstructionSite>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind completedBuildingKind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private ResourceAmount cost = new ResourceAmount(150, 0);
        [SerializeField, Min(0.1f)] private float buildTime = 8f;
        [SerializeField, Min(0.1f)] private float interactionRange = 1.25f;
        [SerializeField] private bool addResourceDropOffOnComplete = true;
        [SerializeField] private GameObject completedBuildingPrefab;

        private static string lastCreateFailureReason;
        private float buildProgress;
        private bool completed;

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => interactionRange;
        public UnitTeam Team => team;
        public ResourceAmount Cost => cost;
        public float BuildTime => Mathf.Max(0.1f, buildTime);
        public float BuildProgress => buildProgress;
        public float BuildProgress01 => Mathf.Clamp01(buildProgress / BuildTime);
        public bool Completed => completed;
        public static string LastCreateFailureReason => lastCreateFailureReason;

        private void OnValidate()
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            buildTime = Mathf.Max(0.1f, buildTime);
            interactionRange = Mathf.Max(0.1f, interactionRange);
        }

        private void Awake()
        {
            EnsureCollider();
        }

        private void OnEnable()
        {
            if (!ActiveSites.Contains(this))
            {
                ActiveSites.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSites.Remove(this);
        }

        public void Initialize(
            UnitTeam ownerTeam,
            BuildingKind buildingKind,
            Vector2Int occupiedFootprint,
            ResourceAmount buildCost,
            float duration,
            GameObject finishedPrefab)
        {
            team = ownerTeam;
            completedBuildingKind = buildingKind;
            footprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
            cost = buildCost;
            buildTime = Mathf.Max(0.1f, duration);
            completedBuildingPrefab = finishedPrefab;
            buildProgress = 0f;
            completed = false;
            EnsureCollider();
        }

        public bool CanInteract(UnitCommandAgent agent)
        {
            var unitStatus = agent != null ? agent.Status : null;
            return !completed
                && unitStatus != null
                && unitStatus.Team == team
                && unitStatus.Roles.HasFlag(UnitRole.Builder);
        }

        public bool TryContribute(UnitCommandAgent builder, float deltaTime)
        {
            if (!CanInteract(builder) || deltaTime <= 0f)
            {
                return false;
            }

            buildProgress += deltaTime;
            if (buildProgress >= BuildTime)
            {
                CompleteConstruction();
            }

            return true;
        }

        public static bool CanPlace(ProjectSTilemapWorld tilemapWorld, Vector3 worldPosition, Vector2Int footprint)
        {
            return string.IsNullOrEmpty(GetPlacementFailureReason(tilemapWorld, worldPosition, footprint));
        }

        public static string GetPlacementFailureReason(
            ProjectSTilemapWorld tilemapWorld,
            Vector3 worldPosition,
            Vector2Int footprint)
        {
            var previewCells = GetPlacementPreviewCells(tilemapWorld, worldPosition, footprint);
            for (var i = 0; i < previewCells.Count; i++)
            {
                if (!previewCells[i].CanPlace)
                {
                    return previewCells[i].FailureReason;
                }
            }

            return string.Empty;
        }

        public static IReadOnlyList<UnitBuildPlacementPreviewCell> GetPlacementPreviewCells(
            ProjectSTilemapWorld tilemapWorld,
            Vector3 worldPosition,
            Vector2Int footprint)
        {
            footprint = SanitizeFootprint(footprint);
            var previewCells = new List<UnitBuildPlacementPreviewCell>();
            var centerCell = tilemapWorld != null ? tilemapWorld.WorldToCell(worldPosition) : Vector3Int.zero;
            foreach (var cell in EnumerateFootprintCells(centerCell, footprint))
            {
                var cellCenter = tilemapWorld != null
                    ? tilemapWorld.GetCellCenterWorld(cell)
                    : worldPosition + new Vector3(cell.x - centerCell.x, cell.y - centerCell.y, 0f);
                var reason = GetCellPlacementFailureReason(tilemapWorld, cell, cellCenter);
                previewCells.Add(new UnitBuildPlacementPreviewCell(cellCenter, string.IsNullOrEmpty(reason), reason));
            }

            return previewCells;
        }

        public static bool TryCreate(
            Vector3 worldPosition,
            UnitTeam team,
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject sitePrefab,
            GameObject completedBuildingPrefab,
            BuildingKind buildingKind,
            ResourceAmount cost,
            float buildTime,
            Vector2Int footprint,
            out ConstructionSite site)
        {
            site = null;
            var placementFailureReason = GetPlacementFailureReason(tilemapWorld, worldPosition, footprint);
            if (!string.IsNullOrEmpty(placementFailureReason))
            {
                FailCreate($"Cannot place {buildingKind} construction site at {worldPosition}: {placementFailureReason}");
                return false;
            }

            if (!cost.IsEmpty && wallet == null)
            {
                FailCreate($"Cannot place {buildingKind} construction site: no resource wallet is available for {team}.");
                return false;
            }

            if (wallet != null && wallet.Team != team)
            {
                FailCreate(
                    $"Cannot place {buildingKind} construction site: wallet team mismatch. Expected {team}, received {wallet.Team}.");
                return false;
            }

            if (wallet != null && !wallet.TrySpend(cost))
            {
                FailCreate($"Cannot place {buildingKind} construction site: insufficient resources for cost ({cost}).");
                return false;
            }

            var siteObject = sitePrefab != null
                ? Instantiate(sitePrefab, worldPosition, Quaternion.identity)
                : new GameObject("ConstructionSite");
            siteObject.transform.position = worldPosition;
            siteObject.SetActive(true);
            site = siteObject.GetComponent<ConstructionSite>();
            if (site == null)
            {
                site = siteObject.AddComponent<ConstructionSite>();
            }

            site.Initialize(team, buildingKind, footprint, cost, buildTime, completedBuildingPrefab);
            lastCreateFailureReason = string.Empty;
            return true;
        }

        private void CompleteConstruction()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            buildProgress = BuildTime;
            if (completedBuildingPrefab != null)
            {
                var completedObject = Instantiate(completedBuildingPrefab, transform.position, transform.rotation);
                completedObject.SetActive(true);
                var completedStatus = completedObject.GetComponent<BuildingStatus>();
                if (completedStatus != null)
                {
                    completedStatus.Initialize(team, completedBuildingKind, footprint, true);
                }

                gameObject.SetActive(false);
                return;
            }

            var buildingStatus = GetComponent<BuildingStatus>();
            if (buildingStatus == null)
            {
                buildingStatus = gameObject.AddComponent<BuildingStatus>();
            }

            buildingStatus.Initialize(team, completedBuildingKind, footprint, true);
            if (addResourceDropOffOnComplete
                && (completedBuildingKind == BuildingKind.MainBase || completedBuildingKind == BuildingKind.ResourceDropOff)
                && GetComponent<ResourceDropOff>() == null)
            {
                gameObject.AddComponent<ResourceDropOff>();
            }

            if ((completedBuildingKind == BuildingKind.MainBase || completedBuildingKind == BuildingKind.Production)
                && GetComponent<UnitProductionQueue>() == null)
            {
                gameObject.AddComponent<UnitProductionQueue>();
            }

            name = $"{completedBuildingKind} Building";
            enabled = false;
        }

        private void EnsureCollider()
        {
            var boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            var sanitizedFootprint = SanitizeFootprint(footprint);
            boxCollider.size = new Vector2(sanitizedFootprint.x, sanitizedFootprint.y);
            boxCollider.isTrigger = true;
        }

        private static void FailCreate(string reason)
        {
            lastCreateFailureReason = reason;
            Debug.LogWarning(reason);
        }

        private static string GetCellPlacementFailureReason(
            ProjectSTilemapWorld tilemapWorld,
            Vector3Int cell,
            Vector3 cellCenter)
        {
            if (tilemapWorld != null && !tilemapWorld.IsBuildable(cell))
            {
                return $"cell {cell} is not buildable.";
            }

            if (IsOccupiedByConstructionSite(tilemapWorld, cell, cellCenter))
            {
                return $"cell {cell} is occupied by another construction site.";
            }

            if (IsOccupiedByBuilding(tilemapWorld, cell, cellCenter))
            {
                return $"cell {cell} is occupied by a building.";
            }

            if (IsOccupiedByResource(tilemapWorld, cell, cellCenter))
            {
                return $"cell {cell} is occupied by a resource node.";
            }

            if (IsOccupiedByUnit(tilemapWorld, cell, cellCenter))
            {
                return $"cell {cell} is occupied by a unit.";
            }

            return string.Empty;
        }

        private static bool IsOccupiedByConstructionSite(
            ProjectSTilemapWorld tilemapWorld,
            Vector3Int cell,
            Vector3 cellCenter)
        {
            for (var i = ActiveSites.Count - 1; i >= 0; i--)
            {
                var site = ActiveSites[i];
                if (site == null)
                {
                    ActiveSites.RemoveAt(i);
                    continue;
                }

                if (!site.isActiveAndEnabled || site.completed)
                {
                    continue;
                }

                if (OccupiesCell(tilemapWorld, site.transform.position, site.footprint, cell, cellCenter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOccupiedByBuilding(
            ProjectSTilemapWorld tilemapWorld,
            Vector3Int cell,
            Vector3 cellCenter)
        {
            var buildings = BuildingRegistry.All;
            for (var i = buildings.Count - 1; i >= 0; i--)
            {
                var building = buildings[i];
                if (building == null || !building.isActiveAndEnabled)
                {
                    continue;
                }

                if (OccupiesCell(tilemapWorld, building.transform.position, building.Footprint, cell, cellCenter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOccupiedByResource(
            ProjectSTilemapWorld tilemapWorld,
            Vector3Int cell,
            Vector3 cellCenter)
        {
            var resources = ResourceNode.AllNodes;
            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource == null || !resource.isActiveAndEnabled || resource.IsDepleted)
                {
                    continue;
                }

                if (OccupiesColliderCell(tilemapWorld, resource.GetComponent<Collider2D>(), resource.transform.position, cell, cellCenter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOccupiedByUnit(
            ProjectSTilemapWorld tilemapWorld,
            Vector3Int cell,
            Vector3 cellCenter)
        {
            var units = UnitRegistry.AllAgents;
            for (var i = units.Count - 1; i >= 0; i--)
            {
                var unit = units[i];
                if (unit == null || !unit.isActiveAndEnabled)
                {
                    continue;
                }

                var status = unit.Status;
                if (status == null || !status.isActiveAndEnabled)
                {
                    continue;
                }

                if (OccupiesCell(tilemapWorld, unit.transform.position, status.OccupiedCells, cell, cellCenter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OccupiesCell(
            ProjectSTilemapWorld tilemapWorld,
            Vector3 worldPosition,
            Vector2Int occupiedFootprint,
            Vector3Int queriedCell,
            Vector3 queriedCellCenter)
        {
            occupiedFootprint = SanitizeFootprint(occupiedFootprint);
            if (tilemapWorld != null)
            {
                var centerCell = tilemapWorld.WorldToCell(worldPosition);
                foreach (var occupiedCell in EnumerateFootprintCells(centerCell, occupiedFootprint))
                {
                    if (occupiedCell == queriedCell)
                    {
                        return true;
                    }
                }

                return false;
            }

            var halfExtents = new Vector2(occupiedFootprint.x * 0.5f, occupiedFootprint.y * 0.5f);
            return Mathf.Abs(worldPosition.x - queriedCellCenter.x) < halfExtents.x
                && Mathf.Abs(worldPosition.y - queriedCellCenter.y) < halfExtents.y;
        }

        private static bool OccupiesColliderCell(
            ProjectSTilemapWorld tilemapWorld,
            Collider2D collider,
            Vector3 fallbackWorldPosition,
            Vector3Int queriedCell,
            Vector3 queriedCellCenter)
        {
            if (collider == null)
            {
                return OccupiesCell(tilemapWorld, fallbackWorldPosition, Vector2Int.one, queriedCell, queriedCellCenter);
            }

            if (tilemapWorld != null)
            {
                var minCell = tilemapWorld.WorldToCell(collider.bounds.min);
                var maxCell = tilemapWorld.WorldToCell(collider.bounds.max);
                return queriedCell.x >= Mathf.Min(minCell.x, maxCell.x)
                    && queriedCell.x <= Mathf.Max(minCell.x, maxCell.x)
                    && queriedCell.y >= Mathf.Min(minCell.y, maxCell.y)
                    && queriedCell.y <= Mathf.Max(minCell.y, maxCell.y);
            }

            var cellBounds = new Bounds(queriedCellCenter, new Vector3(1f, 1f, 0.1f));
            return cellBounds.Intersects(collider.bounds);
        }

        private static System.Collections.Generic.IEnumerable<Vector3Int> EnumerateFootprintCells(
            Vector3Int centerCell,
            Vector2Int footprint)
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
    }
}
