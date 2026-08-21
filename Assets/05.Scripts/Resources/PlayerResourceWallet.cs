using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    public sealed class PlayerResourceWallet : MonoBehaviour
    {
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

        public int Get(ResourceType type)
        {
            return currentResources.Get(type);
        }

        public void Add(ResourceType type, int amount)
        {
            currentResources.Add(type, amount);
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
