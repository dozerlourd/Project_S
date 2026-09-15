using System;
using System.Collections.Generic;
using ProjectS.Visibility;
using UnityEngine;

namespace ProjectS.Units
{
    public enum UnitTrial
    {
        Human
    }

    public enum UnitTeam
    {
        Team1,
        Team2,
        Team3,
        Team4,
        Team5,
        Team6,
        Team7,
        Team8
    }

    public enum PrototypeUnitType
    {
        Worker,
        Soldier,
        Spliter,
        Ranger,
        Tank,
        Striker,
        Swarm,
        Medic,
        Siege,
        Scout
    }

    public enum MovementDomain
    {
        Ground,
        Air,
        Naval
    }

    [Flags]
    public enum UnitRole
    {
        None = 0,
        Resource = 1 << 0,
        Builder = 1 << 1,
        Combat = 1 << 2,
        Siege = 1 << 3,
        Support = 1 << 4
    }

    public enum AttackDistanceType
    {
        Melee,
        Ranged
    }

    public enum AttackPowerType
    {
        Physical,
        Magical
    }

    public enum PlacementType
    {
        Movable,
        Fixed
    }

    public enum UnitGrade
    {
        Common,
        Rare,
        Hero,
        Legendary
    }

    public enum AttackTargetType
    {
        SingleTarget,
        AreaAttack
    }

    public sealed class PrototypeUnitStatus : MonoBehaviour, IPlayerSelectableTarget, IUnitAttackTarget, IFogVisionProvider
    {
        [Header("Classification")]
        [SerializeField] private UnitTrial trial;
        [SerializeField] private UnitTeam team;
        [SerializeField] private PrototypeUnitType unitType;
        [SerializeField] private MovementDomain movementDomain;
        [SerializeField] private UnitRole roles;
        [SerializeField] private AttackDistanceType attackDistanceType;
        [SerializeField] private AttackPowerType attackPowerType;
        [SerializeField] private PlacementType placementType;
        [SerializeField] private UnitGrade grade;
        [SerializeField] private AttackTargetType attackTargetType;

