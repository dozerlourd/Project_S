using System.Collections.Generic;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ResourceNode : MonoBehaviour, IUnitInteractableTarget
    {
        private static readonly List<ResourceNode> Nodes = new List<ResourceNode>();

        [SerializeField] private ResourceType resourceType = ResourceType.Minerals;
        [SerializeField, Min(0)] private int totalAmount = 1500;
        [SerializeField, Min(1)] private int gatherAmountPerTrip = 5;
        [SerializeField, Min(0f)] private float gatherDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float interactionRange = 0.85f;
        [SerializeField] private bool depleteWhenEmpty = true;

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => interactionRange;
        public ResourceType ResourceType => resourceType;
        public int RemainingAmount => totalAmount;
        public int GatherAmountPerTrip => gatherAmountPerTrip;
        public float GatherDuration => gatherDuration;
        public bool IsDepleted => totalAmount <= 0;
        public static IReadOnlyList<ResourceNode> AllNodes => Nodes;

        private void OnEnable()
        {
            if (!Nodes.Contains(this))
            {
                Nodes.Add(this);
            }
        }

        private void OnDisable()
        {
            Nodes.Remove(this);
        }

        public static ResourceNode FindNearestAvailable(Vector3 position, ResourceType type = ResourceType.Minerals)
        {
            var bestDistance = float.PositiveInfinity;
            ResourceNode best = null;
            for (var i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                if (node == null || node.resourceType != type || !node.CanGather())
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(node.transform.position - position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = node;
            }

            return best;
        }

        private void OnValidate()
        {
            totalAmount = Mathf.Max(0, totalAmount);
            gatherAmountPerTrip = Mathf.Max(1, gatherAmountPerTrip);
            gatherDuration = Mathf.Max(0f, gatherDuration);
            interactionRange = Mathf.Max(0.1f, interactionRange);
        }

        public bool CanInteract(UnitCommandAgent agent)
        {
            var status = agent != null ? agent.Status : null;
            return status != null && status.CanGatherResources && CanGather();
        }

        public bool CanGather()
        {
            return totalAmount > 0;
        }

        public void Configure(
            ResourceType type,
            int amount,
            int gatherPerTrip,
            float duration,
            float range,
            bool hideWhenEmpty)
        {
            resourceType = type;
            totalAmount = Mathf.Max(0, amount);
            gatherAmountPerTrip = Mathf.Max(1, gatherPerTrip);
            gatherDuration = Mathf.Max(0f, duration);
            interactionRange = Mathf.Max(0.1f, range);
            depleteWhenEmpty = hideWhenEmpty;
        }

        public int TryGather()
        {
            if (totalAmount <= 0)
            {
                return 0;
            }

            var gathered = Mathf.Min(gatherAmountPerTrip, totalAmount);
            totalAmount -= gathered;
            if (depleteWhenEmpty && totalAmount <= 0)
            {
                gameObject.SetActive(false);
            }

            return gathered;
        }
    }
}
