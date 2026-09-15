namespace ProjectS.Buildings
{
    public sealed class VehicleFactoryStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.VehicleFactory;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
        }
    }
}
