namespace ProjectS.Buildings
{
    public sealed class AutoTurretStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.AutoTurret;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<BuildingAutoTurret>();
        }
    }
}
