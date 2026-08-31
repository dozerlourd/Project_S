using System.Collections.Generic;

namespace ProjectS.Units
{
    public static class UnitAttackTargetRegistry
    {
        private static readonly List<IUnitAttackTarget> AllTargets = new List<IUnitAttackTarget>();
        private static readonly Dictionary<IUnitAttackTarget, UnitTeam> TargetTeams =
            new Dictionary<IUnitAttackTarget, UnitTeam>();
        private static readonly Dictionary<UnitTeam, List<IUnitAttackTarget>> TargetsByTeam =
            new Dictionary<UnitTeam, List<IUnitAttackTarget>>();
        private static readonly List<IUnitAttackTarget> EmptyTargets = new List<IUnitAttackTarget>(0);

        public static IReadOnlyList<IUnitAttackTarget> All => AllTargets;

        public static void Register(IUnitAttackTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (!AllTargets.Contains(target))
            {
                AllTargets.Add(target);
            }

            if (TargetTeams.TryGetValue(target, out var registeredTeam))
            {
                if (registeredTeam == target.Team)
                {
                    return;
                }

                RemoveFromTeam(target, registeredTeam);
            }

            AddToTeam(target, target.Team);
            TargetTeams[target] = target.Team;
        }

        public static void Unregister(IUnitAttackTarget target)
        {
            if (target == null)
            {
                return;
            }

            AllTargets.Remove(target);
            if (TargetTeams.TryGetValue(target, out var registeredTeam))
            {
                RemoveFromTeam(target, registeredTeam);
                TargetTeams.Remove(target);
            }
            else
            {
                RemoveFromTeam(target, target.Team);
            }
        }

        public static IReadOnlyList<IUnitAttackTarget> GetTargets(UnitTeam team)
        {
            return TargetsByTeam.TryGetValue(team, out var targets)
                ? targets
                : EmptyTargets;
        }

        private static void AddToTeam(IUnitAttackTarget target, UnitTeam team)
        {
            if (!TargetsByTeam.TryGetValue(team, out var teamTargets))
            {
                teamTargets = new List<IUnitAttackTarget>();
                TargetsByTeam.Add(team, teamTargets);
            }

            if (!teamTargets.Contains(target))
            {
                teamTargets.Add(target);
            }
        }

        private static void RemoveFromTeam(IUnitAttackTarget target, UnitTeam team)
        {
            if (TargetsByTeam.TryGetValue(team, out var teamTargets))
            {
                teamTargets.Remove(target);
            }
        }
    }
}
