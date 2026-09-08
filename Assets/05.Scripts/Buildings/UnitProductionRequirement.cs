using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    public enum UnitProductionRequirementKind
    {
        None,
        CompletedBuilding
    }

    [System.Serializable]
    public sealed class UnitProductionRequirement
    {
        [SerializeField] private UnitProductionRequirementKind kind;
        [SerializeField] private BuildingKind requiredBuildingKind = BuildingKind.Other;
        [SerializeField, Min(1)] private int requiredCount = 1;

        public UnitProductionRequirementKind Kind => kind;
        public BuildingKind RequiredBuildingKind => requiredBuildingKind;
        public int RequiredCount => Mathf.Max(1, requiredCount);

        public bool IsMet(UnitTeam team)
        {
            switch (kind)
            {
                case UnitProductionRequirementKind.None:
                    return true;
                case UnitProductionRequirementKind.CompletedBuilding:
                    return CountCompletedBuildings(team, requiredBuildingKind) >= RequiredCount;
                default:
                    return false;
            }
        }

        public string GetFailureReason(UnitProductionDefinition definition)
        {
            var displayName = definition != null ? definition.DisplayName : "production";
            switch (kind)
            {
                case UnitProductionRequirementKind.CompletedBuilding:
                    return $"Cannot enqueue {displayName}: requires {RequiredCount} completed {requiredBuildingKind} building(s).";
                default:
                    return $"Cannot enqueue {displayName}: production requirement is not met.";
            }
        }

        public void ConfigureCompletedBuilding(BuildingKind buildingKind, int count = 1)
        {
            kind = UnitProductionRequirementKind.CompletedBuilding;
            requiredBuildingKind = buildingKind;
            requiredCount = Mathf.Max(1, count);
        }

        private static int CountCompletedBuildings(UnitTeam team, BuildingKind buildingKind)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            var count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null
                    && building.isActiveAndEnabled
                    && building.Completed
                    && building.IsAlive
                    && building.Kind == buildingKind)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
