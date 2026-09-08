using System;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingHealth : MonoBehaviour, IRecentAttackerTarget
    {
        [SerializeField, Min(1f)] private float maxHealth = 650f;

        private float currentHealth;
        private bool isDestroyed;
        private float recentAttackerTime = float.NegativeInfinity;

        public event Action<BuildingHealth> Destroyed;
        public event Action<BuildingHealth, float> HealthChanged;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public bool IsDestroyed => isDestroyed;
        public IUnitAttackTarget RecentAttacker { get; private set; }

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            isDestroyed = false;
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
