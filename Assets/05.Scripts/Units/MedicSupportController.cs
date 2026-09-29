using UnityEngine;

namespace ProjectS.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class MedicSupportController : MonoBehaviour
    {
        [Header("Automatic Healing")]
        [SerializeField, Min(0f)] private float healRange = 4.5f;
        [SerializeField, Min(0f)] private float healAmount = 8f;
        [SerializeField, Min(0.05f)] private float healInterval = 1f;
        [SerializeField, Min(0.05f)] private float targetScanInterval = 0.2f;

        private PrototypeUnitStatus status;
        private PrototypeUnitStatus currentTarget;
        private UnitHealth currentTargetHealth;
        private float nextHealTime;
        private float nextTargetScanTime;

        public PrototypeUnitStatus CurrentTarget => currentTarget;
        public float HealRange => Mathf.Max(0f, healRange);
        public float HealAmount => Mathf.Max(0f, healAmount);
        public float HealInterval => Mathf.Max(0.05f, healInterval);

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void Update()
        {
            if (!CanProvideSupport())
            {
                ClearTarget();
                return;
            }

            if (!IsValidTarget(currentTarget, currentTargetHealth))
            {
                ClearTarget();
            }

            if (currentTarget == null && Time.time >= nextTargetScanTime)
            {
                SelectBestTarget();
                nextTargetScanTime = Time.time + Mathf.Max(0.05f, targetScanInterval);
            }

            if (currentTargetHealth == null || Time.time < nextHealTime)
            {
                return;
            }

            HealCurrentTarget();
        }

        public void Configure(float range, float amount, float interval, float scanInterval = 0.2f)
        {
            healRange = Mathf.Max(0f, range);
            healAmount = Mathf.Max(0f, amount);
            healInterval = Mathf.Max(0.05f, interval);
            targetScanInterval = Mathf.Max(0.05f, scanInterval);
            ClearTarget();
        }

        public bool TryHealNow()
        {
            if (!CanProvideSupport())
            {
                return false;
            }

            if (!IsValidTarget(currentTarget, currentTargetHealth))
            {
                ClearTarget();
                SelectBestTarget();
            }

            if (currentTargetHealth == null || Time.time < nextHealTime)
            {
                return false;
            }

            return HealCurrentTarget();
        }

        private bool CanProvideSupport()
        {
            return status != null
                && status.UnitType == PrototypeUnitType.Medic
                && status.Roles.HasFlag(UnitRole.Support)
                && status.IsAlive
                && HealRange > 0f
                && HealAmount > 0f;
        }

        private void SelectBestTarget()
        {
            var candidates = UnitAttackTargetRegistry.GetTargets(status.Team);
            PrototypeUnitStatus bestTarget = null;
            UnitHealth bestHealth = null;
            var bestHealthRatio = float.PositiveInfinity;
            var bestDistanceSquared = float.PositiveInfinity;
            var rangeSquared = HealRange * HealRange;

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index] as PrototypeUnitStatus;
                if (candidate == null || candidate == status)
                {
                    continue;
                }

                var candidateHealth = candidate.GetComponent<UnitHealth>();
                if (!IsValidTarget(candidate, candidateHealth))
                {
                    continue;
                }

                var distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared > rangeSquared)
                {
                    continue;
                }

                var healthRatio = candidateHealth.HealthRatio;
                if (healthRatio > bestHealthRatio
                    || (Mathf.Approximately(healthRatio, bestHealthRatio) && distanceSquared >= bestDistanceSquared))
                {
                    continue;
                }

                bestTarget = candidate;
                bestHealth = candidateHealth;
                bestHealthRatio = healthRatio;
                bestDistanceSquared = distanceSquared;
            }

            currentTarget = bestTarget;
            currentTargetHealth = bestHealth;
        }

        private bool IsValidTarget(PrototypeUnitStatus target, UnitHealth targetHealth)
        {
            return target != null
                && target != status
                && target.Team == status.Team
                && target.IsAlive
                && target.gameObject.activeInHierarchy
                && targetHealth != null
                && !targetHealth.IsDead
                && !targetHealth.IsAtFullHealth
                && (target.transform.position - transform.position).sqrMagnitude <= HealRange * HealRange;
        }

        private bool HealCurrentTarget()
        {
            var healedAmount = currentTargetHealth.Heal(HealAmount);
            nextHealTime = Time.time + HealInterval;
            if (currentTargetHealth.IsAtFullHealth)
            {
                ClearTarget();
            }

            return healedAmount > 0f;
        }

        private void ClearTarget()
        {
            currentTarget = null;
            currentTargetHealth = null;
        }
    }
}
