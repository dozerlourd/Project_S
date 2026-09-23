using ProjectS.Units;
using ProjectS.Resources;
using ProjectS.Visibility;
using UnityEngine;

namespace ProjectS.Buildings
{
    [DisallowMultipleComponent]
    public abstract class Structure : MonoBehaviour, IUnitAttackTarget, IAttackTargetPriorityProvider, IFogVisionProvider, IUnitSettlementBlocker
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private BuildingKind kind = BuildingKind.MainBase;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private bool completed = true;
        [SerializeField, Min(0)] private int supplyProvided;
        [SerializeField, Min(0)] private int advancedSupplyProvided;
        [SerializeField, Min(0f)] private float visionRadius = 9f;

        [SerializeField, HideInInspector] private bool roleDefaultsApplied;

        private BuildingHealth health;
        private Collider2D attackCollider;

        public UnitTeam Team => team;
        public BuildingKind Kind => RoleKind ?? kind;
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public bool Completed => completed;
        public int SupplyProvided => Mathf.Max(0, supplyProvided);
        public int AdvancedSupplyProvided => Mathf.Max(0, advancedSupplyProvided);
        public SupplyAmount SupplyProvidedAmount => new SupplyAmount(SupplyProvided, AdvancedSupplyProvided);
        public string SelectionName => Kind.ToString();
        public Transform SelectionTransform => transform;
        public GameObject SelectionGameObject => gameObject;
        public bool IsAlive => completed && (health == null || !health.IsDestroyed);
        public Collider2D AttackCollider => attackCollider != null ? attackCollider : GetComponent<Collider2D>();
        public AttackTargetPriority TargetPriority => GetTargetPriority(Kind);
        public Transform VisionTransform => transform;
        public bool IsVisionActive => isActiveAndEnabled && IsAlive;
        public float VisionRadius => Mathf.Max(0f, visionRadius);
        public Transform SettlementTransform => transform;
        public Vector2Int SettlementFootprint => Footprint;
        public bool IsSettlementBlocking => isActiveAndEnabled;

        public BuildingHealth Health => health;
        protected virtual BuildingKind? RoleKind => null;

        protected abstract void RegisterBuilding();
        protected abstract void UnregisterBuilding();
        protected virtual void EnsureRoleComponents() { }

