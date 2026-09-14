using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using ProjectS.Unlocks;
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
        [SerializeField] private GameObject supplyDepotBuildingPrefab;
        [SerializeField] private GameObject resourceDropOffBuildingPrefab;
        [SerializeField] private GameObject mainBaseBuildingPrefab;
        [SerializeField] private BuildingConstructionDefinition[] constructionDefinitions = new BuildingConstructionDefinition[0];
        [SerializeField] private BuildingKind defaultBuildingKind = BuildingKind.MainBase;
        [SerializeField] private ResourceAmount defaultCost = new ResourceAmount(150, 0);
        [SerializeField, Min(0.1f)] private float defaultBuildTime = 8f;
        [SerializeField] private Vector2Int defaultFootprint = new Vector2Int(2, 2);

        public static BuildingPlacementService ActiveInstance { get; private set; }
        public Vector2Int DefaultFootprint => new Vector2Int(Mathf.Max(1, defaultFootprint.x), Mathf.Max(1, defaultFootprint.y));
        public BuildingKind SelectedBuildingKind => defaultBuildingKind;
        public ResourceAmount SelectedBuildingCost => defaultCost;
        public IReadOnlyList<BuildingConstructionDefinition> ConstructionDefinitions => constructionDefinitions;
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
            if (!CanSelectBuilding(defaultBuildingKind, out var unlockFailureReason))
            {
                site = null;
                LastPlacementFailureReason = unlockFailureReason;
                return false;
            }

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
            if (!CanSelectBuilding(defaultBuildingKind, out var unlockFailureReason))
            {
                LastPlacementFailureReason = unlockFailureReason;
                return false;
            }

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
            GameObject speedAuraPrefab,
            GameObject supplyDepotPrefab = null,
            GameObject resourceDropOffPrefab = null,
            GameObject mainBasePrefab = null)
        {
            spliterProductionBuildingPrefab = spliterProductionPrefab;
            autoTurretBuildingPrefab = autoTurretPrefab;
            speedAuraBuildingPrefab = speedAuraPrefab;
            supplyDepotBuildingPrefab = supplyDepotPrefab;
            resourceDropOffBuildingPrefab = resourceDropOffPrefab;
            mainBaseBuildingPrefab = mainBasePrefab;
        }

        public void ConfigureConstructionDefinitions(BuildingConstructionDefinition[] definitions)
        {
            constructionDefinitions = definitions ?? new BuildingConstructionDefinition[0];
        }

        public bool CanSelectBuilding(BuildingKind buildingKind, out string failureReason)
        {
            ResolveReferences();
            var definition = FindConstructionDefinition(buildingKind);
            if (definition != null && !definition.CanBeBuiltBy(team, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool SelectBuilding(BuildingKind buildingKind)
        {
            ResolveReferences();
            if (!CanSelectBuilding(buildingKind, out var unlockFailureReason))
            {
                LastPlacementFailureReason = unlockFailureReason;
                return false;
            }

            switch (buildingKind)
            {
                case BuildingKind.MainBase:
                    return SelectConfiguredBuilding(buildingKind, mainBaseBuildingPrefab, new ResourceAmount(350, 75), 12f, new Vector2Int(3, 3));
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
                case BuildingKind.SupplyDepot:
                    return SelectConfiguredBuilding(buildingKind, supplyDepotBuildingPrefab, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 2));
                case BuildingKind.ResourceDropOff:
                    return SelectConfiguredBuilding(buildingKind, resourceDropOffBuildingPrefab, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 2));
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

        private BuildingConstructionDefinition FindConstructionDefinition(BuildingKind buildingKind)
        {
            if (constructionDefinitions == null)
            {
                return null;
            }

            for (var i = 0; i < constructionDefinitions.Length; i++)
            {
                var definition = constructionDefinitions[i];
                if (definition != null && definition.BuildingKind == buildingKind)
                {
                    return definition;
                }
            }

            return null;
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

    [System.Serializable]
    public sealed class BuildingConstructionDefinition
    {
        [SerializeField] private BuildingKind buildingKind = BuildingKind.Other;
        [SerializeField] private UnlockRequirement[] unlockRequirements = new UnlockRequirement[0];

        public BuildingKind BuildingKind => buildingKind;
        public IReadOnlyList<UnlockRequirement> UnlockRequirements => unlockRequirements;

        public void Configure(BuildingKind kind, UnlockRequirement[] requirements = null)
        {
            buildingKind = kind;
            unlockRequirements = requirements ?? new UnlockRequirement[0];
        }

        public bool CanBeBuiltBy(UnitTeam team, out string failureReason)
        {
            return UnlockRequirement.AreMet(unlockRequirements, team, "build", buildingKind.ToString(), out failureReason);
        }
    }
}
