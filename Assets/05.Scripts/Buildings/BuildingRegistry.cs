using System.Collections.Generic;
using ProjectS.Units;

namespace ProjectS.Buildings
{
    public static class BuildingRegistry
    {
        private static readonly List<BuildingStatus> AllBuildings = new List<BuildingStatus>();
        private static readonly Dictionary<UnitTeam, List<BuildingStatus>> BuildingsByTeam =
            new Dictionary<UnitTeam, List<BuildingStatus>>();
        private static readonly List<BuildingStatus> EmptyBuildings = new List<BuildingStatus>(0);

        public static IReadOnlyList<BuildingStatus> All => AllBuildings;

        public static void Register(BuildingStatus building)
        {
            if (building == null)
            {
                return;
            }

            if (!AllBuildings.Contains(building))
            {
                AllBuildings.Add(building);
            }

            if (!BuildingsByTeam.TryGetValue(building.Team, out var buildings))
            {
                buildings = new List<BuildingStatus>();
                BuildingsByTeam.Add(building.Team, buildings);
            }

            if (!buildings.Contains(building))
            {
                buildings.Add(building);
            }
        }

        public static void Unregister(BuildingStatus building)
        {
            if (building == null)
            {
                return;
            }

            AllBuildings.Remove(building);
            if (BuildingsByTeam.TryGetValue(building.Team, out var buildings))
            {
                buildings.Remove(building);
            }
        }

        public static IReadOnlyList<BuildingStatus> GetBuildings(UnitTeam team)
        {
            return BuildingsByTeam.TryGetValue(team, out var buildings)
                ? buildings
                : EmptyBuildings;
        }
    }
}
