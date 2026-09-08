using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    // Keeps the runtime-only building definitions available to AI decisions.
    public sealed class AiBuildingTemplateRegistry : MonoBehaviour
    {
        private readonly Dictionary<BuildingKind, Template> templates = new Dictionary<BuildingKind, Template>();

        private UnitTeam team = UnitTeam.Team2;
        private PlayerResourceWallet wallet;
        private ProjectSTilemapWorld tilemapWorld;
        private GameObject constructionSitePrefab;

        public UnitTeam Team => team;
        public PlayerResourceWallet Wallet => wallet;
        public ProjectSTilemapWorld TilemapWorld => tilemapWorld;
        public GameObject ConstructionSitePrefab => constructionSitePrefab;

        public void Configure(
            UnitTeam ownerTeam,
            PlayerResourceWallet resourceWallet,
            ProjectSTilemapWorld world,
            GameObject sitePrefab)
        {
            team = ownerTeam;
            wallet = resourceWallet;
            tilemapWorld = world;
            constructionSitePrefab = sitePrefab;
        }

        public void RegisterTemplate(
            BuildingKind buildingKind,
            GameObject completedPrefab,
            ResourceAmount cost,
            float buildTime,
            Vector2Int footprint)
        {
            templates[buildingKind] = new Template(
                completedPrefab,
                cost,
                Mathf.Max(0.1f, buildTime),
                new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y)));
        }

        public bool TryGetTemplate(
            BuildingKind buildingKind,
            out GameObject completedPrefab,
            out ResourceAmount cost,
            out float buildTime,
            out Vector2Int footprint)
        {
            if (templates.TryGetValue(buildingKind, out var template))
            {
                completedPrefab = template.CompletedPrefab;
                cost = template.Cost;
                buildTime = template.BuildTime;
                footprint = template.Footprint;
                return true;
            }

            completedPrefab = null;
            cost = default;
            buildTime = 0f;
            footprint = Vector2Int.zero;
            return false;
        }

        public bool TryCreateConstructionSite(BuildingKind buildingKind, Vector3 worldPosition, out ConstructionSite site)
        {
            site = null;
            if (!TryGetTemplate(buildingKind, out var completedPrefab, out var cost, out var buildTime, out var footprint))
            {
                return false;
            }

            return ConstructionSite.TryCreate(
                worldPosition,
                team,
                wallet,
                tilemapWorld,
                constructionSitePrefab,
                completedPrefab,
                buildingKind,
                cost,
                buildTime,
                footprint,
                out site);
        }

        private readonly struct Template
        {
            public Template(GameObject completedPrefab, ResourceAmount cost, float buildTime, Vector2Int footprint)
            {
                CompletedPrefab = completedPrefab;
                Cost = cost;
                BuildTime = buildTime;
                Footprint = footprint;
            }

            public GameObject CompletedPrefab { get; }
            public ResourceAmount Cost { get; }
            public float BuildTime { get; }
            public Vector2Int Footprint { get; }
        }
    }
}
