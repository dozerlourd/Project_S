using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    public enum BuildingKind
    {
        MainBase,
        Production,
        ResourceDropOff,
        Other
    }

    public sealed class BuildingStatus : MonoBehaviour, IPlayerSelectableTarget
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind kind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private bool completed = true;

        public UnitTeam Team => team;
        public BuildingKind Kind => kind;
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public bool Completed => completed;
        public string SelectionName => kind.ToString();
        public Transform SelectionTransform => transform;
        public GameObject SelectionGameObject => gameObject;

        private void OnEnable()
        {
            BuildingRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingRegistry.Unregister(this);
        }

        public void Initialize(UnitTeam ownerTeam, BuildingKind buildingKind, Vector2Int occupiedFootprint, bool isCompleted)
        {
            if (isActiveAndEnabled)
            {
                BuildingRegistry.Unregister(this);
            }

            team = ownerTeam;
            kind = buildingKind;
            footprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
            completed = isCompleted;

            if (isActiveAndEnabled)
            {
                BuildingRegistry.Register(this);
            }
        }

        public void MarkCompleted()
        {
            completed = true;
        }
    }
}
