using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectS.Tilemaps
{
    public enum ResourceTileType
    {
        Minerals,
        Gas
    }

    [CreateAssetMenu(menuName = "Project S/Resources/Resource Tile", fileName = "ResourceTile")]
    public sealed class ResourceTile : Tile
    {
        [SerializeField] private ResourceTileType resourceType = ResourceTileType.Minerals;
        [SerializeField, Min(1)] private int initialAmount = 1500;
        [SerializeField, Min(1)] private int gatherAmountPerTrip = 8;
        [SerializeField, Min(0f)] private float gatherDuration = 1.2f;
        [SerializeField, Min(0.1f)] private float interactionRange = 0.95f;

        public ResourceTileType ResourceType => resourceType;
        public int InitialAmount => Mathf.Max(1, initialAmount);
        public int GatherAmountPerTrip => Mathf.Max(1, gatherAmountPerTrip);
        public float GatherDuration => Mathf.Max(0f, gatherDuration);
        public float InteractionRange => Mathf.Max(0.1f, interactionRange);

        public void Configure(
            ResourceTileType type,
            int amount,
            int gatherPerTrip,
            float duration,
            float range)
        {
            resourceType = type;
            initialAmount = Mathf.Max(1, amount);
            gatherAmountPerTrip = Mathf.Max(1, gatherPerTrip);
            gatherDuration = Mathf.Max(0f, duration);
            interactionRange = Mathf.Max(0.1f, range);
        }
    }
}
