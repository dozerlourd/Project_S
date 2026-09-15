namespace ProjectS.Buildings
{
    public sealed class ResourceDropOffStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.ResourceDropOff;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<ResourceDropOff>();
        }
    }
}
