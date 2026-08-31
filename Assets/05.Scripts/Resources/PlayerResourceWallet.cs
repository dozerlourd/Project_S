using ProjectS.Units;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Resources
{
    public sealed class PlayerResourceWallet : MonoBehaviour
    {
        private static readonly Dictionary<UnitTeam, PlayerResourceWallet> WalletsByTeam =
            new Dictionary<UnitTeam, PlayerResourceWallet>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private ResourceAmount startingResources = new ResourceAmount(50, 0);

        private ResourceAmount currentResources;
        private string lastFailureReason;

        public UnitTeam Team => team;
        public int Minerals => currentResources.Minerals;
        public int Gas => currentResources.Gas;
        public ResourceAmount CurrentResources => currentResources;
        public string LastFailureReason => lastFailureReason;

        private void Awake()
        {
            currentResources = startingResources;
        }

        private void OnEnable()
        {
            if (WalletsByTeam.TryGetValue(team, out var existingWallet) && existingWallet != null && existingWallet != this)
            {
                Debug.LogWarning(
                    $"Replacing existing resource wallet for {team}. Only one active wallet should own a team's resources.",
                    this);
            }

            WalletsByTeam[team] = this;
        }

        private void OnDisable()
        {
            if (WalletsByTeam.TryGetValue(team, out var wallet) && wallet == this)
            {
                WalletsByTeam.Remove(team);
            }
        }

        public static PlayerResourceWallet FindForTeam(UnitTeam team)
        {
            return WalletsByTeam.TryGetValue(team, out var wallet) ? wallet : null;
        }

        public void Initialize(UnitTeam ownerTeam, ResourceAmount resources)
        {
            if (isActiveAndEnabled
                && WalletsByTeam.TryGetValue(team, out var registeredWallet)
                && registeredWallet == this)
            {
                WalletsByTeam.Remove(team);
            }

            team = ownerTeam;
            startingResources = resources;
            currentResources = resources;
            lastFailureReason = string.Empty;
            if (isActiveAndEnabled)
            {
                WalletsByTeam[team] = this;
            }
        }

        public int Get(ResourceType type)
        {
            return currentResources.Get(type);
        }

        public void Add(ResourceType type, int amount)
        {
            currentResources.Add(type, amount);
        }

        public void Add(ResourceAmount amount)
        {
            currentResources.Add(ResourceType.Minerals, amount.Minerals);
            currentResources.Add(ResourceType.Gas, amount.Gas);
        }

        public bool CanAfford(ResourceAmount cost)
        {
            return currentResources.CanAfford(cost);
        }

        public bool TrySpend(ResourceAmount cost)
        {
            if (currentResources.TrySpend(cost))
            {
                lastFailureReason = string.Empty;
                return true;
            }

            lastFailureReason = $"Insufficient resources for cost ({cost}). Current resources: {currentResources}.";
            Debug.LogWarning(lastFailureReason, this);
            return false;
        }
    }
}
