namespace ProjectS.Buildings
{
    public sealed class SignalRelayStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.SignalRelay;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<UnitProductionQueue>();
            EnsureComponent<BuildingRangeIndicator>().Configure(BuildingRangeIndicatorSource.StructureVision);
        }
    }
}
