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
        [SerializeField] private BuildingKind defaultBuildingKind = BuildingKind.MainBase;
        [SerializeField] private ResourceAmount defaultCost = new ResourceAmount(150, 0);
        [SerializeField, Min(0.1f)] private float defaultBuildTime = 8f;
        [SerializeField] private Vector2Int defaultFootprint = new Vector2Int(2, 2);

        public static BuildingPlacementService ActiveInstance { get; private set; }

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
            return ConstructionSite.TryCreate(
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
            defaultBuildingKind = buildingKind;
            defaultCost = cost;
            defaultBuildTime = Mathf.Max(0.1f, buildTime);
            defaultFootprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }

        private void ResolveReferences()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance;
            }

            if (wallet == null)
            {
                wallet = PlayerResourceWallet.FindForTeam(team);
            }
        }
    }
}
