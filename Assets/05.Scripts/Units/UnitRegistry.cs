using System.Collections.Generic;

namespace ProjectS.Units
{
    public static class UnitRegistry
    {
        private static readonly List<UnitCommandAgent> AllCommandAgents = new List<UnitCommandAgent>();
        private static readonly Dictionary<UnitTeam, List<UnitCommandAgent>> CommandAgentsByTeam =
            new Dictionary<UnitTeam, List<UnitCommandAgent>>();
        private static readonly List<UnitCommandAgent> EmptyCommandAgents = new List<UnitCommandAgent>(0);
        private static readonly UnitTeam[] Teams =
        {
            UnitTeam.Team1,
            UnitTeam.Team2,
            UnitTeam.Team3,
            UnitTeam.Team4,
            UnitTeam.Team5,
            UnitTeam.Team6,
            UnitTeam.Team7,
            UnitTeam.Team8
        };

        public static IReadOnlyList<UnitCommandAgent> AllAgents => AllCommandAgents;
        public static IReadOnlyList<UnitTeam> AllTeams => Teams;

        public static void Register(UnitCommandAgent agent, PrototypeUnitStatus status)
        {
            if (agent == null || status == null || AllCommandAgents.Contains(agent))
            {
                return;
            }

            AllCommandAgents.Add(agent);
            if (!CommandAgentsByTeam.TryGetValue(status.Team, out var teamAgents))
            {
                teamAgents = new List<UnitCommandAgent>();
                CommandAgentsByTeam.Add(status.Team, teamAgents);
            }

            teamAgents.Add(agent);
        }

        public static void Unregister(UnitCommandAgent agent, PrototypeUnitStatus status)
        {
            if (agent == null)
            {
                return;
            }

            AllCommandAgents.Remove(agent);
            if (status == null || !CommandAgentsByTeam.TryGetValue(status.Team, out var teamAgents))
            {
                return;
            }

            teamAgents.Remove(agent);
        }

        public static IReadOnlyList<UnitCommandAgent> GetAgents(UnitTeam team)
        {
            return CommandAgentsByTeam.TryGetValue(team, out var agents)
                ? agents
                : EmptyCommandAgents;
        }
    }
}
