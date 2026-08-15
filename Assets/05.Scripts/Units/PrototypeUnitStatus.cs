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

    public sealed class PrototypeUnitStatus : MonoBehaviour
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
        [SerializeField] private float attackSpeed = 1f;
        [SerializeField] private float movementSpeed = 3f;
        [SerializeField] private int maxAttackTargets = 1;

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
        public float AttackSpeed => attackSpeed;
        public float MovementSpeed => movementSpeed;
        public int MaxAttackTargets => maxAttackTargets;
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
            float attackSpeed,
            float movementSpeed,
            int maxAttackTargets,
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
            this.attackSpeed = attackSpeed;
            this.movementSpeed = movementSpeed;
            this.maxAttackTargets = maxAttackTargets;
            this.canGatherResources = canGatherResources;
            this.hasAreaAttack = hasAreaAttack;
            this.attackArea = attackArea;
        }
    }
}
