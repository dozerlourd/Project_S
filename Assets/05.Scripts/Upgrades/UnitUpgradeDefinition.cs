using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Upgrades
{
    [CreateAssetMenu(menuName = "Project S/Upgrades/Unit Upgrade Definition", fileName = "UnitUpgrade")]
    public sealed class UnitUpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Unit Upgrade";
        [SerializeField] private UnitUpgradeKind upgradeKind;
        [SerializeField] private ResourceAmount cost = new ResourceAmount(100, 25);
        [SerializeField, Min(0.1f)] private float researchDuration = 10f;
        [SerializeField, Min(0f)] private float value = 1f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? upgradeKind.ToString() : displayName;
        public UnitUpgradeKind UpgradeKind => upgradeKind;
        public ResourceAmount Cost => cost;
        public float ResearchDuration => Mathf.Max(0.1f, researchDuration);
        public float Value => Mathf.Max(0f, value);

        public void Configure(
            string upgradeDisplayName,
            UnitUpgradeKind kind,
            ResourceAmount upgradeCost,
            float duration,
            float upgradeValue)
        {
            displayName = upgradeDisplayName;
            upgradeKind = kind;
            cost = upgradeCost;
            researchDuration = Mathf.Max(0.1f, duration);
            value = Mathf.Max(0f, upgradeValue);
        }
    }
}
