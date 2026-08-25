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
                || commandAgent.ActionState != UnitActionState.AttackingTarget
                || !CanAttack()
                || Time.time < nextAttackTime)
            {
                return;
            }

            var targetHealth = target.GetComponent<UnitHealth>();
            if (targetHealth == null || targetHealth.IsDead || !IsTargetInRange(target))
            {
                return;
            }

            targetHealth.TakeDamage(GetAttackDamage());
            attackEffect?.PlayAttackFlash(target.transform.position);
            nextAttackTime = Time.time + GetAttackInterval();
        }

        private bool CanAttack()
        {
            return status.Roles.HasFlag(UnitRole.Combat)
                || status.PhysicalAttackPower > 0f
                || status.MagicalAttackPower > 0f;
        }

        private bool IsTargetInRange(PrototypeUnitStatus target)
        {
            if (target == null)
            {
                return false;
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            var targetCollider = target.GetComponent<Collider2D>();
            if (attackCollider != null && targetCollider != null)
            {
                var colliderDistance = attackCollider.Distance(targetCollider);
                if (colliderDistance.isValid)
                {
                    return colliderDistance.distance <= status.AttackRange;
                }
            }

            return Vector3.Distance(transform.position, target.transform.position) <= status.AttackRange;
        }

        private float GetAttackDamage()
        {
            return Mathf.Max(status.PhysicalAttackPower, status.MagicalAttackPower);
        }

        private float GetAttackInterval()
        {
            return status.AttackSpeed > 0f ? 1f / status.AttackSpeed : 1f;
        }
    }
}
