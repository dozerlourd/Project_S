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

        private BuildingStatus status;
        private float nextAttackTime;

        private void Awake()
        {
            status = GetComponent<BuildingStatus>();
        }

        private void Update()
        {
            if (status == null || !status.Completed || Time.time < nextAttackTime)
            {
                return;
            }

            var target = FindNearestEnemy();
            if (target == null)
            {
                return;
            }

            target.TakeDamage(attackDamage, status);
            nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, attacksPerSecond);
        }

        private IUnitAttackTarget FindNearestEnemy()
        {
            IUnitAttackTarget nearest = null;
            var nearestDistance = attackRange;
            var targets = UnitAttackTargetRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var candidate = targets[i];
                if (candidate == null
                    || !(candidate is PrototypeUnitStatus)
                    || candidate.Team == status.Team
                    || !candidate.IsAlive
                    || candidate.SelectionTransform == null
                    || candidate.SelectionGameObject == null
                    || !candidate.SelectionGameObject.activeInHierarchy)
                {
                    continue;
                }

                var distance = Vector3.Distance(transform.position, candidate.SelectionTransform.position);
                if (distance > nearestDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
