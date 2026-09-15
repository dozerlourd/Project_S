namespace ProjectS.Buildings
{
    // Repair behavior is intentionally not implemented; Kind identifies this future role.
    public sealed class MaintenanceBayStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.MaintenanceBay;
    }
}
