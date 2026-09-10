using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    public readonly struct UnitTargetQueryStatistics
    {
        public readonly int QueryCount;
        public readonly int VisitedBucketCount;
        public readonly int VisitedCandidateCount;
        public readonly int PositionUpdateCount;

        public UnitTargetQueryStatistics(
            int queryCount,
            int visitedBucketCount,
            int visitedCandidateCount,
            int positionUpdateCount)
        {
            QueryCount = queryCount;
            VisitedBucketCount = visitedBucketCount;
            VisitedCandidateCount = visitedCandidateCount;
            PositionUpdateCount = positionUpdateCount;
        }
    }

    public static class UnitAttackTargetRegistry
    {
        public const float SpatialCellSize = 4f;

        private sealed class TargetRecord
        {
            public UnitTeam Team;
            public Vector2Int Cell;
        }

        private static readonly List<IUnitAttackTarget> AllTargets = new List<IUnitAttackTarget>();
        private static readonly Dictionary<IUnitAttackTarget, TargetRecord> TargetRecords =
            new Dictionary<IUnitAttackTarget, TargetRecord>();
        private static readonly Dictionary<UnitTeam, List<IUnitAttackTarget>> TargetsByTeam =
            new Dictionary<UnitTeam, List<IUnitAttackTarget>>();
        private static readonly Dictionary<UnitTeam, Dictionary<Vector2Int, List<IUnitAttackTarget>>> SpatialTargetsByTeam =
            new Dictionary<UnitTeam, Dictionary<Vector2Int, List<IUnitAttackTarget>>>();
        private static readonly List<IUnitAttackTarget> EmptyTargets = new List<IUnitAttackTarget>(0);

        private static int queryCount;
        private static int visitedBucketCount;
        private static int visitedCandidateCount;
        private static int positionUpdateCount;

        public static IReadOnlyList<IUnitAttackTarget> All => AllTargets;

        public static void Register(IUnitAttackTarget target)
        {
            if (IsMissingTarget(target) || target.SelectionTransform == null)
            {
                return;
            }

            var team = target.Team;
            var cell = WorldToCell(target.SelectionTransform.position);
            if (TargetRecords.TryGetValue(target, out var record))
            {
                if (record.Team == team && record.Cell == cell)
                {
                    return;
                }

                RemoveFromTeam(target, record.Team);
                RemoveFromSpatialBucket(target, record.Team, record.Cell);
                record.Team = team;
                record.Cell = cell;
            }
            else
            {
                AllTargets.Add(target);
                record = new TargetRecord { Team = team, Cell = cell };
                TargetRecords.Add(target, record);
            }

            AddToTeam(target, team);
            AddToSpatialBucket(target, team, cell);
        }

        public static void RefreshPosition(IUnitAttackTarget target)
        {
            if (IsMissingTarget(target) || target.SelectionTransform == null)
            {
                return;
            }

            if (!TargetRecords.TryGetValue(target, out var record))
            {
                Register(target);
                return;
            }

            if (record.Team != target.Team)
            {
                Register(target);
                return;
            }

            var currentCell = WorldToCell(target.SelectionTransform.position);
            if (currentCell == record.Cell)
            {
                return;
            }

            RemoveFromSpatialBucket(target, record.Team, record.Cell);
            record.Cell = currentCell;
            AddToSpatialBucket(target, record.Team, currentCell);
            positionUpdateCount++;
        }

        public static void Unregister(IUnitAttackTarget target)
        {
            if (target == null)
            {
                return;
            }

            AllTargets.Remove(target);
            if (TargetRecords.TryGetValue(target, out var record))
            {
                RemoveFromTeam(target, record.Team);
                RemoveFromSpatialBucket(target, record.Team, record.Cell);
                TargetRecords.Remove(target);
            }
        }

        public static IReadOnlyList<IUnitAttackTarget> GetTargets(UnitTeam team)
        {
            return TargetsByTeam.TryGetValue(team, out var targets)
                ? targets
                : EmptyTargets;
        }

        public static void QueryNearbyEnemies(
            UnitTeam friendlyTeam,
            Vector3 center,
            float radius,
            List<IUnitAttackTarget> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            queryCount++;
            var safeRadius = Mathf.Max(0f, radius);
            var minCell = WorldToCell(center - new Vector3(safeRadius, safeRadius));
            var maxCell = WorldToCell(center + new Vector3(safeRadius, safeRadius));

            foreach (var teamEntry in SpatialTargetsByTeam)
            {
                if (teamEntry.Key == friendlyTeam)
                {
                    continue;
                }

                var buckets = teamEntry.Value;
                for (var y = minCell.y; y <= maxCell.y; y++)
                {
                    for (var x = minCell.x; x <= maxCell.x; x++)
                    {
                        if (!buckets.TryGetValue(new Vector2Int(x, y), out var bucket))
                        {
                            continue;
                        }

                        visitedBucketCount++;
                        visitedCandidateCount += bucket.Count;
                        for (var i = 0; i < bucket.Count; i++)
                        {
                            results.Add(bucket[i]);
                        }
                    }
                }
            }
        }

        public static UnitTargetQueryStatistics GetQueryStatistics()
        {
            return new UnitTargetQueryStatistics(
                queryCount,
                visitedBucketCount,
                visitedCandidateCount,
                positionUpdateCount);
        }

        public static void ResetQueryStatistics()
        {
            queryCount = 0;
            visitedBucketCount = 0;
            visitedCandidateCount = 0;
            positionUpdateCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            AllTargets.Clear();
            TargetRecords.Clear();
            TargetsByTeam.Clear();
            SpatialTargetsByTeam.Clear();
            ResetQueryStatistics();
        }

        private static Vector2Int WorldToCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / SpatialCellSize),
                Mathf.FloorToInt(position.y / SpatialCellSize));
        }

        private static void AddToTeam(IUnitAttackTarget target, UnitTeam team)
        {
            if (!TargetsByTeam.TryGetValue(team, out var teamTargets))
            {
                teamTargets = new List<IUnitAttackTarget>();
                TargetsByTeam.Add(team, teamTargets);
            }

            teamTargets.Add(target);
        }

        private static void RemoveFromTeam(IUnitAttackTarget target, UnitTeam team)
        {
            if (TargetsByTeam.TryGetValue(team, out var teamTargets))
            {
                teamTargets.Remove(target);
                if (teamTargets.Count == 0)
                {
                    TargetsByTeam.Remove(team);
                }
            }
        }

        private static void AddToSpatialBucket(IUnitAttackTarget target, UnitTeam team, Vector2Int cell)
        {
            if (!SpatialTargetsByTeam.TryGetValue(team, out var buckets))
            {
                buckets = new Dictionary<Vector2Int, List<IUnitAttackTarget>>();
                SpatialTargetsByTeam.Add(team, buckets);
            }

            if (!buckets.TryGetValue(cell, out var bucket))
            {
                bucket = new List<IUnitAttackTarget>();
                buckets.Add(cell, bucket);
            }

            bucket.Add(target);
        }

        private static void RemoveFromSpatialBucket(IUnitAttackTarget target, UnitTeam team, Vector2Int cell)
        {
            if (!SpatialTargetsByTeam.TryGetValue(team, out var buckets)
                || !buckets.TryGetValue(cell, out var bucket))
            {
                return;
            }

            bucket.Remove(target);
            if (bucket.Count == 0)
            {
                buckets.Remove(cell);
                if (buckets.Count == 0)
                {
                    SpatialTargetsByTeam.Remove(team);
                }
            }
        }

        private static bool IsMissingTarget(IUnitAttackTarget target)
        {
            return target == null || target is Object unityObject && unityObject == null;
        }
    }
}
