using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    public sealed class UnitCombat : MonoBehaviour
    {
        private PrototypeUnitStatus status;
        private UnitCommandAgent commandAgent;
        private TemporaryAttackEffect attackEffect;
        private Collider2D attackCollider;
        private float nextAttackTime;
        private readonly List<IUnitAttackTarget> attackTargets = new List<IUnitAttackTarget>();

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            commandAgent = GetComponent<UnitCommandAgent>();
            attackEffect = GetComponent<TemporaryAttackEffect>();
            attackCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            var target = commandAgent.PriorityTarget;
            if (target == null
                || target.SelectionTransform == null
                || commandAgent.ActionState != UnitActionState.AttackingTarget
                || !CanAttack()
                || Time.time < nextAttackTime)
            {
                return;
            }

            if (!target.IsAlive || !IsTargetInRange(target))
            {
                return;
            }

            ApplyAttack(target);
            attackEffect?.PlayAttackFlash(target.SelectionTransform.position);
            nextAttackTime = Time.time + GetAttackInterval();
        }

        private void ApplyAttack(IUnitAttackTarget primaryTarget)
        {
            CollectAttackTargets(primaryTarget);
            for (var i = 0; i < attackTargets.Count; i++)
            {
                var target = attackTargets[i];
                UnitTargetPriority.RecordRecentAttacker(target, status);
                target.TakeDamage(GetAttackDamage(target), status);
            }
        }

        private void CollectAttackTargets(IUnitAttackTarget primaryTarget)
        {
            attackTargets.Clear();
            if (!IsValidEnemyTarget(primaryTarget))
            {
                return;
            }

            attackTargets.Add(primaryTarget);
            if (!HasAreaAttack())
            {
                return;
            }

            var center = primaryTarget.SelectionTransform.position;
            var maxTargets = Mathf.Max(1, status.MaxAttackTargets);
            var radiusSquared = status.AttackArea * status.AttackArea;
            var allTargets = UnitAttackTargetRegistry.All;
            for (var i = 0; i < allTargets.Count; i++)
            {
                var candidate = allTargets[i];
                if (candidate == primaryTarget
                    || !IsValidEnemyTarget(candidate)
                    || (candidate.SelectionTransform.position - center).sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                attackTargets.Add(candidate);
            }

            attackTargets.Sort((left, right) => CompareTargets(primaryTarget, center, left, right));
            if (attackTargets.Count > maxTargets)
            {
                attackTargets.RemoveRange(maxTargets, attackTargets.Count - maxTargets);
            }
        }

        private bool HasAreaAttack()
        {
            return (status.AttackTargetType == AttackTargetType.AreaAttack || status.HasAreaAttack)
                && status.AttackArea > 0f
                && status.MaxAttackTargets > 1;
        }

        private bool IsValidEnemyTarget(IUnitAttackTarget target)
        {
            return target != null
                && target.IsAlive
                && target.Team != status.Team
                && target.SelectionTransform != null
                && target.SelectionGameObject != null
                && target.SelectionGameObject.activeInHierarchy;
        }

        private static int CompareTargets(
            IUnitAttackTarget primaryTarget,
            Vector3 center,
            IUnitAttackTarget left,
            IUnitAttackTarget right)
        {
            if (left == primaryTarget)
            {
                return right == primaryTarget ? 0 : -1;
            }

            if (right == primaryTarget)
            {
                return 1;
            }

            var leftDistance = (left.SelectionTransform.position - center).sqrMagnitude;
            var rightDistance = (right.SelectionTransform.position - center).sqrMagnitude;
            var distanceComparison = leftDistance.CompareTo(rightDistance);
            return distanceComparison != 0
                ? distanceComparison
                : left.SelectionGameObject.GetInstanceID().CompareTo(right.SelectionGameObject.GetInstanceID());
        }

        private bool CanAttack()
        {
            return status.Roles.HasFlag(UnitRole.Combat)
                || status.PhysicalAttackPower > 0f
                || status.MagicalAttackPower > 0f;
        }

        private bool IsTargetInRange(IUnitAttackTarget target)
        {
            if (target == null || target.SelectionTransform == null)
            {
                return false;
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            var targetCollider = target.AttackCollider;
            if (attackCollider != null && targetCollider != null)
            {
                var colliderDistance = attackCollider.Distance(targetCollider);
                if (colliderDistance.isValid)
                {
                    return colliderDistance.distance <= status.AttackRange;
                }
            }

            return Vector3.Distance(transform.position, target.SelectionTransform.position) <= status.AttackRange;
        }

        private float GetAttackDamage(IUnitAttackTarget target)
        {
            var baseDamage = Mathf.Max(status.PhysicalAttackPower, status.MagicalAttackPower);
            var targetStatus = target as PrototypeUnitStatus;
            return targetStatus == null
                ? baseDamage
                : baseDamage * UnitCombatRules.GetDamageMultiplier(status.UnitType, targetStatus.UnitType);
        }

        private float GetAttackInterval()
        {
            return status.AttackSpeed > 0f ? 1f / status.AttackSpeed : 1f;
        }
    }
}
