namespace ProjectS.Buildings
{
    public sealed class SpeedAuraStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.SpeedAura;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<BuildingSpeedAura>();
        }
    }
}
