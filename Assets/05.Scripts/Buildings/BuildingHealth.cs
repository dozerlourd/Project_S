using System;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 650f;

        private float currentHealth;
        private bool isDestroyed;

        public event Action<BuildingHealth> Destroyed;
        public event Action<BuildingHealth, float> HealthChanged;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public bool IsDestroyed => isDestroyed;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            isDestroyed = false;
            HealthChanged?.Invoke(this, currentHealth);
        }

        public void TakeDamage(float amount)
        {
            if (isDestroyed || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(this, currentHealth);

            if (currentHealth <= 0f)
            {
                DestroyBuilding();
            }
        }

        private void DestroyBuilding()
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed = true;
            Destroyed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
