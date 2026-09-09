using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    public enum UnitUpgradeKind
    {
        AttackDamage,
        MovementSpeed
    }

    public static class UnitUpgradeStatModifiers
    {
        private static readonly Dictionary<UnitTeam, float> AttackDamageBonuses = new Dictionary<UnitTeam, float>();
        private static readonly Dictionary<UnitTeam, float> MovementSpeedMultipliers = new Dictionary<UnitTeam, float>();

        public static float GetAttackDamageBonus(UnitTeam team)
        {
            return AttackDamageBonuses.TryGetValue(team, out var bonus) ? bonus : 0f;
        }

        public static float GetMovementSpeedMultiplier(UnitTeam team)
        {
            return MovementSpeedMultipliers.TryGetValue(team, out var multiplier) ? multiplier : 1f;
        }

        public static void SetUpgrade(UnitTeam team, UnitUpgradeKind kind, float value)
        {
            switch (kind)
            {
                case UnitUpgradeKind.AttackDamage:
                    AttackDamageBonuses[team] = Mathf.Max(0f, value);
                    break;
                case UnitUpgradeKind.MovementSpeed:
                    MovementSpeedMultipliers[team] = Mathf.Max(0.01f, 1f + value);
                    break;
            }
        }

        public static void ClearTeam(UnitTeam team)
        {
            AttackDamageBonuses.Remove(team);
            MovementSpeedMultipliers.Remove(team);
        }
    }
}
