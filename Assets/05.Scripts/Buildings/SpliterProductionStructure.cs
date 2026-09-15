namespace ProjectS.Buildings
{
    public sealed class SpliterProductionStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.SpliterProduction;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
        }
    }
}
