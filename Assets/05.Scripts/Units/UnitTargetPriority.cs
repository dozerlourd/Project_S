using UnityEngine;

namespace ProjectS.Units
{
    public static class UnitTargetPriority
    {
        public const float RecentAttackerMemoryDuration = 2f;

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

            var candidatePriority = GetPriority(candidate, recentAttacker);
            var currentPriority = GetPriority(currentBest, recentAttacker);
            return candidatePriority < currentPriority
                || (candidatePriority == currentPriority && candidateDistance < currentBestDistance);
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
