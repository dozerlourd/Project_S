using UnityEngine;

namespace ProjectS.Resources
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceType resourceType = ResourceType.Minerals;
        [SerializeField, Min(0)] private int totalAmount = 1500;
        [SerializeField, Min(1)] private int gatherAmountPerTrip = 5;
        [SerializeField, Min(0f)] private float gatherDuration = 1.5f;
        [SerializeField] private bool depleteWhenEmpty = true;

        public ResourceType ResourceType => resourceType;
        public int RemainingAmount => totalAmount;
        public int GatherAmountPerTrip => gatherAmountPerTrip;
        public float GatherDuration => gatherDuration;
        public bool IsDepleted => totalAmount <= 0;

        private void OnValidate()
        {
            totalAmount = Mathf.Max(0, totalAmount);
            gatherAmountPerTrip = Mathf.Max(1, gatherAmountPerTrip);
            gatherDuration = Mathf.Max(0f, gatherDuration);
        }

        public bool CanGather()
        {
            return totalAmount > 0;
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
