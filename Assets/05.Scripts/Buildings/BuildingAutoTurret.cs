using System.Collections.Generic;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingAutoTurret : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackRange = 6f;
        [SerializeField, Min(0.1f)] private float attackDamage = 14f;
        [SerializeField, Min(0.1f)] private float attacksPerSecond = 1f;
        [SerializeField, Min(0.02f)] private float targetScanInterval = 0.2f;
        [SerializeField, Min(0f)] private float targetScanJitter = 0.05f;
        [SerializeField, Min(1f)] private float targetRetentionRangeMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float targetSwitchDistanceAdvantage = 0.4f;

        private BuildingStatus status;
        private float nextAttackTime;
        private float nextTargetScanTime;
        private IUnitAttackTarget currentTarget;
        private readonly List<IUnitAttackTarget> targetCandidates = new List<IUnitAttackTarget>();

        private void Awake()
        {
            status = GetComponent<BuildingStatus>();
        }

        private void OnEnable()
        {
            ScheduleNextTargetScan(true);
        }

        private void Update()
        {
            if (status == null || !status.Completed)
            {
                return;
            }

            RefreshTarget();
            if (currentTarget == null || Time.time < nextAttackTime || !IsInAttackRange(currentTarget))
            {
                return;
            }

            UnitTargetPriority.RecordRecentAttacker(currentTarget, status);
            CombatFeedbackEvents.Publish(
                CombatFeedbackType.AttackHit,
                currentTarget.SelectionTransform.position,
                currentTarget.Team,
                status.Team);
            currentTarget.TakeDamage(attackDamage, status);
            nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, attacksPerSecond);
        }

        private void RefreshTarget()
        {
            if (!IsValidUnitTarget(currentTarget)
                || GetTargetDistance(currentTarget) > attackRange * Mathf.Max(1f, targetRetentionRangeMultiplier))
            {
                currentTarget = null;
            }

            if (Time.time < nextTargetScanTime)
            {
                return;
            }

            ScheduleNextTargetScan(false);
            var recentAttacker = UnitTargetPriority.GetRecentAttacker(status);
            IUnitAttackTarget bestTarget = null;
            var bestDistance = float.PositiveInfinity;
            UnitAttackTargetRegistry.QueryNearbyEnemies(status.Team, transform.position, attackRange, targetCandidates);
            for (var i = 0; i < targetCandidates.Count; i++)
            {
                var candidate = targetCandidates[i];
                if (!IsValidUnitTarget(candidate))
                {
                    continue;
                }

                var distance = GetTargetDistance(candidate);
                if (distance > attackRange
                    || !UnitTargetPriority.IsPreferredTarget(
                        candidate,
                        distance,
                        bestTarget,
                        bestDistance,
                        recentAttacker))
                {
                    continue;
                }

                bestTarget = candidate;
                bestDistance = distance;
            }

            if (bestTarget == null)
            {
                return;
            }

            if (currentTarget == null
                || !IsInAttackRange(currentTarget)
                || UnitTargetPriority.ShouldSwitchTarget(
                    currentTarget,
                    GetTargetDistance(currentTarget),
                    bestTarget,
                    bestDistance,
                    recentAttacker,
                    targetSwitchDistanceAdvantage))
            {
                currentTarget = bestTarget;
            }
        }

        private bool IsValidUnitTarget(IUnitAttackTarget target)
        {
            return target is PrototypeUnitStatus
                && target.Team != status.Team
                && target.IsAlive
                && target.SelectionTransform != null
                && target.SelectionGameObject != null
                && target.SelectionGameObject.activeInHierarchy;
        }

        private bool IsInAttackRange(IUnitAttackTarget target)
        {
            return GetTargetDistance(target) <= attackRange;
        }

        private float GetTargetDistance(IUnitAttackTarget target)
        {
            return target != null && target.SelectionTransform != null
                ? Vector3.Distance(transform.position, target.SelectionTransform.position)
                : float.PositiveInfinity;
        }

        private void ScheduleNextTargetScan(bool initial)
        {
            var jitter = Mathf.Max(0f, targetScanJitter);
            var randomOffset = jitter > 0f ? Random.Range(0f, jitter) : 0f;
            nextTargetScanTime = Time.time
                + (initial ? randomOffset : Mathf.Max(0.02f, targetScanInterval) + randomOffset);
        }
    }
}
