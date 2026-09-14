using System;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitHealth : MonoBehaviour, IRecentAttackerTarget
    {
        private PrototypeUnitStatus status;
        private float currentHealth;
        private bool isDead;
        private float recentAttackerTime = float.NegativeInfinity;

        public event Action<UnitHealth> Died;
        public event Action<UnitHealth, float> HealthChanged;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => status != null ? status.MaxHealth : 0f;
        public bool IsDead => isDead;
        public IUnitAttackTarget RecentAttacker { get; private set; }

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            currentHealth = status != null ? status.MaxHealth : 1f;
        }

        public void ResetHealth()
        {
            if (status == null)
            {
                status = GetComponent<PrototypeUnitStatus>();
            }

            currentHealth = status != null ? status.MaxHealth : 1f;
            isDead = false;
            RecentAttacker = null;
            recentAttackerTime = float.NegativeInfinity;
            HealthChanged?.Invoke(this, currentHealth);
        }

        public bool TryGetRecentAttacker(float maxAge, out IUnitAttackTarget attacker)
        {
            attacker = RecentAttacker;
            return attacker != null && Time.time - recentAttackerTime <= Mathf.Max(0f, maxAge);
        }

        public void RecordRecentAttacker(IUnitAttackTarget attacker)
        {
            if (attacker != null && attacker.IsAlive)
            {
                RecentAttacker = attacker;
                recentAttackerTime = Time.time;
            }
        }

        public void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            CombatFeedbackEvents.Publish(CombatFeedbackType.Damaged, transform.position, status.Team);
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(this, currentHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            CombatFeedbackEvents.Publish(CombatFeedbackType.Death, transform.position, status.Team);
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