        protected T EnsureComponent<T>() where T : Component
        {
            return GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        protected virtual void Reset()
        {
            ApplyRoleDefaults();
            EnsureHealth();
            EnsureRoleComponents();
        }

        protected virtual void Awake()
        {
            ApplyRoleDefaults();
            ResolveReferences();
            EnsureHealth();
            EnsureRoleComponents();
            EnsureRangeIndicator();
            EnsureFogVisibilityTarget();
            EnsureSelectionOutlineEffect();
        }

        protected virtual void OnEnable()
        {
            ApplyRoleDefaults();
            EnsureRoleComponents();
            EnsureRangeIndicator();
            ResolveReferences();
            EnsureHealth();
            EnsureFogVisibilityTarget();
            EnsureSelectionOutlineEffect();
            UnitSettlementRegistry.Register(this);
            RegisterBuilding();
            UnitAttackTargetRegistry.Register(this);
            FogOfWarRegistry.Register(this);
            SyncSupplyProvider();
        }

        protected virtual void OnDisable()
        {
            UnitSettlementRegistry.Unregister(this);
            UnregisterBuilding();
            UnitAttackTargetRegistry.Unregister(this);
            FogOfWarRegistry.Unregister(this);
            SupplyManager.FindForTeam(team)?.UnregisterBuilding(this);
        }

        protected virtual void LateUpdate()
        {
            UnitAttackTargetRegistry.RefreshPosition(this);
            FogOfWarRegistry.Refresh(this);
        }


        public void Initialize(UnitTeam ownerTeam, BuildingKind buildingKind, Vector2Int occupiedFootprint, bool isCompleted)
        {
            ApplyRoleDefaults();
            SupplyManager.FindForTeam(team)?.UnregisterBuilding(this);
            if (isActiveAndEnabled)
            {
                UnregisterBuilding();
                UnitAttackTargetRegistry.Unregister(this);
                FogOfWarRegistry.Unregister(this);
            }

            team = ownerTeam;
            kind = RoleKind ?? buildingKind;
            if ((Kind == BuildingKind.SupplyDepot || Kind == BuildingKind.AdvancedSupplyDepot)
                && supplyProvided == 0
                && advancedSupplyProvided == 0)
            {
                var defaultSupply = StructureFactory.GetDefaultSupplyAmount(Kind);
                supplyProvided = defaultSupply.Standard;
                advancedSupplyProvided = defaultSupply.Advanced;
            }
            footprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
            completed = isCompleted;
            ResolveReferences();
            EnsureHealth();
            health?.ResetHealth();
            EnsureRoleComponents();
            EnsureRangeIndicator();
            EnsureFogVisibilityTarget();
            EnsureSelectionOutlineEffect();

            if (isActiveAndEnabled)
            {
                RegisterBuilding();
                UnitAttackTargetRegistry.Register(this);
                FogOfWarRegistry.Register(this);
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
                FogOfWarRegistry.Register(this);
                SyncSupplyProvider();
            }
        }

        public void ConfigureVisionRadius(float radius)
        {
            visionRadius = Mathf.Max(0f, radius);
            if (isActiveAndEnabled)
            {
                FogOfWarRegistry.Refresh(this);
            }
        }

        public void ConfigureSupplyProvided(int amount)
        {
            supplyProvided = Mathf.Max(0, amount);
            SyncSupplyProvider();
        }

        public void ConfigureSupplyProvided(SupplyAmount amount)
        {
            supplyProvided = amount.Standard;
            advancedSupplyProvided = amount.Advanced;
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

        private void ApplyRoleDefaults()
        {
            if (roleDefaultsApplied || !RoleKind.HasValue)
            {
                return;
            }

            // Serialized prefabs mark these defaults as applied so authored overrides survive loading.
            roleDefaultsApplied = true;
            kind = RoleKind.Value;
            footprint = StructureFactory.GetDefaultFootprint(kind);
            var defaultSupply = StructureFactory.GetDefaultSupplyAmount(kind);
            supplyProvided = defaultSupply.Standard;
            advancedSupplyProvided = defaultSupply.Advanced;
            visionRadius = StructureFactory.GetDefaultVisionRadius(kind);
            EnsureHealth();
            health.ConfigureMaxHealth(StructureFactory.GetDefaultMaxHealth(kind));
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

            EnsureComponent<BuildingHealthBar>();
        }

        private void EnsureRangeIndicator()
        {
            BuildingRangeIndicatorSource? source = null;
            if (GetComponent<BuildingAutoTurret>() != null)
            {
                source = BuildingRangeIndicatorSource.AutoTurretAttack;
            }
            else if (GetComponent<BuildingSpeedAura>() != null)
            {
                source = BuildingRangeIndicatorSource.SpeedAura;
            }
            else if (Kind == BuildingKind.SignalRelay)
            {
                source = BuildingRangeIndicatorSource.StructureVision;
            }

            if (source.HasValue)
            {
                EnsureComponent<BuildingRangeIndicator>().Configure(source.Value);
            }
        }

        private void EnsureFogVisibilityTarget()
        {
            EnsureComponent<BuildingFogVisibilityTarget>();
        }

        private void EnsureSelectionOutlineEffect()
        {
            EnsureComponent<SelectionOutlineEffect>().Prepare();
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

            if (isActiveAndEnabled && IsAlive)
            {
                supplyManager.RegisterBuilding(this, SupplyProvidedAmount);
            }
            else
            {
                supplyManager.UnregisterBuilding(this);
            }
        }
    }

}
