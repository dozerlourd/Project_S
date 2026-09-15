namespace ProjectS.Buildings
{
    public sealed class ForwardSupplyPostStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.ForwardSupplyPost;

        protected override void EnsureRoleComponents()
        {
            EnsureComponent<ResourceDropOff>();
        }
    }
}
