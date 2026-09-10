namespace ProjectS.Units
{
    public enum UnitEngagementStyle
    {
        Standard,
        AreaPressure,
        KeepDistance,
        Artillery,
        CloseAssault
    }

    public readonly struct UnitTacticalBehaviorProfile
    {
        public readonly UnitEngagementStyle EngagementStyle;
        public readonly float AttackEntryRangeRatio;
        public readonly float RetreatTriggerRangeRatio;
        public readonly float RetreatReleaseRangeRatio;
        public readonly float TargetRepathIntervalMultiplier;

        public UnitTacticalBehaviorProfile(
            UnitEngagementStyle engagementStyle,
            float attackEntryRangeRatio,
            float retreatTriggerRangeRatio,
            float retreatReleaseRangeRatio,
            float targetRepathIntervalMultiplier)
        {
            EngagementStyle = engagementStyle;
            AttackEntryRangeRatio = attackEntryRangeRatio;
            RetreatTriggerRangeRatio = retreatTriggerRangeRatio;
            RetreatReleaseRangeRatio = retreatReleaseRangeRatio;
            TargetRepathIntervalMultiplier = targetRepathIntervalMultiplier;
        }
    }

    public static class UnitTacticalBehaviorProfiles
    {
        private static readonly UnitTacticalBehaviorProfile Standard =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.Standard, 1f, 0f, 0f, 1f);
        private static readonly UnitTacticalBehaviorProfile AreaPressure =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.AreaPressure, 1f, 0f, 0f, 1f);
        private static readonly UnitTacticalBehaviorProfile KeepDistance =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.KeepDistance, 1f, 0.45f, 0.72f, 1f);
        private static readonly UnitTacticalBehaviorProfile Artillery =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.Artillery, 1f, 0f, 0f, 1.15f);
        private static readonly UnitTacticalBehaviorProfile StrikerAssault =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.CloseAssault, 0.8f, 0f, 0f, 0.6f);
        private static readonly UnitTacticalBehaviorProfile SwarmAssault =
            new UnitTacticalBehaviorProfile(UnitEngagementStyle.CloseAssault, 0.85f, 0f, 0f, 0.7f);

        public static UnitTacticalBehaviorProfile Get(PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Spliter:
                    return AreaPressure;
                case PrototypeUnitType.Ranger:
                    return KeepDistance;
                case PrototypeUnitType.Tank:
                    return Artillery;
                case PrototypeUnitType.Striker:
                    return StrikerAssault;
                case PrototypeUnitType.Swarm:
                    return SwarmAssault;
                default:
                    return Standard;
            }
        }
    }
}
