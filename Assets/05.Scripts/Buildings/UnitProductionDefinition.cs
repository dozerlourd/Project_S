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
        // Keep this serialized name for existing definitions: it is the standard supply cost.
        [SerializeField, Min(0)] private int supplyCost = 1;
        [SerializeField, Min(0)] private int advancedSupplyCost;
        [SerializeField, Min(0.1f)] private float productionTime = 6f;
        [SerializeField, Min(1)] private int unitsPerProduction = 1;
        [Header("Additional Production Conditions")]
        [Tooltip("All listed conditions must be met before resources or supply are reserved.")]
        [SerializeField] private UnitProductionRequirement[] requirements = new UnitProductionRequirement[0];
        [Tooltip("Optional team unlock conditions. These are evaluated with the building conditions above.")]
        [SerializeField] private UnlockRequirement[] unlockRequirements = new UnlockRequirement[0];
        [SerializeField] private BuildingKind[] allowedProductionBuildings = new BuildingKind[0];

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? unitType.ToString() : displayName;
        public PrototypeUnitType UnitType => unitType;
        public GameObject UnitPrefab => unitPrefab;
        public ResourceAmount Cost => cost;
        public int SupplyCost => Mathf.Max(0, supplyCost);
        public int AdvancedSupplyCost => Mathf.Max(0, advancedSupplyCost);
        public SupplyKind SupplyCategory => AdvancedSupplyCost > 0 && SupplyCost == 0 ? SupplyKind.Advanced : SupplyKind.Standard;
        public SupplyAmount SupplyRequirement => new SupplyAmount(SupplyCost, AdvancedSupplyCost);
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
            advancedSupplyCost = 0;
            unitsPerProduction = Mathf.Max(1, outputCount);
            requirements = productionRequirements ?? new UnitProductionRequirement[0];
        }

        public void ConfigureSupplyRequirement(SupplyKind kind, int requiredSupply)
        {
            ConfigureSupplyRequirement(kind == SupplyKind.Advanced ? 0 : requiredSupply,
                kind == SupplyKind.Advanced ? requiredSupply : 0);
        }

        public void ConfigureSupplyRequirement(int requiredStandardSupply, int requiredAdvancedSupply)
        {
            supplyCost = Mathf.Max(0, requiredStandardSupply);
            advancedSupplyCost = Mathf.Max(0, requiredAdvancedSupply);
        }

        public void ConfigureSupplyRequirement(SupplyAmount amount)
        {
            ConfigureSupplyRequirement(amount.Standard, amount.Advanced);
        }

        public void ConfigureAllowedProductionBuildings(BuildingKind[] buildingKinds)
        {
            allowedProductionBuildings = buildingKinds ?? new BuildingKind[0];
        }

        public void ConfigureProductionRequirements(UnitProductionRequirement[] productionRequirements)
        {
            requirements = productionRequirements ?? new UnitProductionRequirement[0];
        }

        public void ConfigureUnlockRequirements(UnlockRequirement[] productionUnlockRequirements)
        {
            unlockRequirements = productionUnlockRequirements ?? new UnlockRequirement[0];
        }

        public void ApplyBalanceCatalog(UnitBalanceCatalog catalog = null)
        {
            catalog = catalog != null ? catalog : UnitBalanceCatalog.LoadDefault();
            UnitBalanceEntry entry;
            if (catalog != null && catalog.TryGetEntry(unitType, out entry))
            {
                ApplyBalanceEntry(entry);
                return;
            }

            ApplyBalanceEntry(UnitBalanceEntry.CreateFallback(unitType));
        }

        private void ApplyBalanceEntry(UnitBalanceEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Configure(
                entry.ProductionDisplayName,
                entry.UnitType,
                unitPrefab,
                new ResourceAmount(entry.ProductionMineralCost, entry.ProductionGasCost),
                entry.ProductionTime,
                entry.SupplyUsage.Standard,
                entry.UnitsPerProduction,
                CreateProductionRequirements(entry.AllowedProductionBuildings));
            ConfigureSupplyRequirement(entry.SupplyUsage);
            ConfigureAllowedProductionBuildings(CreateAllowedProductionBuildings(entry.AllowedProductionBuildings));
            ConfigureUnlockRequirements(CreateUnlockRequirements(entry.UnlockRequirementIds));
        }

        public bool CanMeetAdditionalProductionConditions(UnitTeam team, string action, out string failureReason)
        {
            var requirementsToCheck = requirements ?? Array.Empty<UnitProductionRequirement>();
            for (var i = 0; i < requirementsToCheck.Length; i++)
            {
                var requirement = requirementsToCheck[i];
                if (requirement != null && !requirement.IsMet(team))
                {
                    failureReason = requirement.GetFailureReason(action, DisplayName);
                    return false;
                }
            }

            return UnlockRequirement.AreMet(unlockRequirements, team, action, DisplayName, out failureReason);
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

        private static BuildingKind[] CreateAllowedProductionBuildings(IReadOnlyList<string> buildingNames)
        {
            if (buildingNames == null || buildingNames.Count == 0)
            {
                return Array.Empty<BuildingKind>();
            }

            var result = new List<BuildingKind>();
            for (var i = 0; i < buildingNames.Count; i++)
            {
                if (Enum.TryParse(buildingNames[i], true, out BuildingKind kind))
                {
                    result.Add(kind);
                }
            }

            return result.ToArray();
        }

        private static UnitProductionRequirement[] CreateProductionRequirements(IReadOnlyList<string> buildingNames)
        {
            var buildings = CreateAllowedProductionBuildings(buildingNames);
            if (buildings.Length == 0)
            {
                return Array.Empty<UnitProductionRequirement>();
            }

            var requirements = new UnitProductionRequirement[buildings.Length];
            for (var i = 0; i < buildings.Length; i++)
            {
                requirements[i] = new UnitProductionRequirement();
                requirements[i].ConfigureCompletedBuilding(buildings[i]);
            }

            return requirements;
        }

        private static UnlockRequirement[] CreateUnlockRequirements(IReadOnlyList<string> unlockIds)
        {
            if (unlockIds == null || unlockIds.Count == 0)
            {
                return Array.Empty<UnlockRequirement>();
            }

            var requirements = new List<UnlockRequirement>();
            for (var i = 0; i < unlockIds.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(unlockIds[i]))
                {
                    continue;
                }

                var requirement = new UnlockRequirement();
                requirement.ConfigureTeamUnlock(unlockIds[i]);
                requirements.Add(requirement);
            }

            return requirements.ToArray();
        }
    }
}
