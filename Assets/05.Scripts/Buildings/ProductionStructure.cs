namespace ProjectS.Buildings
{
    public sealed class ProductionStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.Production;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
        }
    }
}
