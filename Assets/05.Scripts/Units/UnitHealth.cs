using System;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitHealth : MonoBehaviour
    {
        private PrototypeUnitStatus status;
        private float currentHealth;
        private bool isDead;

        public event Action<UnitHealth> Died;
        public event Action<UnitHealth, float> HealthChanged;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => status != null ? status.MaxHealth : 0f;
        public bool IsDead => isDead;

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
            HealthChanged?.Invoke(this, currentHealth);
        }

        public void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

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
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
