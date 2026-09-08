using ProjectS.Units;
using ProjectS.Resources;
using UnityEngine;

namespace ProjectS.Buildings
{
    public enum BuildingKind
    {
        MainBase,
        Production,
        ResourceDropOff,
        SpliterProduction,
        AutoTurret,
        SpeedAura,
        Other
    }

    public sealed class BuildingStatus : MonoBehaviour, IUnitAttackTarget, IAttackTargetPriorityProvider
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind kind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private bool completed = true;
        [SerializeField, Min(0)] private int supplyProvided;

        private BuildingHealth health;
        private Collider2D attackCollider;

        public UnitTeam Team => team;
        public BuildingKind Kind => kind;
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public bool Completed => completed;
        public int SupplyProvided => Mathf.Max(0, supplyProvided);
        public string SelectionName => kind.ToString();
        public Transform SelectionTransform => transform;
        public GameObject SelectionGameObject => gameObject;
        public bool IsAlive => completed && (health == null || !health.IsDestroyed);
        public Collider2D AttackCollider => attackCollider != null ? attackCollider : GetComponent<Collider2D>();
        public AttackTargetPriority TargetPriority => GetTargetPriority(kind);

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
            SyncSupplyProvider();
        }

        private void OnDisable()
        {
            BuildingRegistry.Unregister(this);
            UnitAttackTargetRegistry.Unregister(this);
            SupplyManager.FindForTeam(team)?.UnregisterBuilding(this);
        }

        public void Initialize(UnitTeam ownerTeam, BuildingKind buildingKind, Vector2Int occupiedFootprint, bool isCompleted)
        {
            SupplyManager.FindForTeam(team)?.UnregisterBuilding(this);
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
                SyncSupplyProvider();
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
                SyncSupplyProvider();
            }
        }

        public void ConfigureSupplyProvided(int amount)
        {
            supplyProvided = Mathf.Max(0, amount);
            SyncSupplyProvider();
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

        public void TakeDamage(float amount, IUnitAttackTarget attacker)
        {
            if (!completed || amount <= 0f)
            {
                return;
            }

            EnsureHealth();
            if (health == null || health.IsDestroyed)
            {
                return;
            }

            UnitTargetPriority.RecordRecentAttacker(this, attacker);
            health.TakeDamage(amount);
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

        private static AttackTargetPriority GetTargetPriority(BuildingKind buildingKind)
        {
            switch (buildingKind)
            {
                case BuildingKind.AutoTurret:
                    return AttackTargetPriority.DefensiveBuilding;
                case BuildingKind.Production:
                case BuildingKind.SpliterProduction:
                    return AttackTargetPriority.ProductionBuilding;
                case BuildingKind.MainBase:
                    return AttackTargetPriority.MainBase;
                default:
                    return AttackTargetPriority.Other;
            }
        }

        private void SyncSupplyProvider()
        {
            var supplyManager = SupplyManager.FindForTeam(team);
            if (supplyManager == null)
            {
                return;
            }

            if (completed)
            {
                supplyManager.RegisterBuilding(this, SupplyProvided);
            }
            else
            {
                supplyManager.UnregisterBuilding(this);
            }
        }
    }
}
