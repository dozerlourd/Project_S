using System;
using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    public sealed class SupplyManager : MonoBehaviour
    {
        private static readonly Dictionary<UnitTeam, SupplyManager> ManagersByTeam =
            new Dictionary<UnitTeam, SupplyManager>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [Header("Team supply limits")]
        [SerializeField, Min(0)] private int standardSupplyLimit = 200;
        [SerializeField, Min(0)] private int advancedSupplyLimit = 100;

        private readonly Dictionary<int, SupplyAmount> unitSupplyByInstanceId = new Dictionary<int, SupplyAmount>();
        private readonly Dictionary<int, SupplyAmount> buildingSupplyByInstanceId = new Dictionary<int, SupplyAmount>();
        private SupplyAmount reservedSupply;
        private bool registered;
        private UnitTeam registeredTeam;

        public UnitTeam Team => team;
        // Legacy standard-supply accessors remain for authored gameplay and external callers.
        public int CurrentSupply => CurrentStandardSupply;
        public int ReservedSupply => ReservedStandardSupply;
        public int MaxSupply => MaxStandardSupply;
        public int AvailableSupply => AvailableStandardSupply;
        public int CurrentStandardSupply { get; private set; }
        public int ReservedStandardSupply => reservedSupply.Standard;
        public int MaxStandardSupply => Mathf.Min(StandardSupplyLimit, GetBuildingSupply(SupplyKind.Standard));
        public int AvailableStandardSupply => Mathf.Max(0, MaxStandardSupply - CurrentStandardSupply - ReservedStandardSupply);
        public int CurrentAdvancedSupply { get; private set; }
        public int ReservedAdvancedSupply => reservedSupply.Advanced;
        public int MaxAdvancedSupply => Mathf.Min(AdvancedSupplyLimit, GetBuildingSupply(SupplyKind.Advanced));
        public int AvailableAdvancedSupply => Mathf.Max(0, MaxAdvancedSupply - CurrentAdvancedSupply - ReservedAdvancedSupply);
        public int StandardSupplyLimit => Mathf.Max(0, standardSupplyLimit);
        public int AdvancedSupplyLimit => Mathf.Max(0, advancedSupplyLimit);
        public string LastFailureReason { get; private set; }

        public event Action SupplyChanged;

        private void OnEnable()
        {
            Register();
            RegisterActiveSceneObjects();
        }

        private void LateUpdate()
        {
            RefreshActiveUnitSupply();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public static SupplyManager FindForTeam(UnitTeam team)
        {
            return ManagersByTeam.TryGetValue(team, out var manager) ? manager : null;
        }

        public void Initialize(UnitTeam ownerTeam)
        {
            Unregister();
            team = ownerTeam;
            if (isActiveAndEnabled)
            {
                Register();
                RegisterActiveSceneObjects();
            }
        }

        public void ConfigureSupplyLimits(int standardLimit, int advancedLimit)
        {
            standardSupplyLimit = Mathf.Max(0, standardLimit);
            advancedSupplyLimit = Mathf.Max(0, advancedLimit);
            NotifySupplyChanged();
        }

        public bool CanReserve(int amount)
        {
            return CanReserve(SupplyAmount.For(SupplyKind.Standard, amount));
        }

        public bool CanReserve(SupplyAmount amount)
        {
            return CurrentStandardSupply + ReservedStandardSupply + amount.Standard <= MaxStandardSupply
                && CurrentAdvancedSupply + ReservedAdvancedSupply + amount.Advanced <= MaxAdvancedSupply;
        }

        public bool TryReserve(int amount)
        {
            return TryReserve(SupplyAmount.For(SupplyKind.Standard, amount));
        }

        public bool TryReserve(SupplyAmount amount)
        {
            if (!CanReserve(amount))
            {
                LastFailureReason = $"Insufficient supply: need {amount.Standard} standard/{amount.Advanced} advanced, available {AvailableStandardSupply} standard/{AvailableAdvancedSupply} advanced.";
                return false;
            }

            reservedSupply += amount;
            LastFailureReason = string.Empty;
            NotifySupplyChanged();
            return true;
        }

        public void ReleaseReservation(int amount)
        {
            ReleaseReservation(SupplyAmount.For(SupplyKind.Standard, amount));
        }

        public void ReleaseReservation(SupplyAmount amount)
        {
            reservedSupply -= amount;
            NotifySupplyChanged();
        }

        public bool CommitReservation(int amount, PrototypeUnitStatus unit)
        {
            return CommitReservation(SupplyAmount.For(SupplyKind.Standard, amount), unit);
        }

        public bool CommitReservation(SupplyAmount amount, PrototypeUnitStatus unit)
        {
            if (unit == null || reservedSupply.Standard < amount.Standard || reservedSupply.Advanced < amount.Advanced)
            {
                LastFailureReason = "Cannot complete supply reservation: reservation is missing.";
                return false;
            }

            RegisterUnit(unit, amount);
            reservedSupply -= amount;
            LastFailureReason = string.Empty;
            NotifySupplyChanged();
            return true;
        }

        public void RegisterUnit(PrototypeUnitStatus unit, int supplyCost)
        {
            RegisterUnit(unit, SupplyAmount.For(SupplyKind.Standard, supplyCost));
        }

        public void RegisterUnit(PrototypeUnitStatus unit, SupplyAmount supplyCost)
        {
            if (unit == null || unit.Team != team)
            {
                return;
            }

            var instanceId = unit.GetInstanceID();
            unitSupplyByInstanceId[instanceId] = supplyCost;
            RefreshUnitSupplyTotals();
            NotifySupplyChanged();
        }

        public void UnregisterUnit(PrototypeUnitStatus unit)
        {
            if (unit == null || !unitSupplyByInstanceId.TryGetValue(unit.GetInstanceID(), out var supplyCost))
            {
                return;
            }

            unitSupplyByInstanceId.Remove(unit.GetInstanceID());
            RefreshUnitSupplyTotals();
            NotifySupplyChanged();
        }

        public void RegisterBuilding(Structure building, int suppliedAmount)
        {
            RegisterBuilding(building, SupplyAmount.For(SupplyKind.Standard, suppliedAmount));
        }

        public void RegisterBuilding(Structure building, SupplyAmount suppliedAmount)
        {
            if (building == null
                || building.Team != team
                || !building.Completed
                || !building.isActiveAndEnabled
                || !building.IsAlive)
            {
                return;
            }

            var instanceId = building.GetInstanceID();
            buildingSupplyByInstanceId[instanceId] = suppliedAmount;
            NotifySupplyChanged();
        }

        public void UnregisterBuilding(Structure building)
        {
            if (building == null || !buildingSupplyByInstanceId.TryGetValue(building.GetInstanceID(), out var suppliedAmount))
            {
                return;
            }

            buildingSupplyByInstanceId.Remove(building.GetInstanceID());
            NotifySupplyChanged();
        }

        private void Register()
        {
            if (ManagersByTeam.TryGetValue(team, out var existingManager) && existingManager != null && existingManager != this)
            {
                // AddComponent invokes OnEnable before callers can configure a newly
                // created manager's owner team. Do not let that temporary serialized
                // default replace an already initialized manager for another team.
                if (existingManager.isActiveAndEnabled)
                {
                    Debug.LogWarning($"A supply manager is already active for {team}. Delaying registration until this manager is initialized.", this);
                    return;
                }

                Debug.LogWarning($"Replacing inactive supply manager for {team}.", this);
            }

            ManagersByTeam[team] = this;
            registeredTeam = team;
            registered = true;
        }

        private void Unregister()
        {
            if (!registered)
            {
                return;
            }

            if (ManagersByTeam.TryGetValue(registeredTeam, out var manager) && manager == this)
            {
                ManagersByTeam.Remove(registeredTeam);
            }

            registered = false;
        }

        private void RegisterActiveSceneObjects()
        {
            RefreshActiveUnitSupply();

            // A manager can be disabled while a provider is destroyed or deactivated.
            // Rebuild from the active scene so that a stale provider never survives a
            // manager re-enable and contributes supply after it is gone.
            buildingSupplyByInstanceId.Clear();

            var buildings = FindObjectsByType<BuildingStatus>(FindObjectsSortMode.None);
            for (var i = 0; i < buildings.Length; i++)
            {
                RegisterBuilding(buildings[i], buildings[i].SupplyProvidedAmount);
            }

            NotifySupplyChanged();
        }

        private void RefreshActiveUnitSupply()
        {
            var activeUnits = UnitRegistry.GetAgents(team);
            unitSupplyByInstanceId.Clear();

            for (var i = 0; i < activeUnits.Count; i++)
            {
                var status = activeUnits[i] != null ? activeUnits[i].Status : null;
                if (status == null || !status.isActiveAndEnabled || !status.IsAlive)
                {
                    continue;
                }

                unitSupplyByInstanceId[status.GetInstanceID()] = status.SupplyUsage;
            }

            var oldStandard = CurrentStandardSupply;
            var oldAdvanced = CurrentAdvancedSupply;
            RefreshUnitSupplyTotals();
            if (oldStandard != CurrentStandardSupply || oldAdvanced != CurrentAdvancedSupply)
            {
                NotifySupplyChanged();
            }
        }

        private void RefreshUnitSupplyTotals()
        {
            var standard = 0;
            var advanced = 0;
            foreach (var amount in unitSupplyByInstanceId.Values)
            {
                standard += amount.Standard;
                advanced += amount.Advanced;
            }

            CurrentStandardSupply = Mathf.Max(0, standard);
            CurrentAdvancedSupply = Mathf.Max(0, advanced);
        }

        private int GetBuildingSupply(SupplyKind kind)
        {
            var total = 0;
            foreach (var amount in buildingSupplyByInstanceId.Values)
            {
                total += amount.Get(kind);
            }

            return Mathf.Max(0, total);
        }

        private void NotifySupplyChanged()
        {
            SupplyChanged?.Invoke();
        }
    }
}
