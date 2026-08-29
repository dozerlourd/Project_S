using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class ResourceDropOff : MonoBehaviour, IUnitInteractableTarget
    {
        private static readonly Dictionary<UnitTeam, List<ResourceDropOff>> DropOffsByTeam =
            new Dictionary<UnitTeam, List<ResourceDropOff>>();

        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private float interactionRange = 1.25f;

        private BuildingStatus status;

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => Mathf.Max(0.1f, interactionRange);
        public UnitTeam Team => status != null ? status.Team : UnitTeam.Team1;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public static ResourceDropOff FindNearest(UnitTeam team, Vector3 position)
        {
            if (!DropOffsByTeam.TryGetValue(team, out var dropOffs))
            {
                return null;
            }

            var bestDistance = float.PositiveInfinity;
            ResourceDropOff best = null;
            for (var i = 0; i < dropOffs.Count; i++)
            {
                var dropOff = dropOffs[i];
                if (dropOff == null || !dropOff.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(dropOff.transform.position - position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = dropOff;
            }

            return best;
        }

        public bool CanInteract(UnitCommandAgent agent)
        {
            var unitStatus = agent != null ? agent.Status : null;
            return unitStatus != null && unitStatus.Team == Team && status != null && status.Completed;
        }

        public bool TryDeposit(UnitTeam team, ResourceAmount amount)
        {
            if (team != Team || amount.IsEmpty)
            {
                return false;
            }

            ResolveReferences();
            if (wallet == null)
            {
                return false;
            }

            wallet.Add(amount);
            return true;
        }

        private void ResolveReferences()
        {
            if (status == null)
            {
                status = GetComponent<BuildingStatus>();
            }

            if (wallet == null)
            {
                wallet = PlayerResourceWallet.FindForTeam(Team);
            }
        }

        private void Register()
        {
            if (!DropOffsByTeam.TryGetValue(Team, out var dropOffs))
            {
                dropOffs = new List<ResourceDropOff>();
                DropOffsByTeam.Add(Team, dropOffs);
            }

            if (!dropOffs.Contains(this))
            {
                dropOffs.Add(this);
            }
        }

        private void Unregister()
        {
            if (DropOffsByTeam.TryGetValue(Team, out var dropOffs))
            {
                dropOffs.Remove(this);
            }
        }
    }
}
