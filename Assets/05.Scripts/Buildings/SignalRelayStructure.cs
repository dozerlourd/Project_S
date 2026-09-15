namespace ProjectS.Buildings
{
    public sealed class SignalRelayStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.SignalRelay;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
        }
    }
}
