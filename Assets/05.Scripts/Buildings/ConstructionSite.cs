using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    public sealed class ConstructionSite : MonoBehaviour, IUnitInteractableTarget
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind completedBuildingKind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private ResourceAmount cost = new ResourceAmount(150, 0);
        [SerializeField, Min(0.1f)] private float buildTime = 8f;
        [SerializeField, Min(0.1f)] private float interactionRange = 1.25f;
        [SerializeField] private bool addResourceDropOffOnComplete = true;
        [SerializeField] private GameObject completedBuildingPrefab;

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
            if (tilemapWorld == null)
            {
                return true;
            }

            var centerCell = tilemapWorld.WorldToCell(worldPosition);
            foreach (var cell in EnumerateFootprintCells(centerCell, footprint))
            {
                if (!tilemapWorld.IsBuildable(cell))
                {
                    return false;
                }
            }

            return true;
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
            if (!CanPlace(tilemapWorld, worldPosition, footprint))
            {
                return false;
            }

            if (wallet != null && !wallet.TrySpend(cost))
            {
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

            boxCollider.size = new Vector2(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            boxCollider.isTrigger = true;
        }

        private static System.Collections.Generic.IEnumerable<Vector3Int> EnumerateFootprintCells(
            Vector3Int centerCell,
            Vector2Int footprint)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
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
    }
}
