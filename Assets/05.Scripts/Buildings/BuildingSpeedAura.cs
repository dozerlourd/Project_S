using System.Collections.Generic;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingSpeedAura : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 4f;
        [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1.35f;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

        private readonly HashSet<PrototypeUnitStatus> affectedUnits = new HashSet<PrototypeUnitStatus>();
        private readonly HashSet<PrototypeUnitStatus> refreshedUnits = new HashSet<PrototypeUnitStatus>();
        private BuildingStatus status;
        private float nextRefreshTime;

        private void Awake()
        {
            status = GetComponent<BuildingStatus>();
        }

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + refreshInterval;
            RefreshAura();
        }

        private void OnDisable()
        {
            ClearAura();
        }

        private void RefreshAura()
        {
            if (status == null)
            {
                status = GetComponent<BuildingStatus>();
            }

            if (status == null || !status.Completed)
            {
                ClearAura();
                return;
            }

            refreshedUnits.Clear();
            var units = UnitRegistry.GetAgents(status.Team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var unitStatus = unit != null ? unit.Status : null;
                if (unitStatus == null
                    || !unit.gameObject.activeInHierarchy
                    || Vector3.Distance(transform.position, unit.transform.position) > radius)
                {
                    continue;
                }

                unitStatus.SetMovementSpeedModifier(this, movementSpeedMultiplier);
                refreshedUnits.Add(unitStatus);
            }

            affectedUnits.RemoveWhere(unit =>
            {
                if (unit == null || refreshedUnits.Contains(unit))
                {
                    return unit == null;
                }

                unit.RemoveMovementSpeedModifier(this);
                return true;
            });

            foreach (var unit in refreshedUnits)
            {
                affectedUnits.Add(unit);
            }
        }

        private void ClearAura()
        {
            foreach (var unit in affectedUnits)
            {
                unit?.RemoveMovementSpeedModifier(this);
            }

            affectedUnits.Clear();
            refreshedUnits.Clear();
        }
    }
}
