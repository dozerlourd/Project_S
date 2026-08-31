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

    public sealed class BuildingStatus : MonoBehaviour, IUnitAttackTarget
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind kind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private bool completed = true;

        private BuildingHealth health;
        private Collider2D attackCollider;

        public UnitTeam Team => team;
        public BuildingKind Kind => kind;
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public bool Completed => completed;
        public string SelectionName => kind.ToString();
        public Transform SelectionTransform => transform;
        public GameObject SelectionGameObject => gameObject;
        public bool IsAlive => completed && (health == null || !health.IsDestroyed);
        public Collider2D AttackCollider => attackCollider != null ? attackCollider : GetComponent<Collider2D>();

        private void Awake()
        {
            ResolveReferences();
            EnsureHealth();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureHealth();
            BuildingRegistry.Register(this);
            UnitAttackTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingRegistry.Unregister(this);
            UnitAttackTargetRegistry.Unregister(this);
        }

        public void Initialize(UnitTeam ownerTeam, BuildingKind buildingKind, Vector2Int occupiedFootprint, bool isCompleted)
        {
            if (isActiveAndEnabled)
            {
                BuildingRegistry.Unregister(this);
                UnitAttackTargetRegistry.Unregister(this);
            }

            team = ownerTeam;
            kind = buildingKind;
            footprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
            completed = isCompleted;
            ResolveReferences();
            EnsureHealth();
            health?.ResetHealth();

            if (isActiveAndEnabled)
            {
                BuildingRegistry.Register(this);
                UnitAttackTargetRegistry.Register(this);
            }
        }

        public void MarkCompleted()
        {
            completed = true;
            EnsureHealth();
            health?.ResetHealth();
            if (isActiveAndEnabled)
            {
                UnitAttackTargetRegistry.Register(this);
            }
        }

        public void TakeDamage(float amount)
        {
            if (!completed)
            {
                return;
            }

            EnsureHealth();
            health?.TakeDamage(amount);
        }

        private void ResolveReferences()
        {
            if (health == null)
            {
                health = GetComponent<BuildingHealth>();
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }
        }

        private void EnsureHealth()
        {
            if (health == null)
            {
                health = GetComponent<BuildingHealth>();
            }

            if (health == null)
            {
                health = gameObject.AddComponent<BuildingHealth>();
            }
        }
    }
}
