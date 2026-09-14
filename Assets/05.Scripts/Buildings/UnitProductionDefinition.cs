using System;
using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Unlocks;
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
        [SerializeField, Min(0)] private int supplyCost = 1;
        [SerializeField, Min(0.1f)] private float productionTime = 6f;
        [SerializeField, Min(1)] private int unitsPerProduction = 1;
        [SerializeField] private UnitProductionRequirement[] requirements = new UnitProductionRequirement[0];
        [SerializeField] private UnlockRequirement[] unlockRequirements = new UnlockRequirement[0];
        [SerializeField] private BuildingKind[] allowedProductionBuildings = new BuildingKind[0];

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? unitType.ToString() : displayName;
        public PrototypeUnitType UnitType => unitType;
        public GameObject UnitPrefab => unitPrefab;
        public ResourceAmount Cost => cost;
        public int SupplyCost => Mathf.Max(0, supplyCost);
        public float ProductionTime => Mathf.Max(0.1f, productionTime);
        public int UnitsPerProduction => Mathf.Max(1, unitsPerProduction);
        public IReadOnlyList<UnitProductionRequirement> Requirements => requirements;
        public IReadOnlyList<UnlockRequirement> UnlockRequirements => unlockRequirements;
        public IReadOnlyList<BuildingKind> AllowedProductionBuildings => allowedProductionBuildings;

        public void Configure(
            string name,
            PrototypeUnitType type,
            GameObject prefab,
            ResourceAmount resourceCost,
            float duration,
            int requiredSupply = 1,
            int outputCount = 1,
            UnitProductionRequirement[] productionRequirements = null)
        {
            displayName = name;
            unitType = type;
            unitPrefab = prefab;
            cost = resourceCost;
            productionTime = Mathf.Max(0.1f, duration);
            supplyCost = Mathf.Max(0, requiredSupply);
            unitsPerProduction = Mathf.Max(1, outputCount);
            requirements = productionRequirements ?? new UnitProductionRequirement[0];
        }

        public void ConfigureAllowedProductionBuildings(BuildingKind[] buildingKinds)
        {
            allowedProductionBuildings = buildingKinds ?? new BuildingKind[0];
        }

        public void ConfigureUnlockRequirements(UnlockRequirement[] productionUnlockRequirements)
        {
            unlockRequirements = productionUnlockRequirements ?? new UnlockRequirement[0];
        }

        public bool CanBeProducedBy(UnitTeam team, out string failureReason)
        {
            return UnlockRequirement.AreMet(unlockRequirements, team, "produce", DisplayName, out failureReason);
        }

        public bool CanBeProducedAt(BuildingKind buildingKind)
        {
            if (allowedProductionBuildings == null || allowedProductionBuildings.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < allowedProductionBuildings.Length; i++)
            {
                if (allowedProductionBuildings[i] == buildingKind)
                {
                    return true;
                }
            }

            return false;
        }

        public string GetProductionBuildingFailureReason(BuildingKind buildingKind)
        {
            var allowedNames = string.Empty;
            for (var i = 0; i < allowedProductionBuildings.Length; i++)
            {
                if (i > 0)
                {
                    allowedNames += ", ";
                }

                allowedNames += allowedProductionBuildings[i];
            }

            return $"Cannot enqueue {DisplayName}: cannot be produced at {buildingKind}. Allowed building(s): {allowedNames}.";
        }
    }
}
