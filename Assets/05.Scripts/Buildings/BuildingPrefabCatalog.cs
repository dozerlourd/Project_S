using UnityEngine;

namespace ProjectS.Buildings
{
    [CreateAssetMenu(fileName = "BuildingPrefabCatalog", menuName = "Project S/Buildings/Prefab Catalog")]
    public sealed class BuildingPrefabCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Buildings/BuildingPrefabCatalog";

        [SerializeField] private GameObject mainBasePrefab;
        [SerializeField] private GameObject productionPrefab;
        [SerializeField] private GameObject spliterProductionPrefab;
        [SerializeField] private GameObject autoTurretPrefab;
        [SerializeField] private GameObject speedAuraPrefab;
        [SerializeField] private GameObject vehicleFactoryPrefab;
        [SerializeField] private GameObject maintenanceBayPrefab;
        [SerializeField] private GameObject signalRelayPrefab;
        [SerializeField] private GameObject constructionSitePrefab;

        public GameObject ConstructionSitePrefab => constructionSitePrefab;

        public static BuildingPrefabCatalog Load()
        {
            return UnityEngine.Resources.Load<BuildingPrefabCatalog>(ResourcesPath);
        }

        public GameObject GetPrefab(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.MainBase: return mainBasePrefab;
                case BuildingKind.Production: return productionPrefab;
                case BuildingKind.SpliterProduction: return spliterProductionPrefab;
                case BuildingKind.AutoTurret: return autoTurretPrefab;
                case BuildingKind.SpeedAura: return speedAuraPrefab;
                case BuildingKind.VehicleFactory: return vehicleFactoryPrefab;
                case BuildingKind.MaintenanceBay: return maintenanceBayPrefab;
                case BuildingKind.SignalRelay: return signalRelayPrefab;
                default: return null;
            }
        }

        public bool TryValidate(out string failureReason)
        {
            if (mainBasePrefab == null
                || productionPrefab == null
                || spliterProductionPrefab == null
                || autoTurretPrefab == null
                || speedAuraPrefab == null
                || vehicleFactoryPrefab == null
                || maintenanceBayPrefab == null
                || signalRelayPrefab == null
                || constructionSitePrefab == null)
            {
                failureReason = "The runtime building prefab catalog is missing one or more core prefab references.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
