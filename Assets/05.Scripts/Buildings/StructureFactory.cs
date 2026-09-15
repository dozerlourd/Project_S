using System;
using UnityEngine;

namespace ProjectS.Buildings
{
    public static class StructureFactory
    {
        public static BuildingStatus AddTo(GameObject target, BuildingKind kind)
        {
            var existing = target.GetComponent<BuildingStatus>();
            if (existing != null)
            {
                return existing;
            }

            var status = (BuildingStatus)target.AddComponent(GetComponentType(kind));
            status.Initialize(ProjectS.Units.UnitTeam.Team1, kind, GetDefaultFootprint(kind), true);
            return status;
        }

        public static Type GetComponentType(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.MainBase: return typeof(MainBaseStructure);
                case BuildingKind.Production: return typeof(ProductionStructure);
                case BuildingKind.SpliterProduction: return typeof(SpliterProductionStructure);
                case BuildingKind.ResourceDropOff: return typeof(ResourceDropOffStructure);
                case BuildingKind.SupplyDepot: return typeof(SupplyDepotStructure);
                case BuildingKind.AutoTurret: return typeof(AutoTurretStructure);
                case BuildingKind.SpeedAura: return typeof(SpeedAuraStructure);
                case BuildingKind.ResearchLab: return typeof(ResearchLabStructure);
                case BuildingKind.DefenseControlCenter: return typeof(DefenseControlCenterStructure);
                case BuildingKind.VehicleFactory: return typeof(VehicleFactoryStructure);
                case BuildingKind.SignalRelay: return typeof(SignalRelayStructure);
                case BuildingKind.TacticalCommandCenter: return typeof(TacticalCommandCenterStructure);
                case BuildingKind.MaintenanceBay: return typeof(MaintenanceBayStructure);
                case BuildingKind.ForwardSupplyPost: return typeof(ForwardSupplyPostStructure);
                default: return typeof(BuildingStatus);
            }
        }

        public static Vector2Int GetDefaultFootprint(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.MainBase:
                case BuildingKind.VehicleFactory:
                    return new Vector2Int(3, 3);
                case BuildingKind.TacticalCommandCenter:
                case BuildingKind.MaintenanceBay:
                    return new Vector2Int(3, 2);
                default:
                    return new Vector2Int(2, 2);
            }
        }

        public static float GetDefaultMaxHealth(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.ResearchLab: return 800f;
                case BuildingKind.DefenseControlCenter: return 950f;
                case BuildingKind.VehicleFactory: return 1250f;
                case BuildingKind.SignalRelay: return 550f;
                case BuildingKind.TacticalCommandCenter: return 1050f;
                case BuildingKind.MaintenanceBay: return 850f;
                case BuildingKind.ForwardSupplyPost: return 700f;
                default: return 650f;
            }
        }

        public static int GetDefaultSupply(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.MainBase: return 20;
                case BuildingKind.SupplyDepot: return 10;
                case BuildingKind.ForwardSupplyPost: return 5;
                default: return 0;
            }
        }

        public static float GetDefaultVisionRadius(BuildingKind kind)
        {
            return kind == BuildingKind.SignalRelay ? 12f : 9f;
        }
    }
}
