namespace ProjectS.Units
{
    public static class UnitCombatRules
    {
        public const float AdvantageDamageMultiplier = 1.25f;
        public const float DisadvantageDamageMultiplier = 0.8f;
        public const float NeutralDamageMultiplier = 1f;

        public static float GetDamageMultiplier(PrototypeUnitType attacker, PrototypeUnitType defender)
        {
            if (HasAdvantage(attacker, defender))
            {
                return AdvantageDamageMultiplier;
            }

            if (HasAdvantage(defender, attacker))
            {
                return DisadvantageDamageMultiplier;
            }

            return NeutralDamageMultiplier;
        }

        private static bool HasAdvantage(PrototypeUnitType attacker, PrototypeUnitType defender)
        {
            switch (attacker)
            {
                case PrototypeUnitType.Soldier:
                    return defender == PrototypeUnitType.Spliter;
                case PrototypeUnitType.Spliter:
                    return defender == PrototypeUnitType.Ranger;
                case PrototypeUnitType.Ranger:
                    return defender == PrototypeUnitType.Soldier;
                case PrototypeUnitType.Tank:
                    return defender == PrototypeUnitType.Swarm;
                case PrototypeUnitType.Striker:
                    return defender == PrototypeUnitType.Tank;
                case PrototypeUnitType.Swarm:
                    return defender == PrototypeUnitType.Striker;
                default:
                    return false;
            }
        }
    }
}
