using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    public sealed class BuildingPlacementService : MonoBehaviour, IUnitBuildPlacementService
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private GameObject constructionSitePrefab;
        [SerializeField] private GameObject completedBuildingPrefab;
        [SerializeField] private GameObject combatProductionBuildingPrefab;
        [SerializeField] private GameObject spliterProductionBuildingPrefab;
        [SerializeField] private GameObject autoTurretBuildingPrefab;
        [SerializeField] private GameObject speedAuraBuildingPrefab;
        [SerializeField] private BuildingKind defaultBuildingKind = BuildingKind.MainBase;
        [SerializeField] private ResourceAmount defaultCost = new ResourceAmount(150, 0);
        [SerializeField, Min(0.1f)] private float defaultBuildTime = 8f;
        [SerializeField] private Vector2Int defaultFootprint = new Vector2Int(2, 2);

        public static BuildingPlacementService ActiveInstance { get; private set; }
        public Vector2Int DefaultFootprint => new Vector2Int(Mathf.Max(1, defaultFootprint.x), Mathf.Max(1, defaultFootprint.y));
        public BuildingKind SelectedBuildingKind => defaultBuildingKind;
        public ResourceAmount SelectedBuildingCost => defaultCost;
        public string LastPlacementFailureReason { get; private set; }

        private void Awake()
        {
            ActiveInstance = this;
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public bool TryPlaceDefaultConstructionSite(Vector3 worldPosition, out ConstructionSite site)
        {
            ResolveReferences();
            var placed = ConstructionSite.TryCreate(
                worldPosition,
                team,
                wallet,
                tilemapWorld,
                constructionSitePrefab,
                completedBuildingPrefab,
                defaultBuildingKind,
                defaultCost,
                defaultBuildTime,
                defaultFootprint,
                out site);
            LastPlacementFailureReason = placed ? string.Empty : ConstructionSite.LastCreateFailureReason;
            return placed;
        }

        public bool CanPlaceDefaultConstructionSite(Vector3 worldPosition)
        {
            ResolveReferences();
            var placementFailureReason = ConstructionSite.GetPlacementFailureReason(tilemapWorld, worldPosition, DefaultFootprint);
            if (!string.IsNullOrEmpty(placementFailureReason))
            {
                LastPlacementFailureReason =
                    $"Cannot place {defaultBuildingKind} construction site at {worldPosition}: {placementFailureReason}";
                return false;
            }

            if (defaultCost.IsEmpty)
            {
                LastPlacementFailureReason = string.Empty;
                return true;
            }

            if (wallet == null)
            {
                LastPlacementFailureReason =
                    $"Cannot place {defaultBuildingKind} construction site: no resource wallet is available for {team}.";
                return false;
            }

            if (!wallet.CanAfford(defaultCost))
            {
                LastPlacementFailureReason =
                    $"Cannot place {defaultBuildingKind} construction site: insufficient resources for cost ({defaultCost}).";
                return false;
            }

            LastPlacementFailureReason = string.Empty;
            return true;
        }

        public IReadOnlyList<UnitBuildPlacementPreviewCell> GetDefaultConstructionSitePreviewCells(Vector3 worldPosition)
        {
            ResolveReferences();
            return ConstructionSite.GetPlacementPreviewCells(tilemapWorld, worldPosition, DefaultFootprint);
        }

        bool IUnitBuildPlacementService.CanPlaceDefaultConstructionSite(Vector3 worldPosition)
        {
            return CanPlaceDefaultConstructionSite(worldPosition);
        }

        IReadOnlyList<UnitBuildPlacementPreviewCell> IUnitBuildPlacementService.GetDefaultConstructionSitePreviewCells(
            Vector3 worldPosition)
        {
            return GetDefaultConstructionSitePreviewCells(worldPosition);
        }

        bool IUnitBuildPlacementService.TryPlaceDefaultConstructionSite(
            Vector3 worldPosition,
            out IUnitInteractableTarget constructionSite)
        {
            var placed = TryPlaceDefaultConstructionSite(worldPosition, out var site);
            constructionSite = site;
            return placed;
        }

        public void Configure(
            UnitTeam ownerTeam,
            PlayerResourceWallet resourceWallet,
            ProjectSTilemapWorld world,
            GameObject sitePrefab,
            GameObject finishedPrefab,
            BuildingKind buildingKind,
            ResourceAmount cost,
            float buildTime,
            Vector2Int footprint)
        {
            team = ownerTeam;
            wallet = resourceWallet;
            tilemapWorld = world;
            constructionSitePrefab = sitePrefab;
            completedBuildingPrefab = finishedPrefab;
            combatProductionBuildingPrefab = finishedPrefab;
            defaultBuildingKind = buildingKind;
            defaultCost = cost;
            defaultBuildTime = Mathf.Max(0.1f, buildTime);
            defaultFootprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }

        public void ConfigureBuildOptions(
            GameObject spliterProductionPrefab,
            GameObject autoTurretPrefab,
            GameObject speedAuraPrefab)
        {
            spliterProductionBuildingPrefab = spliterProductionPrefab;
            autoTurretBuildingPrefab = autoTurretPrefab;
            speedAuraBuildingPrefab = speedAuraPrefab;
        }

        public bool SelectBuilding(BuildingKind buildingKind)
        {
            switch (buildingKind)
            {
                case BuildingKind.Production:
                    if (combatProductionBuildingPrefab == null)
                    {
                        LastPlacementFailureReason = "Combat production building prefab is not configured.";
                        return false;
                    }

                    completedBuildingPrefab = combatProductionBuildingPrefab;
                    defaultBuildingKind = BuildingKind.Production;
                    defaultCost = new ResourceAmount(150, 0);
                    defaultBuildTime = 8f;
                    defaultFootprint = new Vector2Int(2, 2);
                    LastPlacementFailureReason = string.Empty;
                    return true;
                case BuildingKind.SpliterProduction:
                    return SelectConfiguredBuilding(buildingKind, spliterProductionBuildingPrefab, new ResourceAmount(175, 0), 9f, new Vector2Int(2, 2));
                case BuildingKind.AutoTurret:
                    return SelectConfiguredBuilding(buildingKind, autoTurretBuildingPrefab, new ResourceAmount(125, 0), 7f, new Vector2Int(2, 2));
                case BuildingKind.SpeedAura:
                    return SelectConfiguredBuilding(buildingKind, speedAuraBuildingPrefab, new ResourceAmount(125, 25), 7f, new Vector2Int(2, 2));
                default:
                    LastPlacementFailureReason = $"Building type {buildingKind} is not available.";
                    return false;
            }
        }

        private bool SelectConfiguredBuilding(BuildingKind buildingKind, GameObject prefab, ResourceAmount cost, float buildTime, Vector2Int footprint)
        {
            if (prefab == null)
            {
                LastPlacementFailureReason = $"Building type {buildingKind} has no configured prefab.";
                return false;
            }

            completedBuildingPrefab = prefab;
            defaultBuildingKind = buildingKind;
            defaultCost = cost;
            defaultBuildTime = buildTime;
            defaultFootprint = footprint;
            LastPlacementFailureReason = string.Empty;
            return true;
        }

        private void ResolveReferences()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance;
            }

            if (wallet == null || wallet.Team != team)
            {
                wallet = PlayerResourceWallet.FindForTeam(team);
            }
        }
    }
}
