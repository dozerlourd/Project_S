using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class ResourceDropOff : MonoBehaviour, IUnitInteractableTarget
    {
        private static readonly List<ResourceDropOff> AllDropOffs = new List<ResourceDropOff>();
        private static readonly Dictionary<UnitTeam, List<ResourceDropOff>> DropOffsByTeam =
            new Dictionary<UnitTeam, List<ResourceDropOff>>();

        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private float interactionRange = 1.25f;

        private BuildingStatus status;
        private UnitTeam registeredTeam;
        private bool registered;
        private string lastDepositFailureReason;

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => Mathf.Max(0.1f, interactionRange);
        public UnitTeam Team => status != null ? status.Team : UnitTeam.Team1;
        public bool CanAcceptDeposits => status != null && status.Completed && wallet != null;
        public string LastDepositFailureReason => lastDepositFailureReason;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (!AllDropOffs.Contains(this))
            {
                AllDropOffs.Add(this);
            }

            Register();
        }

        private void OnDisable()
        {
            Unregister();
            AllDropOffs.Remove(this);
        }

        public static ResourceDropOff FindNearest(UnitTeam team, Vector3 position)
        {
            RefreshRegistrations();
            DropOffsByTeam.TryGetValue(team, out var dropOffs);

            var bestDistance = float.PositiveInfinity;
            ResourceDropOff best = null;
            if (dropOffs == null)
            {
                return null;
            }

            for (var i = 0; i < dropOffs.Count; i++)
            {
                var dropOff = dropOffs[i];
                if (dropOff == null)
                {
                    dropOffs.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!dropOff.gameObject.activeInHierarchy)
                {
                    continue;
                }

                dropOff.ResolveReferences();
                if (dropOff.Team != team || !dropOff.CanAcceptDeposits)
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

        private static void RefreshRegistrations()
        {
            for (var i = 0; i < AllDropOffs.Count; i++)
            {
                var dropOff = AllDropOffs[i];
                if (dropOff == null)
                {
                    AllDropOffs.RemoveAt(i);
                    i--;
                    continue;
                }

                if (dropOff.isActiveAndEnabled)
                {
                    dropOff.ResolveReferences();
                }
            }
        }

        public bool CanInteract(UnitCommandAgent agent)
        {
            var unitStatus = agent != null ? agent.Status : null;
            return unitStatus != null && unitStatus.Team == Team && status != null && status.Completed;
        }

        public bool TryDeposit(UnitTeam team, ResourceAmount amount)
        {
            if (team != Team)
            {
                return FailDeposit($"Drop-off team mismatch. Expected {Team}, received {team}.");
            }

            if (amount.IsEmpty)
            {
                return FailDeposit("Cannot deposit an empty resource amount.");
            }

            ResolveReferences();
            if (wallet == null)
            {
                return FailDeposit($"No resource wallet registered for {Team}.");
            }

            wallet.Add(amount);
            lastDepositFailureReason = string.Empty;
            return true;
        }

        private bool FailDeposit(string reason)
        {
            lastDepositFailureReason = reason;
            Debug.LogWarning(reason, this);
            return false;
        }

        private void ResolveReferences()
        {
            if (status == null)
            {
                status = GetComponent<BuildingStatus>();
            }

            if (registered && registeredTeam != Team)
            {
                Unregister();
                Register();
            }

            if (wallet == null || wallet.Team != Team)
            {
                wallet = PlayerResourceWallet.FindForTeam(Team);
            }
        }

        private void Register()
        {
            registeredTeam = Team;
            if (!DropOffsByTeam.TryGetValue(Team, out var dropOffs))
            {
                dropOffs = new List<ResourceDropOff>();
                DropOffsByTeam.Add(Team, dropOffs);
            }

            if (!dropOffs.Contains(this))
            {
                dropOffs.Add(this);
            }

            registered = true;
        }

        private void Unregister()
        {
            if (DropOffsByTeam.TryGetValue(registeredTeam, out var dropOffs))
            {
                dropOffs.Remove(this);
            }

            registered = false;
        }
    }
}
