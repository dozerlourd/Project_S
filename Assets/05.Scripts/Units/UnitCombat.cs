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

            target.TakeDamage(GetAttackDamage());
            attackEffect?.PlayAttackFlash(target.SelectionTransform.position);
            nextAttackTime = Time.time + GetAttackInterval();
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
