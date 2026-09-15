namespace ProjectS.Buildings
{
    public sealed class MaintenanceBayStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.MaintenanceBay;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
        }
    }
}
