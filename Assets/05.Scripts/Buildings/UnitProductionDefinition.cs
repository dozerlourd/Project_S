using System;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [Serializable]
    public sealed class UnitProductionDefinition
    {
        [SerializeField] private string displayName = "Unit";
        [SerializeField] private PrototypeUnitType unitType = PrototypeUnitType.Soldier;
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private ResourceAmount cost = new ResourceAmount(50, 0);
        [SerializeField, Min(0.1f)] private float productionTime = 6f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? unitType.ToString() : displayName;
        public PrototypeUnitType UnitType => unitType;
        public GameObject UnitPrefab => unitPrefab;
        public ResourceAmount Cost => cost;
        public float ProductionTime => Mathf.Max(0.1f, productionTime);

        public void Configure(
            string name,
            PrototypeUnitType type,
            GameObject prefab,
            ResourceAmount resourceCost,
            float duration)
        {
            displayName = name;
            unitType = type;
            unitPrefab = prefab;
            cost = resourceCost;
            productionTime = Mathf.Max(0.1f, duration);
        }
    }
}