        [Header("Common Status")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float physicalAttackPower = 10f;
        [SerializeField] private float magicalAttackPower;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float detectionRange = 5f;
        [SerializeField] private float attackSpeed = 1f;
        [SerializeField] private float movementSpeed = 3f;
        [SerializeField] private int maxAttackTargets = 1;
        [SerializeField] private Vector2Int occupiedCells = Vector2Int.one;
        [SerializeField, Min(0)] private int supplyCost = 1;
        [SerializeField, Min(0f)] private float visionRadius = 7f;

        [Header("Special Status")]
        [SerializeField] private bool hasHealthRegeneration;
        [SerializeField] private float healthRegenerationAmount;
        [SerializeField] private bool hasShield;
        [SerializeField] private float shieldAmount;
        [SerializeField] private bool hasShieldRegeneration;
        [SerializeField] private float shieldRegenerationAmount;
        [SerializeField] private bool hasMana;
        [SerializeField] private float manaAmount;
        [SerializeField] private bool hasManaRegeneration;
        [SerializeField] private float manaRegenerationAmount;
        [SerializeField] private bool canGatherResources;
        [SerializeField] private bool hasAreaAttack;
        [SerializeField] private float attackArea;

        private readonly Dictionary<object, float> movementSpeedModifiers = new Dictionary<object, float>();

        public UnitTrial Trial => trial;
        public UnitTeam Team => team;
        public PrototypeUnitType UnitType => unitType;
        public MovementDomain MovementDomain => movementDomain;
        public UnitRole Roles => roles;
        public AttackDistanceType AttackDistanceType => attackDistanceType;
        public AttackPowerType AttackPowerType => attackPowerType;
        public PlacementType PlacementType => placementType;
        public UnitGrade Grade => grade;
        public AttackTargetType AttackTargetType => attackTargetType;
        public float MaxHealth => maxHealth;
        public float PhysicalAttackPower => physicalAttackPower + UnitUpgradeStatModifiers.GetAttackDamageBonus(team);
        public float MagicalAttackPower => magicalAttackPower;
        public float AttackRange => attackRange;
        public float DetectionRange => Mathf.Max(attackRange, detectionRange);
        public float AttackSpeed => attackSpeed;
        public float MovementSpeed
        {
            get
            {
                var multiplier = 1f;
                foreach (var modifier in movementSpeedModifiers.Values)
                {
                    multiplier *= Mathf.Max(0.01f, modifier);
                }

                return movementSpeed * UnitUpgradeStatModifiers.GetMovementSpeedMultiplier(team) * multiplier;
            }
        }
        public int MaxAttackTargets => maxAttackTargets;
        public Vector2Int OccupiedCells => new Vector2Int(Mathf.Max(1, occupiedCells.x), Mathf.Max(1, occupiedCells.y));
        public int SupplyCost => Mathf.Max(0, supplyCost);
        public bool HasHealthRegeneration => hasHealthRegeneration;
        public float HealthRegenerationAmount => healthRegenerationAmount;
        public bool HasShield => hasShield;
        public float ShieldAmount => shieldAmount;
        public bool HasShieldRegeneration => hasShieldRegeneration;
        public float ShieldRegenerationAmount => shieldRegenerationAmount;
        public bool HasMana => hasMana;
        public float ManaAmount => manaAmount;
        public bool HasManaRegeneration => hasManaRegeneration;
        public float ManaRegenerationAmount => manaRegenerationAmount;
        public bool CanGatherResources => canGatherResources;
        public bool HasAreaAttack => hasAreaAttack;
        public float AttackArea => attackArea;
        public string SelectionName => unitType.ToString();
        public Transform SelectionTransform => transform;
        public GameObject SelectionGameObject => gameObject;
        public bool IsAlive
        {
            get
            {
                var health = GetComponent<UnitHealth>();
                return health == null || !health.IsDead;
            }
        }
        public Collider2D AttackCollider => GetComponent<Collider2D>();
        public Transform VisionTransform => transform;
        public bool IsVisionActive => isActiveAndEnabled && IsAlive;
        public float VisionRadius => Mathf.Max(0f, visionRadius);

        private void Awake()
        {
            EnsureHealth();
            EnsureGroundPathAgent();
            EnsureCommandAgent();
            EnsureCombatComponents();
            EnsureFogVisibilityTarget();
        }

        private void OnEnable()
        {
            UnitAttackTargetRegistry.Register(this);
            FogOfWarRegistry.Register(this);
        }

        private void OnDisable()
        {
            UnitAttackTargetRegistry.Unregister(this);
            FogOfWarRegistry.Unregister(this);
        }

        private void LateUpdate()
        {
            UnitAttackTargetRegistry.RefreshPosition(this);
            FogOfWarRegistry.Refresh(this);
        }

        private void EnsureFogVisibilityTarget()
        {
            if (GetComponent<FogVisibilityTarget>() == null)
            {
                gameObject.AddComponent<FogVisibilityTarget>();
            }
        }

        public void Initialize(
            UnitTrial trial,
            UnitTeam team,
            PrototypeUnitType unitType,
            MovementDomain movementDomain,
            UnitRole roles,
            AttackDistanceType attackDistanceType,
            AttackPowerType attackPowerType,
            PlacementType placementType,
            UnitGrade grade,
            AttackTargetType attackTargetType,
            float maxHealth,
            float physicalAttackPower,
            float magicalAttackPower,
            float attackRange,
            float detectionRange,
            float attackSpeed,
            float movementSpeed,
            int maxAttackTargets,
            Vector2Int occupiedCells,
            bool canGatherResources,
            bool hasAreaAttack,
            float attackArea)
        {
            this.trial = trial;
            this.team = team;
            this.unitType = unitType;
            this.movementDomain = movementDomain;
            this.roles = roles;
            this.attackDistanceType = attackDistanceType;
            this.attackPowerType = attackPowerType;
            this.placementType = placementType;
            this.grade = grade;
            this.attackTargetType = attackTargetType;
            this.maxHealth = maxHealth;
            this.physicalAttackPower = physicalAttackPower;
            this.magicalAttackPower = magicalAttackPower;
            this.attackRange = attackRange;
            this.detectionRange = Mathf.Max(attackRange, detectionRange);
            this.attackSpeed = attackSpeed;
            this.movementSpeed = movementSpeed;
            this.maxAttackTargets = maxAttackTargets;
            this.occupiedCells = new Vector2Int(Mathf.Max(1, occupiedCells.x), Mathf.Max(1, occupiedCells.y));
            this.canGatherResources = canGatherResources;
            this.hasAreaAttack = hasAreaAttack;
            this.attackArea = attackArea;

            var commandAgent = GetComponent<UnitCommandAgent>();
            if (commandAgent != null)
            {
                UnitRegistry.Register(commandAgent, this);
            }

            var health = GetComponent<UnitHealth>();
            if (health != null)
            {
                health.ResetHealth();
            }

            if (isActiveAndEnabled)
            {
                UnitAttackTargetRegistry.Register(this);
                FogOfWarRegistry.Register(this);
            }
        }

        public void SetTeam(UnitTeam newTeam)
        {
            team = newTeam;
            var commandAgent = GetComponent<UnitCommandAgent>();
            if (commandAgent != null)
            {
                UnitRegistry.Register(commandAgent, this);
            }

            if (isActiveAndEnabled)
            {
                UnitAttackTargetRegistry.Register(this);
                FogOfWarRegistry.Register(this);
            }
        }

        public void ConfigurePrototypeDefaults(PrototypeUnitType type, UnitTeam initialTeam = UnitTeam.Team1)
        {
            switch (type)
            {
                case PrototypeUnitType.Worker:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Resource | UnitRole.Builder, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 60f, 3f, 0f,
                        1.2f, 4f, 1f, 3f, 1, Vector2Int.one, true, false, 0f);
                    ConfigureSupplyCost(1);
                    ConfigureVisionRadius(6f);
                    break;
                case PrototypeUnitType.Soldier:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 100f, 10f, 0f,
                        1.5f, 5f, 1f, 3.2f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(2);
                    ConfigureVisionRadius(7f);
                    break;
                case PrototypeUnitType.Spliter:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.AreaAttack, 90f, 8f, 0f,
                        1.4f, 5f, 0.9f, 3f, 3, Vector2Int.one, false, true, 2f);
                    ConfigureSupplyCost(3);
                    ConfigureVisionRadius(7f);
                    break;
                case PrototypeUnitType.Ranger:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Ranged, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 70f, 8f, 0f,
                        6f, 8f, 0.8f, 2.8f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(2);
                    ConfigureVisionRadius(9f);
                    break;
                case PrototypeUnitType.Tank:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 260f, 5f, 0f,
                        1.4f, 4.5f, 0.65f, 2.4f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(3);
                    ConfigureVisionRadius(8f);
                    break;
                case PrototypeUnitType.Striker:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 55f, 6f, 0f,
                        0.8f, 4f, 3f, 4.2f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(1);
                    ConfigureVisionRadius(6f);
                    break;
                case PrototypeUnitType.Swarm:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 45f, 4f, 0f,
                        1.1f, 4.5f, 1.1f, 3.5f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(1);
                    ConfigureVisionRadius(5.5f);
                    break;
                case PrototypeUnitType.Medic:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Support, AttackDistanceType.Ranged, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 90f, 0f, 0f,
                        0f, 0f, 0f, 3.6f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(2);
                    ConfigureVisionRadius(10f);
                    break;
                case PrototypeUnitType.Siege:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat | UnitRole.Siege, AttackDistanceType.Ranged, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 180f, 36f, 0f,
                        8.5f, 9.5f, 0.45f, 1.7f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(4);
                    ConfigureVisionRadius(8f);
                    break;
                case PrototypeUnitType.Scout:
                    Initialize(UnitTrial.Human, initialTeam, type, MovementDomain.Ground,
                        UnitRole.Combat, AttackDistanceType.Ranged, AttackPowerType.Physical,
                        PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 55f, 5f, 0f,
                        4.5f, 7f, 1.2f, 4.8f, 1, Vector2Int.one, false, false, 0f);
                    ConfigureSupplyCost(1);
                    ConfigureVisionRadius(11f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            SynchronizeCombatComponents();
        }

        public void ConfigureVisionRadius(float radius)
        {
            visionRadius = Mathf.Max(0f, radius);
            if (isActiveAndEnabled)
            {
                FogOfWarRegistry.Refresh(this);
            }
        }

        public void ConfigureSupplyCost(int cost)
        {
            supplyCost = Mathf.Max(0, cost);
        }

        public void SetMovementSpeedModifier(object source, float multiplier)
        {
            if (source == null)
            {
                return;
            }

            movementSpeedModifiers[source] = Mathf.Max(0.01f, multiplier);
        }

        public void RemoveMovementSpeedModifier(object source)
        {
            if (source != null)
            {
                movementSpeedModifiers.Remove(source);
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, null);
        }

        public void TakeDamage(float amount, IUnitAttackTarget attacker)
        {
            var health = GetComponent<UnitHealth>();
            if (health != null)
            {
                health.TakeDamage(amount);
                if (!health.IsDead)
                {
                    GetComponent<UnitCommandAgent>()?.TryRetaliate(attacker);
                }
            }
        }

        private void EnsureGroundPathAgent()
        {
            if (movementDomain != MovementDomain.Ground || placementType != PlacementType.Movable)
            {
                return;
            }

            if (GetComponent<UnitPathAgent>() == null)
            {
                gameObject.AddComponent<UnitPathAgent>();
            }
        }

        private void EnsureCommandAgent()
        {
            if (placementType != PlacementType.Movable)
            {
                return;
            }

            if (GetComponent<UnitCommandAgent>() == null)
            {
                gameObject.AddComponent<UnitCommandAgent>();
            }
        }

        private void EnsureHealth()
        {
            if (GetComponent<UnitHealth>() == null)
            {
                gameObject.AddComponent<UnitHealth>();
            }
        }

        private void EnsureCombatComponents()
        {
            if (placementType != PlacementType.Movable || !CanAttack())
            {
                return;
            }

            if (GetComponent<UnitCombat>() == null)
            {
                gameObject.AddComponent<UnitCombat>();
            }

            if (GetComponent<TemporaryAttackEffect>() == null)
            {
                gameObject.AddComponent<TemporaryAttackEffect>();
            }
        }

        private void SynchronizeCombatComponents()
        {
            if (CanAttack())
            {
                EnsureCombatComponents();
                return;
            }

            RemoveCombatComponent(GetComponent<UnitCombat>());
            RemoveCombatComponent(GetComponent<TemporaryAttackEffect>());
        }

        private static void RemoveCombatComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private bool CanAttack()
        {
            return roles.HasFlag(UnitRole.Combat) || physicalAttackPower > 0f || magicalAttackPower > 0f;
        }
    }
}
