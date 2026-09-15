namespace ProjectS.Buildings
{
    // Keep existing serialized enum values stable; new kinds are appended.
    public enum BuildingKind
    {
        MainBase = 0,
        Production = 1,
        ResourceDropOff = 2,
        SupplyDepot = 3,
        SpliterProduction = 4,
        AutoTurret = 5,
        SpeedAura = 6,
        Other = 7,
        ResearchLab = 8,
        DefenseControlCenter = 9,
        VehicleFactory = 10,
        SignalRelay = 11,
        TacticalCommandCenter = 12,
        MaintenanceBay = 13,
        ForwardSupplyPost = 14
    }

    // Preserve this script's GUID and type for scene references and existing GetComponent calls.
    public class BuildingStatus : Structure
    {
        protected override void RegisterBuilding()
        {
            BuildingRegistry.Register(this);
        }

        protected override void UnregisterBuilding()
        {
            BuildingRegistry.Unregister(this);
        }
    }
}
