using System;
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
        Ranger
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
        Siege = 1 << 3
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

    public sealed class PrototypeUnitStatus : MonoBehaviour, IPlayerSelectableTarget, IUnitAttackTarget
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
        public float PhysicalAttackPower => physicalAttackPower;
        public float MagicalAttackPower => magicalAttackPower;
        public float AttackRange => attackRange;
        public float DetectionRange => Mathf.Max(attackRange, detectionRange);
        public float AttackSpeed => attackSpeed;
        public float MovementSpeed => movementSpeed;
        public int MaxAttackTargets => maxAttackTargets;
        public Vector2Int OccupiedCells => new Vector2Int(Mathf.Max(1, occupiedCells.x), Mathf.Max(1, occupiedCells.y));
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

        private void Awake()
        {
            EnsureHealth();
            EnsureGroundPathAgent();
            EnsureCommandAgent();
            EnsureCombatComponents();
        }

        private void OnEnable()
        {
            UnitAttackTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            UnitAttackTargetRegistry.Unregister(this);
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
            }
        }

        public void TakeDamage(float amount)
        {
            var health = GetComponent<UnitHealth>();
            if (health != null)
            {
                health.TakeDamage(amount);
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

        private bool CanAttack()
        {
            return roles.HasFlag(UnitRole.Combat) || physicalAttackPower > 0f || magicalAttackPower > 0f;
        }
    }
}
