using System;
using UnityEngine;

namespace ProjectS.Resources
{
    [Serializable]
    public struct ResourceAmount
    {
        [SerializeField, Min(0)] private int minerals;
        [SerializeField, Min(0)] private int gas;

        public int Minerals => minerals;
        public int Gas => gas;
        public bool IsEmpty => minerals <= 0 && gas <= 0;

        public ResourceAmount(int minerals, int gas)
        {
            this.minerals = Mathf.Max(0, minerals);
            this.gas = Mathf.Max(0, gas);
        }

        public int Get(ResourceType type)
        {
            return type == ResourceType.Minerals ? minerals : gas;
        }

        public void Set(ResourceType type, int value)
        {
            if (type == ResourceType.Minerals)
            {
                minerals = Mathf.Max(0, value);
                return;
            }

            gas = Mathf.Max(0, value);
        }

        public void Add(ResourceType type, int value)
        {
            if (value <= 0)
            {
                return;
            }

            Set(type, Get(type) + value);
        }

        public bool CanAfford(ResourceAmount cost)
        {
            return minerals >= cost.minerals && gas >= cost.gas;
        }

        public bool TrySpend(ResourceAmount cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            minerals -= cost.minerals;
            gas -= cost.gas;
            return true;
        }

        public override string ToString()
        {
            return $"Minerals: {minerals}, Gas: {gas}";
        }
    }
}
