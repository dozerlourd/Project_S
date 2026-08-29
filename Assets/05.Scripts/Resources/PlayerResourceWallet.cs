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

        public UnitTeam Team => team;
        public int Minerals => currentResources.Minerals;
        public int Gas => currentResources.Gas;
        public ResourceAmount CurrentResources => currentResources;

        private void Awake()
        {
            currentResources = startingResources;
        }

        private void OnEnable()
        {
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
            return currentResources.TrySpend(cost);
        }
    }
}
