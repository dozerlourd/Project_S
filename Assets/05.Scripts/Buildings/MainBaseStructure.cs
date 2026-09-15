namespace ProjectS.Buildings
{
    public sealed class MainBaseStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.MainBase;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
            EnsureComponent<ResourceDropOff>();
        }
    }
}
