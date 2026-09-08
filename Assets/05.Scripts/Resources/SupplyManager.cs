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

        private readonly Dictionary<int, int> unitSupplyByInstanceId = new Dictionary<int, int>();
        private readonly Dictionary<int, int> buildingSupplyByInstanceId = new Dictionary<int, int>();
        private int reservedSupply;
        private bool registered;
        private UnitTeam registeredTeam;

        public UnitTeam Team => team;
        public int CurrentSupply { get; private set; }
        public int ReservedSupply => reservedSupply;
        public int MaxSupply { get; private set; }
        public int AvailableSupply => Mathf.Max(0, MaxSupply - CurrentSupply - ReservedSupply);
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

        public bool CanReserve(int amount)
        {
            return amount >= 0 && CurrentSupply + ReservedSupply + amount <= MaxSupply;
        }

        public bool TryReserve(int amount)
        {
            if (!CanReserve(amount))
            {
                LastFailureReason = $"Insufficient supply: need {Mathf.Max(0, amount)}, available {AvailableSupply}.";
                return false;
            }

            reservedSupply += amount;
            LastFailureReason = string.Empty;
            NotifySupplyChanged();
            return true;
        }

        public void ReleaseReservation(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            reservedSupply = Mathf.Max(0, reservedSupply - amount);
            NotifySupplyChanged();
        }

        public bool CommitReservation(int amount, PrototypeUnitStatus unit)
        {
            if (unit == null || amount < 0 || reservedSupply < amount)
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
            if (unit == null || unit.Team != team)
            {
                return;
            }

            var instanceId = unit.GetInstanceID();
            unitSupplyByInstanceId.TryGetValue(instanceId, out var previousCost);
            var sanitizedCost = Mathf.Max(0, supplyCost);
            unitSupplyByInstanceId[instanceId] = sanitizedCost;
            CurrentSupply += sanitizedCost - previousCost;
            NotifySupplyChanged();
        }

        public void UnregisterUnit(PrototypeUnitStatus unit)
        {
            if (unit == null || !unitSupplyByInstanceId.TryGetValue(unit.GetInstanceID(), out var supplyCost))
            {
                return;
            }

            unitSupplyByInstanceId.Remove(unit.GetInstanceID());
            CurrentSupply = Mathf.Max(0, CurrentSupply - supplyCost);
            NotifySupplyChanged();
        }

        public void RegisterBuilding(BuildingStatus building, int suppliedAmount)
        {
            if (building == null || building.Team != team || !building.Completed)
            {
                return;
            }

            var instanceId = building.GetInstanceID();
            buildingSupplyByInstanceId.TryGetValue(instanceId, out var previousAmount);
            var sanitizedAmount = Mathf.Max(0, suppliedAmount);
            buildingSupplyByInstanceId[instanceId] = sanitizedAmount;
            MaxSupply += sanitizedAmount - previousAmount;
            NotifySupplyChanged();
        }

        public void UnregisterBuilding(BuildingStatus building)
        {
            if (building == null || !buildingSupplyByInstanceId.TryGetValue(building.GetInstanceID(), out var suppliedAmount))
            {
                return;
            }

            buildingSupplyByInstanceId.Remove(building.GetInstanceID());
            MaxSupply = Mathf.Max(0, MaxSupply - suppliedAmount);
            NotifySupplyChanged();
        }

        private void Register()
        {
            if (ManagersByTeam.TryGetValue(team, out var existingManager) && existingManager != null && existingManager != this)
            {
                Debug.LogWarning($"Replacing existing supply manager for {team}. Only one active manager should own a team's supply.", this);
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

            var buildings = FindObjectsByType<BuildingStatus>(FindObjectsSortMode.None);
            for (var i = 0; i < buildings.Length; i++)
            {
                RegisterBuilding(buildings[i], buildings[i].SupplyProvided);
            }
        }

        private void RefreshActiveUnitSupply()
        {
            var activeUnits = UnitRegistry.GetAgents(team);
            var refreshedSupply = 0;
            unitSupplyByInstanceId.Clear();

            for (var i = 0; i < activeUnits.Count; i++)
            {
                var status = activeUnits[i] != null ? activeUnits[i].Status : null;
                if (status == null || !status.isActiveAndEnabled || !status.IsAlive)
                {
                    continue;
                }

                var supplyCost = status.SupplyCost;
                unitSupplyByInstanceId[status.GetInstanceID()] = supplyCost;
                refreshedSupply += supplyCost;
            }

            if (CurrentSupply != refreshedSupply)
            {
                CurrentSupply = refreshedSupply;
                NotifySupplyChanged();
            }
        }

        private void NotifySupplyChanged()
        {
            SupplyChanged?.Invoke();
        }
    }
}
