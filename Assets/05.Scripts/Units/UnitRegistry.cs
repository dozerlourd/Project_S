using System.Collections.Generic;

namespace ProjectS.Units
{
    public static class UnitRegistry
    {
        private static readonly List<UnitCommandAgent> AllCommandAgents = new List<UnitCommandAgent>();
        private static readonly Dictionary<UnitCommandAgent, UnitTeam> AgentTeams =
            new Dictionary<UnitCommandAgent, UnitTeam>();
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
            if (agent == null || status == null)
            {
                return;
            }

            if (!AllCommandAgents.Contains(agent))
            {
                AllCommandAgents.Add(agent);
            }

            if (AgentTeams.TryGetValue(agent, out var registeredTeam))
            {
                if (registeredTeam == status.Team)
                {
                    return;
                }

                RemoveFromTeam(agent, registeredTeam);
            }

            AddToTeam(agent, status.Team);
            AgentTeams[agent] = status.Team;
        }

        public static void Unregister(UnitCommandAgent agent, PrototypeUnitStatus status)
        {
            if (agent == null)
            {
                return;
            }

            AllCommandAgents.Remove(agent);
            if (AgentTeams.TryGetValue(agent, out var registeredTeam))
            {
                RemoveFromTeam(agent, registeredTeam);
                AgentTeams.Remove(agent);
            }
            else if (status != null)
            {
                RemoveFromTeam(agent, status.Team);
            }
        }

        public static IReadOnlyList<UnitCommandAgent> GetAgents(UnitTeam team)
        {
            return CommandAgentsByTeam.TryGetValue(team, out var agents)
                ? agents
                : EmptyCommandAgents;
        }

        private static void AddToTeam(UnitCommandAgent agent, UnitTeam team)
        {
            if (!CommandAgentsByTeam.TryGetValue(team, out var teamAgents))
            {
                teamAgents = new List<UnitCommandAgent>();
                CommandAgentsByTeam.Add(team, teamAgents);
            }

            if (!teamAgents.Contains(agent))
            {
                teamAgents.Add(agent);
            }
        }

        private static void RemoveFromTeam(UnitCommandAgent agent, UnitTeam team)
        {
            if (CommandAgentsByTeam.TryGetValue(team, out var teamAgents))
            {
                teamAgents.Remove(agent);
            }
        }
    }
}
