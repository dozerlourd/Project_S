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
        [SerializeField] private BuildingConstructionDefinition[] constructionDefinitions = new BuildingConstructionDefinition[0];
        [SerializeField] private BuildingConstructionDefinition selectedDefinition;

        public static BuildingPlacementService ActiveInstance { get; private set; }
        public Vector2Int DefaultFootprint => selectedDefinition != null ? selectedDefinition.Footprint : Vector2Int.one;
        public BuildingKind SelectedBuildingKind => selectedDefinition != null ? selectedDefinition.BuildingKind : BuildingKind.Other;
        public ResourceAmount SelectedBuildingCost => selectedDefinition != null ? selectedDefinition.Cost : default;
        public IReadOnlyList<BuildingConstructionDefinition> ConstructionDefinitions => constructionDefinitions;
        public BuildingConstructionDefinition SelectedDefinition => selectedDefinition;
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
            if (!CanPlaceSelectedBuilding(out var failureReason))
            {
                site = null;
                LastPlacementFailureReason = failureReason;
                return false;
            }

            var placed = ConstructionSite.TryCreate(
                worldPosition,
                team,
                wallet,
                tilemapWorld,
                constructionSitePrefab,
                selectedDefinition.CompletedBuildingPrefab,
                selectedDefinition.BuildingKind,
                selectedDefinition.Cost,
                selectedDefinition.BuildTime,
                selectedDefinition.Footprint,
                out site);
            LastPlacementFailureReason = placed ? string.Empty : ConstructionSite.LastCreateFailureReason;
            return placed;
        }

        public bool CanPlaceDefaultConstructionSite(Vector3 worldPosition)
        {
            ResolveReferences();
            if (!CanPlaceSelectedBuilding(out var failureReason))
            {
                LastPlacementFailureReason = failureReason;
                return false;
            }

            var placementFailureReason = ConstructionSite.GetPlacementFailureReason(tilemapWorld, worldPosition, selectedDefinition.Footprint);
            if (!string.IsNullOrEmpty(placementFailureReason))
            {
                LastPlacementFailureReason =
                    $"Cannot place {selectedDefinition.BuildingKind} construction site at {worldPosition}: {placementFailureReason}";
                return false;
            }

            if (selectedDefinition.Cost.IsEmpty)
            {
                LastPlacementFailureReason = string.Empty;
                return true;
            }

            if (wallet == null)
            {
                LastPlacementFailureReason =
                    $"Cannot place {selectedDefinition.BuildingKind} construction site: no resource wallet is available for {team}.";
                return false;
            }

            if (!wallet.CanAfford(selectedDefinition.Cost))
            {
                LastPlacementFailureReason =
                    $"Cannot place {selectedDefinition.BuildingKind} construction site: insufficient resources for cost ({selectedDefinition.Cost}).";
                return false;
            }

            LastPlacementFailureReason = string.Empty;
            return true;
        }

        public IReadOnlyList<UnitBuildPlacementPreviewCell> GetDefaultConstructionSitePreviewCells(Vector3 worldPosition)
        {
            ResolveReferences();
            return selectedDefinition != null
                ? ConstructionSite.GetPlacementPreviewCells(tilemapWorld, worldPosition, selectedDefinition.Footprint)
                : new UnitBuildPlacementPreviewCell[0];
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
            ConfigureConstructionDefinitions(new[]
            {
                BuildingConstructionDefinition.Create("Building", buildingKind, cost, buildTime, footprint, finishedPrefab)
            });
            SelectBuilding(buildingKind);
        }

        public void ConfigureConstructionDefinitions(BuildingConstructionDefinition[] definitions)
        {
            constructionDefinitions = definitions ?? new BuildingConstructionDefinition[0];
            if (selectedDefinition != null)
            {
                selectedDefinition = FindConstructionDefinition(selectedDefinition.BuildingKind);
            }
        }

        // Compatibility bridge for existing scene setup; selection still reads only constructionDefinitions.
        public void ConfigureBuildOptions(
            GameObject spliterProductionPrefab,
            GameObject autoTurretPrefab,
            GameObject speedAuraPrefab,
            GameObject supplyDepotPrefab = null,
            GameObject resourceDropOffPrefab = null,
            GameObject mainBasePrefab = null)
        {
            var definitions = new List<BuildingConstructionDefinition>();
            if (selectedDefinition != null)
            {
                definitions.Add(selectedDefinition);
            }

            definitions.Add(BuildingConstructionDefinition.Create("Spliter Production", BuildingKind.SpliterProduction, new ResourceAmount(175, 0), 9f, new Vector2Int(2, 2), spliterProductionPrefab));
            definitions.Add(BuildingConstructionDefinition.Create("Auto Turret", BuildingKind.AutoTurret, new ResourceAmount(125, 0), 7f, new Vector2Int(2, 2), autoTurretPrefab));
            definitions.Add(BuildingConstructionDefinition.Create("Speed Aura", BuildingKind.SpeedAura, new ResourceAmount(125, 25), 7f, new Vector2Int(2, 2), speedAuraPrefab));
            definitions.Add(BuildingConstructionDefinition.Create("Supply Depot", BuildingKind.SupplyDepot, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 2), supplyDepotPrefab));
            definitions.Add(BuildingConstructionDefinition.Create("Resource Drop-off", BuildingKind.ResourceDropOff, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 2), resourceDropOffPrefab));
            definitions.Add(BuildingConstructionDefinition.Create("Main Base", BuildingKind.MainBase, new ResourceAmount(350, 75), 12f, new Vector2Int(3, 3), mainBasePrefab));
            ConfigureConstructionDefinitions(definitions.ToArray());
        }

        public bool CanSelectBuilding(BuildingKind buildingKind, out string failureReason)
        {
            ResolveReferences();
            var definition = FindConstructionDefinition(buildingKind);
            if (definition == null)
            {
                failureReason = $"Building type {buildingKind} is not available.";
                return false;
            }

            if (!definition.CanBeBuiltBy(team, out failureReason))
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

            var definition = FindConstructionDefinition(buildingKind);
            if (definition.CompletedBuildingPrefab == null)
            {
                LastPlacementFailureReason = $"Building type {buildingKind} has no configured prefab.";
                return false;
            }

            selectedDefinition = definition;
            LastPlacementFailureReason = string.Empty;
            return true;
        }

        private bool CanPlaceSelectedBuilding(out string failureReason)
        {
            if (selectedDefinition == null)
            {
                failureReason = "No building type is selected.";
                return false;
            }

            return CanSelectBuilding(selectedDefinition.BuildingKind, out failureReason);
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
        [SerializeField] private string displayName = "Building";
        [SerializeField] private BuildingKind buildingKind = BuildingKind.Other;
        [SerializeField] private ResourceAmount cost;
        [SerializeField, Min(0.1f)] private float buildTime = 1f;
        [SerializeField] private Vector2Int footprint = Vector2Int.one;
        [SerializeField] private GameObject completedBuildingPrefab;
        [SerializeField] private UnlockRequirement[] unlockRequirements = new UnlockRequirement[0];

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? buildingKind.ToString() : displayName;
        public BuildingKind BuildingKind => buildingKind;
        public ResourceAmount Cost => cost;
        public float BuildTime => Mathf.Max(0.1f, buildTime);
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public GameObject CompletedBuildingPrefab => completedBuildingPrefab;
        public IReadOnlyList<UnlockRequirement> UnlockRequirements => unlockRequirements;

        public void Configure(BuildingKind kind, UnlockRequirement[] requirements = null)
        {
            buildingKind = kind;
            unlockRequirements = requirements ?? new UnlockRequirement[0];
        }

        public void Configure(
            string name,
            BuildingKind kind,
            ResourceAmount buildingCost,
            float duration,
            Vector2Int occupiedFootprint,
            GameObject finishedPrefab,
            UnlockRequirement[] requirements = null)
        {
            displayName = name;
            buildingKind = kind;
            cost = buildingCost;
            buildTime = Mathf.Max(0.1f, duration);
            footprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
            completedBuildingPrefab = finishedPrefab;
            unlockRequirements = requirements ?? new UnlockRequirement[0];
        }

        public static BuildingConstructionDefinition Create(
            string name,
            BuildingKind kind,
            ResourceAmount cost,
            float buildTime,
            Vector2Int footprint,
            GameObject prefab,
            UnlockRequirement[] unlockRequirements = null)
        {
            var definition = new BuildingConstructionDefinition();
            definition.Configure(name, kind, cost, buildTime, footprint, prefab, unlockRequirements);
            return definition;
        }

        public bool CanBeBuiltBy(UnitTeam team, out string failureReason)
        {
            return UnlockRequirement.AreMet(unlockRequirements, team, "build", buildingKind.ToString(), out failureReason);
        }
    }
}
