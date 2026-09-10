using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Visibility
{
    public interface IFogVisionProvider
    {
        ProjectS.Units.UnitTeam Team { get; }
        Transform VisionTransform { get; }
        bool IsVisionActive { get; }
        float VisionRadius { get; }
    }

    public static class FogOfWarRegistry
    {
        private sealed class ProviderRecord
        {
            public ProjectS.Units.UnitTeam Team;
            public Vector3Int PositionCell;
            public bool Active;
            public float Radius;
        }

        private static readonly List<IFogVisionProvider> Providers = new List<IFogVisionProvider>();
        private static readonly Dictionary<IFogVisionProvider, ProviderRecord> Records =
            new Dictionary<IFogVisionProvider, ProviderRecord>();
        private static readonly Dictionary<ProjectS.Units.UnitTeam, List<IFogVisionProvider>> ProvidersByTeam =
            new Dictionary<ProjectS.Units.UnitTeam, List<IFogVisionProvider>>();
        private static readonly Dictionary<ProjectS.Units.UnitTeam, int> TeamVersions =
            new Dictionary<ProjectS.Units.UnitTeam, int>();
        private static readonly List<IFogVisionProvider> EmptyProviders = new List<IFogVisionProvider>(0);

        public static IReadOnlyList<IFogVisionProvider> All => Providers;
        public static int Version { get; private set; }

        public static int GetVersion(ProjectS.Units.UnitTeam team)
        {
            return TeamVersions.TryGetValue(team, out var version) ? version : 0;
        }

        public static IReadOnlyList<IFogVisionProvider> GetProviders(ProjectS.Units.UnitTeam team)
        {
            return ProvidersByTeam.TryGetValue(team, out var providers) ? providers : EmptyProviders;
        }

        public static void Register(IFogVisionProvider provider)
        {
            if (IsMissing(provider) || provider.VisionTransform == null)
            {
                return;
            }

            var team = provider.Team;
            var positionCell = GetPositionCell(provider);
            var active = provider.IsVisionActive;
            var radius = Mathf.Max(0f, provider.VisionRadius);
            if (Records.TryGetValue(provider, out var record))
            {
                if (Matches(record, team, positionCell, active, radius))
                {
                    return;
                }

                var previousTeam = record.Team;
                Copy(team, positionCell, active, radius, record);
                if (previousTeam != team)
                {
                    RemoveFromTeam(provider, previousTeam);
                    AddToTeam(provider, team);
                }
                MarkChanged(previousTeam, team);
                return;
            }

            Providers.Add(provider);
            Records.Add(provider, new ProviderRecord
            {
                Team = team,
                PositionCell = positionCell,
                Active = active,
                Radius = radius
            });
            AddToTeam(provider, team);
            MarkChanged(team, team);
        }

        public static void Refresh(IFogVisionProvider provider)
        {
            if (IsMissing(provider) || provider.VisionTransform == null)
            {
                return;
            }

            if (!Records.TryGetValue(provider, out var record))
            {
                Register(provider);
                return;
            }

            var team = provider.Team;
            var positionCell = GetPositionCell(provider);
            var active = provider.IsVisionActive;
            var radius = Mathf.Max(0f, provider.VisionRadius);
            if (Matches(record, team, positionCell, active, radius))
            {
                return;
            }

            var previousTeam = record.Team;
            Copy(team, positionCell, active, radius, record);
            if (previousTeam != team)
            {
                RemoveFromTeam(provider, previousTeam);
                AddToTeam(provider, team);
            }
            MarkChanged(previousTeam, team);
        }

        public static void Unregister(IFogVisionProvider provider)
        {
            if (provider == null || !Records.TryGetValue(provider, out var record))
            {
                return;
            }

            Records.Remove(provider);
            Providers.Remove(provider);
            RemoveFromTeam(provider, record.Team);
            MarkChanged(record.Team, record.Team);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Providers.Clear();
            Records.Clear();
            ProvidersByTeam.Clear();
            TeamVersions.Clear();
            Version = 0;
        }

        private static Vector3Int GetPositionCell(IFogVisionProvider provider)
        {
            var world = ProjectS.Tilemaps.ProjectSTilemapWorld.ActiveInstance;
            return world != null
                ? world.WorldToCell(provider.VisionTransform.position)
                : Vector3Int.FloorToInt(provider.VisionTransform.position);
        }

        private static bool Matches(
            ProviderRecord record,
            ProjectS.Units.UnitTeam team,
            Vector3Int positionCell,
            bool active,
            float radius)
        {
            return record.Team == team
                && record.PositionCell == positionCell
                && record.Active == active
                && Mathf.Approximately(record.Radius, radius);
        }

        private static void Copy(
            ProjectS.Units.UnitTeam team,
            Vector3Int positionCell,
            bool active,
            float radius,
            ProviderRecord destination)
        {
            destination.Team = team;
            destination.PositionCell = positionCell;
            destination.Active = active;
            destination.Radius = radius;
        }

        private static void MarkChanged(
            ProjectS.Units.UnitTeam previousTeam,
            ProjectS.Units.UnitTeam currentTeam)
        {
            Version++;
            IncrementTeamVersion(previousTeam);
            if (currentTeam != previousTeam)
            {
                IncrementTeamVersion(currentTeam);
            }
        }

        private static void IncrementTeamVersion(ProjectS.Units.UnitTeam team)
        {
            TeamVersions[team] = GetVersion(team) + 1;
        }

        private static void AddToTeam(IFogVisionProvider provider, ProjectS.Units.UnitTeam team)
        {
            if (!ProvidersByTeam.TryGetValue(team, out var providers))
            {
                providers = new List<IFogVisionProvider>();
                ProvidersByTeam.Add(team, providers);
            }

            providers.Add(provider);
        }

        private static void RemoveFromTeam(IFogVisionProvider provider, ProjectS.Units.UnitTeam team)
        {
            if (!ProvidersByTeam.TryGetValue(team, out var providers))
            {
                return;
            }

            providers.Remove(provider);
            if (providers.Count == 0)
            {
                ProvidersByTeam.Remove(team);
            }
        }

        private static bool IsMissing(IFogVisionProvider provider)
        {
            return provider == null || provider is Object unityObject && unityObject == null;
        }
    }
}
