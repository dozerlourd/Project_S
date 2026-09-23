namespace ProjectS.Buildings
{
    /// <summary>Completed buildings of this type provide only advanced supply capacity.</summary>
    public sealed class AdvancedSupplyDepotStructure : BuildingStatus
    {
        protected override BuildingKind? RoleKind => BuildingKind.AdvancedSupplyDepot;
    }
}
