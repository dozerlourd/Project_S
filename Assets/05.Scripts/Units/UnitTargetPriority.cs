using UnityEngine;

namespace ProjectS.Units
{
    public static class UnitTargetPriority
    {
        public const float RecentAttackerMemoryDuration = 2f;
        private const float ThreatDistanceTolerance = 0.75f;
        private const float ThreatSwitchMultiplier = 1.25f;

        public static bool IsPreferredTarget(
            IUnitAttackTarget candidate,
            float candidateDistance,
            IUnitAttackTarget currentBest,
            float currentBestDistance,
            IUnitAttackTarget recentAttacker)
        {
            if (candidate == null)
            {
                return false;
            }

            if (currentBest == null)
            {
                return true;
            }

            return CompareTargets(
                candidate,
                candidateDistance,
                currentBest,
                currentBestDistance,
                recentAttacker) < 0;
        }

        public static bool ShouldSwitchTarget(
            IUnitAttackTarget currentTarget,
            float currentDistance,
            IUnitAttackTarget candidate,
            float candidateDistance,
            IUnitAttackTarget recentAttacker,
            float requiredDistanceAdvantage)
        {
            if (candidate == null || ReferenceEquals(candidate, currentTarget))
            {
                return false;
            }

            if (currentTarget == null)
            {
                return true;
            }

            var candidatePriority = GetPriority(candidate, recentAttacker);
            var currentPriority = GetPriority(currentTarget, recentAttacker);
            if (candidatePriority != currentPriority)
            {
                return candidatePriority < currentPriority;
            }

            if (candidateDistance + Mathf.Max(0f, requiredDistanceAdvantage) < currentDistance)
            {
                return true;
            }

            var currentThreat = GetThreatScore(currentTarget);
            var candidateThreat = GetThreatScore(candidate);
            return candidateDistance <= currentDistance + ThreatDistanceTolerance
                && candidateThreat > currentThreat * ThreatSwitchMultiplier;
        }

        public static AttackTargetPriority GetPriority(IUnitAttackTarget target, IUnitAttackTarget recentAttacker = null)
        {
            if (ReferenceEquals(target, recentAttacker))
            {
                return AttackTargetPriority.CurrentAttacker;
            }

            if (target is IAttackTargetPriorityProvider provider)
            {
                return provider.TargetPriority;
            }

            if (target is PrototypeUnitStatus unit)
            {
                return unit.Roles.HasFlag(UnitRole.Combat)
                    || unit.PhysicalAttackPower > 0f
                    || unit.MagicalAttackPower > 0f
                    ? AttackTargetPriority.CombatUnit
                    : AttackTargetPriority.WorkerUnit;
            }

            return AttackTargetPriority.Other;
        }

        public static float GetThreatScore(IUnitAttackTarget target)
        {
            if (target is PrototypeUnitStatus unit)
            {
                var damage = Mathf.Max(unit.PhysicalAttackPower, unit.MagicalAttackPower);
                return damage * Mathf.Max(0.1f, unit.AttackSpeed) + unit.AttackRange * 0.1f;
            }

            switch (GetPriority(target))
            {
                case AttackTargetPriority.DefensiveBuilding:
                    return 100f;
                case AttackTargetPriority.ProductionBuilding:
                    return 50f;
                case AttackTargetPriority.MainBase:
                    return 25f;
                default:
                    return 0f;
            }
        }

        private static int CompareTargets(
            IUnitAttackTarget left,
            float leftDistance,
            IUnitAttackTarget right,
            float rightDistance,
            IUnitAttackTarget recentAttacker)
        {
            var priorityComparison = GetPriority(left, recentAttacker).CompareTo(GetPriority(right, recentAttacker));
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            if (Mathf.Abs(leftDistance - rightDistance) <= ThreatDistanceTolerance)
            {
                var threatComparison = GetThreatScore(right).CompareTo(GetThreatScore(left));
                if (threatComparison != 0)
                {
                    return threatComparison;
                }
            }

            var distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return GetStableId(left).CompareTo(GetStableId(right));
        }

        private static int GetStableId(IUnitAttackTarget target)
        {
            var targetObject = target != null ? target.SelectionGameObject : null;
            return targetObject != null ? targetObject.GetInstanceID() : int.MaxValue;
        }

        public static IUnitAttackTarget GetRecentAttacker(IUnitAttackTarget target)
        {
            var targetObject = target != null ? target.SelectionGameObject : null;
            if (targetObject == null)
            {
                return null;
            }

            var behaviours = targetObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRecentAttackerTarget recentAttackerTarget
                    && recentAttackerTarget.TryGetRecentAttacker(RecentAttackerMemoryDuration, out var attacker)
                    && IsValidRecentAttacker(target, attacker))
                {
                    return attacker;
                }
            }

            return null;
        }

        public static void RecordRecentAttacker(IUnitAttackTarget target, IUnitAttackTarget attacker)
        {
            if (target == null || attacker == null || ReferenceEquals(target, attacker))
            {
                return;
            }

            var targetObject = target.SelectionGameObject;
            if (targetObject == null)
            {
                return;
            }

            var behaviours = targetObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRecentAttackerTarget recentAttackerTarget)
                {
                    recentAttackerTarget.RecordRecentAttacker(attacker);
                }
            }
        }

        private static bool IsValidRecentAttacker(IUnitAttackTarget target, IUnitAttackTarget attacker)
        {
            var attackerObject = attacker != null ? attacker.SelectionGameObject : null;
            return target != null
                && attacker != null
                && !ReferenceEquals(target, attacker)
                && attacker.IsAlive
                && attacker.Team != target.Team
                && attackerObject != null
                && attackerObject.activeInHierarchy;
        }
    }
}
