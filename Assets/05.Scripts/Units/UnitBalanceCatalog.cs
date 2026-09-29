using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ProjectS.Units
{
    [CreateAssetMenu(fileName = ResourceName, menuName = "Project S/Units/Unit Balance Catalog")]
    public sealed class UnitBalanceCatalog : ScriptableObject
    {
        public const string ResourceName = "UnitBalanceCatalog";

        [SerializeField] private TextAsset sourceCsv;
        [SerializeField] private UnitBalanceEntry[] entries = Array.Empty<UnitBalanceEntry>();

        private Dictionary<PrototypeUnitType, UnitBalanceEntry> entriesByType;

        public TextAsset SourceCsv => sourceCsv;
        public IReadOnlyList<UnitBalanceEntry> Entries => entries;

        public static UnitBalanceCatalog LoadDefault()
        {
            return Resources.Load<UnitBalanceCatalog>(ResourceName);
        }

        public static UnitBalanceEntry GetDefaultEntry(PrototypeUnitType unitType)
        {
            var catalog = LoadDefault();
            return catalog != null && catalog.TryGetEntry(unitType, out var entry)
                ? entry
                : UnitBalanceEntry.CreateFallback(unitType);
        }

        public bool TryGetEntry(PrototypeUnitType unitType, out UnitBalanceEntry entry)
        {
            EnsureLookup();
            return entriesByType.TryGetValue(unitType, out entry);
        }

        public void Configure(TextAsset csv, UnitBalanceEntry[] newEntries)
        {
            sourceCsv = csv;
            entries = newEntries ?? Array.Empty<UnitBalanceEntry>();
            entriesByType = null;
        }

        private void OnValidate()
        {
            entriesByType = null;
        }

        private void EnsureLookup()
        {
            if (entriesByType != null)
            {
                return;
            }

            entriesByType = new Dictionary<PrototypeUnitType, UnitBalanceEntry>();
            var sourceEntries = entries ?? Array.Empty<UnitBalanceEntry>();
            for (var i = 0; i < sourceEntries.Length; i++)
            {
                entriesByType[sourceEntries[i].UnitType] = sourceEntries[i];
            }
        }

        public static UnitBalanceEntry[] CreateFallbackEntries()
        {
            var types = (PrototypeUnitType[])Enum.GetValues(typeof(PrototypeUnitType));
            var fallbackEntries = new UnitBalanceEntry[types.Length];
            for (var i = 0; i < types.Length; i++)
            {
                fallbackEntries[i] = UnitBalanceEntry.CreateFallback(types[i]);
            }

            return fallbackEntries;
        }
    }

    [Serializable]
    public sealed class UnitBalanceEntry
    {
        [SerializeField] private PrototypeUnitType unitType;
        [SerializeField] private UnitTrial trial = UnitTrial.Human;
        [SerializeField] private MovementDomain movementDomain = MovementDomain.Ground;
        [SerializeField] private UnitRole roles = UnitRole.Combat;
        [SerializeField] private AttackDistanceType attackDistanceType = AttackDistanceType.Melee;
        [SerializeField] private AttackPowerType attackPowerType = AttackPowerType.Physical;
        [SerializeField] private PlacementType placementType = PlacementType.Movable;
        [SerializeField] private UnitGrade grade = UnitGrade.Common;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float physicalAttackPower = 10f;
        [SerializeField] private float magicalAttackPower;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float detectionRange = 5f;
        [SerializeField] private float attackSpeed = 1f;
        [SerializeField] private float movementSpeed = 3f;
        [SerializeField] private float visionRadius = 7f;
        [SerializeField] private Vector2Int occupiedCells = Vector2Int.one;
        [SerializeField] private int standardSupplyCost = 1;
        [SerializeField] private int advancedSupplyCost;
        [SerializeField] private bool canGatherResources;
        [SerializeField] private float areaDamageRadius;
        [SerializeField] private int maxAreaTargets;
        [SerializeField] private string productionDisplayName;
        [SerializeField] private int productionMineralCost = 50;
        [SerializeField] private int productionGasCost;
        [SerializeField] private float productionTime = 6f;
        [SerializeField] private int unitsPerProduction = 1;
        [SerializeField] private string[] allowedProductionBuildings = Array.Empty<string>();
        [SerializeField] private string[] unlockRequirementIds = Array.Empty<string>();

        public PrototypeUnitType UnitType => unitType;
        public UnitTrial Trial => trial;
        public MovementDomain MovementDomain => movementDomain;
        public UnitRole Roles => roles;
        public AttackDistanceType AttackDistanceType => attackDistanceType;
        public AttackPowerType AttackPowerType => attackPowerType;
        public PlacementType PlacementType => placementType;
        public UnitGrade Grade => grade;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float PhysicalAttackPower => Mathf.Max(0f, physicalAttackPower);
        public float MagicalAttackPower => Mathf.Max(0f, magicalAttackPower);
        public float AttackRange => Mathf.Max(0f, attackRange);
        public float DetectionRange => Mathf.Max(AttackRange, detectionRange);
        public float AttackSpeed => Mathf.Max(0f, attackSpeed);
        public float MovementSpeed => Mathf.Max(0f, movementSpeed);
        public float VisionRadius => Mathf.Max(0f, visionRadius);
        public Vector2Int OccupiedCells => new Vector2Int(Mathf.Max(1, occupiedCells.x), Mathf.Max(1, occupiedCells.y));
        public SupplyAmount SupplyUsage => new SupplyAmount(standardSupplyCost, advancedSupplyCost);
        public bool CanGatherResources => canGatherResources;
        public float AreaDamageRadius => Mathf.Max(0f, areaDamageRadius);
        public int MaxAreaTargets => Mathf.Max(0, maxAreaTargets);
        public AttackTargetType AttackTargetType => AreaDamageRadius > 0f && MaxAreaTargets > 0
            ? AttackTargetType.AreaAttack
            : AttackTargetType.SingleTarget;
        public string ProductionDisplayName => string.IsNullOrWhiteSpace(productionDisplayName)
            ? unitType.ToString()
            : productionDisplayName;
        public int ProductionMineralCost => Mathf.Max(0, productionMineralCost);
        public int ProductionGasCost => Mathf.Max(0, productionGasCost);
        public float ProductionTime => Mathf.Max(0.1f, productionTime);
        public int UnitsPerProduction => Mathf.Max(1, unitsPerProduction);
        public IReadOnlyList<string> AllowedProductionBuildings => allowedProductionBuildings;
        public IReadOnlyList<string> UnlockRequirementIds => unlockRequirementIds;

        public void ApplyTo(PrototypeUnitStatus status, UnitTeam team)
        {
            if (status == null)
            {
                return;
            }

            status.Initialize(
                Trial,
                team,
                UnitType,
                MovementDomain,
                Roles,
                AttackDistanceType,
                AttackPowerType,
                PlacementType,
                Grade,
                AttackTargetType,
                MaxHealth,
                PhysicalAttackPower,
                MagicalAttackPower,
                AttackRange,
                DetectionRange,
                AttackSpeed,
                MovementSpeed,
                MaxAreaTargets,
                OccupiedCells,
                CanGatherResources,
                AreaDamageRadius > 0f && MaxAreaTargets > 0,
                AreaDamageRadius);
            status.ConfigureSupply(SupplyUsage);
            status.ConfigureVisionRadius(VisionRadius);
        }

        public static UnitBalanceEntry CreateFallback(PrototypeUnitType unitType)
        {
            var entry = new UnitBalanceEntry { unitType = unitType, productionDisplayName = unitType.ToString() };
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    entry.Configure(UnitRole.Resource | UnitRole.Builder, AttackDistanceType.Melee, 60f, 3f, 1.2f, 4f, 1f, 3f, 6f, 1, 0, true, 0f, 0, 50, 0, 5f, 1, "MainBase");
                    break;
                case PrototypeUnitType.Soldier:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Melee, 100f, 10f, 1.5f, 5f, 1f, 3.2f, 7f, 2, 0, false, 0f, 0, 100, 0, 7f, 1, "Production");
                    break;
                case PrototypeUnitType.Spliter:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Melee, 90f, 8f, 1.4f, 5f, 0.9f, 3f, 7f, 3, 0, false, 2f, 3, 125, 0, 8f, 1, "SpliterProduction");
                    break;
                case PrototypeUnitType.Ranger:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Ranged, 70f, 8f, 6f, 8f, 0.8f, 2.8f, 9f, 2, 0, false, 0f, 0, 100, 25, 8f, 1, "Production");
                    break;
                case PrototypeUnitType.Tank:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Ranged, 260f, 26f, 5.5f, 7f, 0.55f, 2f, 8f, 3, 0, false, 0f, 0, 150, 0, 10f, 1, "Production");
                    break;
                case PrototypeUnitType.Striker:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Melee, 55f, 6f, 0.8f, 4f, 3f, 4.2f, 6f, 1, 0, false, 0f, 0, 75, 0, 6f, 1, "Production");
                    break;
                case PrototypeUnitType.Swarm:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Melee, 45f, 4f, 1.1f, 4.5f, 1.1f, 3.5f, 5.5f, 1, 0, false, 0f, 0, 120, 0, 8f, 3, "Production");
                    entry.productionDisplayName = "Swarm x3";
                    break;
                case PrototypeUnitType.Medic:
                    entry.Configure(UnitRole.Support, AttackDistanceType.Ranged, 90f, 0f, 0f, 0f, 0f, 3.6f, 10f, 2, 0, false, 0f, 0, 50, 0, 6f, 1, "MaintenanceBay");
                    break;
                case PrototypeUnitType.Siege:
                    entry.Configure(UnitRole.Combat | UnitRole.Siege, AttackDistanceType.Ranged, 180f, 36f, 8.5f, 9.5f, 0.45f, 1.7f, 8f, 4, 0, false, 2.5f, 6, 50, 0, 6f, 1, "VehicleFactory");
                    break;
                case PrototypeUnitType.Scout:
                    entry.Configure(UnitRole.Combat, AttackDistanceType.Ranged, 55f, 5f, 4.5f, 7f, 1.2f, 4.8f, 11f, 1, 0, false, 0f, 0, 50, 0, 6f, 1, "SignalRelay");
                    break;
            }

            return entry;
        }

        private void Configure(
            UnitRole roles,
            AttackDistanceType distanceType,
            float health,
            float physicalPower,
            float range,
            float detection,
            float speed,
            float moveSpeed,
            float vision,
            int supply,
            int advancedSupply,
            bool gather,
            float areaRadius,
            int areaTargets,
            int mineralCost,
            int gasCost,
            float buildTime,
            int outputCount,
            string productionBuilding)
        {
            this.roles = roles;
            attackDistanceType = distanceType;
            maxHealth = health;
            physicalAttackPower = physicalPower;
            attackRange = range;
            detectionRange = detection;
            attackSpeed = speed;
            movementSpeed = moveSpeed;
            visionRadius = vision;
            standardSupplyCost = Mathf.Max(0, supply);
            advancedSupplyCost = Mathf.Max(0, advancedSupply);
            canGatherResources = gather;
            areaDamageRadius = areaRadius;
            maxAreaTargets = areaTargets;
            productionMineralCost = Mathf.Max(0, mineralCost);
            productionGasCost = Mathf.Max(0, gasCost);
            productionTime = buildTime;
            unitsPerProduction = Mathf.Max(1, outputCount);
            allowedProductionBuildings = new[] { productionBuilding };
        }

        public static UnitBalanceEntry FromCsvRow(IReadOnlyDictionary<string, string> row)
        {
            var unitType = ParseEnum(Get(row, "unitType", "type", "unit"), PrototypeUnitType.Soldier);
            var entry = CreateFallback(unitType);
            entry.productionDisplayName = Get(row, "displayName", "name", "productionDisplayName", "unitName", "유닛명");
            if (string.IsNullOrWhiteSpace(entry.productionDisplayName))
            {
                entry.productionDisplayName = unitType == PrototypeUnitType.Swarm ? "Swarm x3" : unitType.ToString();
            }

            entry.maxHealth = GetFloat(row, entry.maxHealth, "maxHealth", "health", "hp", "체력");
            entry.physicalAttackPower = GetFloat(row, entry.physicalAttackPower, "physicalAttackPower", "physicalDamage", "attackPower", "damage", "공격력", "물리공격력");
            entry.magicalAttackPower = GetFloat(row, entry.magicalAttackPower, "magicalAttackPower", "magicDamage", "마법공격력");
            entry.attackRange = GetFloat(row, entry.attackRange, "attackRange", "range", "사거리", "공격사거리");
            entry.detectionRange = GetFloat(row, entry.detectionRange, "detectionRange", "detectRange", "감지거리");
            entry.attackSpeed = GetFloat(row, entry.attackSpeed, "attackSpeed", "공격속도");
            entry.movementSpeed = GetFloat(row, entry.movementSpeed, "movementSpeed", "moveSpeed", "이동속도");
            entry.visionRadius = GetFloat(row, entry.visionRadius, "visionRadius", "vision", "시야");
            entry.standardSupplyCost = GetInt(row, entry.standardSupplyCost, "standardSupplyCost", "supplyCost", "population", "인구");
            entry.advancedSupplyCost = GetInt(row, entry.advancedSupplyCost, "advancedSupplyCost", "advancedPopulation", "고급인구");
            entry.areaDamageRadius = GetFloat(row, entry.areaDamageRadius, "areaDamageRadius", "areaRadius", "광역반경", "광역피해반경");
            entry.maxAreaTargets = GetInt(row, entry.maxAreaTargets, "maxAreaTargets", "areaTargets", "targetCount", "최대대상수");
            entry.productionMineralCost = GetInt(row, entry.productionMineralCost, "minerals", "mineralCost", "productionMinerals", "미네랄", "생산미네랄");
            entry.productionGasCost = GetInt(row, entry.productionGasCost, "gas", "gasCost", "productionGas", "가스", "생산가스");
            entry.productionTime = GetFloat(row, entry.productionTime, "productionTime", "buildTime", "생산시간");
            entry.unitsPerProduction = GetInt(row, entry.unitsPerProduction, "unitsPerProduction", "outputCount", "생산수");
            entry.canGatherResources = GetBool(row, entry.canGatherResources, "canGatherResources", "gather", "자원채집");
            entry.roles = GetFlags(row, entry.roles, "roles", "role", "역할");
            entry.attackDistanceType = ParseEnum(Get(row, "attackDistanceType", "distanceType", "공격거리분류"), entry.attackDistanceType);
            entry.attackPowerType = ParseEnum(Get(row, "attackPowerType", "powerType", "공격기반"), entry.attackPowerType);
            entry.allowedProductionBuildings = GetStringList(row, entry.allowedProductionBuildings, "allowedProductionBuildings", "productionBuildings", "생산건물");
            entry.unlockRequirementIds = GetStringList(row, entry.unlockRequirementIds, "unlockRequirements", "requiredUnlocks", "생산조건");

            return entry;
        }

        private static string Get(IReadOnlyDictionary<string, string> row, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (row.TryGetValue(keys[i], out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static int GetInt(IReadOnlyDictionary<string, string> row, int fallback, params string[] keys)
        {
            var value = Get(row, keys);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static float GetFloat(IReadOnlyDictionary<string, string> row, float fallback, params string[] keys)
        {
            var value = Get(row, keys);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static bool GetBool(IReadOnlyDictionary<string, string> row, bool fallback, params string[] keys)
        {
            var value = Get(row, keys);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("y", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("가능", StringComparison.OrdinalIgnoreCase);
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }

        private static UnitRole GetFlags(IReadOnlyDictionary<string, string> row, UnitRole fallback, params string[] keys)
        {
            var value = Get(row, keys);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var roles = UnitRole.None;
            var parts = SplitList(value);
            for (var i = 0; i < parts.Length; i++)
            {
                roles |= ParseEnum(parts[i], UnitRole.None);
            }

            return roles == UnitRole.None ? fallback : roles;
        }

        private static string[] GetStringList(
            IReadOnlyDictionary<string, string> row,
            string[] fallback,
            params string[] keys)
        {
            var value = Get(row, keys);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback ?? Array.Empty<string>();
            }

            var parts = SplitList(value);
            var result = new List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(part))
                {
                    result.Add(part);
                }
            }

            return result.Count > 0 ? result.ToArray() : fallback ?? Array.Empty<string>();
        }

        private static string[] SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value.Split(new[] { '|', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public static class UnitBalanceCsvParser
    {
        public static UnitBalanceEntry[] Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return UnitBalanceCatalog.CreateFallbackEntries();
            }

            var rows = SplitRows(csv);
            if (rows.Count < 2)
            {
                return UnitBalanceCatalog.CreateFallbackEntries();
            }

            var headers = SplitLine(rows[0]);
            var entries = new List<UnitBalanceEntry>();
            for (var i = 1; i < rows.Count; i++)
            {
                var values = SplitLine(rows[i]);
                if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
                {
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var column = 0; column < headers.Count && column < values.Count; column++)
                {
                    row[headers[column].Trim()] = values[column].Trim();
                }

                entries.Add(UnitBalanceEntry.FromCsvRow(row));
            }

            return entries.Count > 0 ? entries.ToArray() : UnitBalanceCatalog.CreateFallbackEntries();
        }

        private static List<string> SplitRows(string csv)
        {
            var rows = new List<string>();
            var current = string.Empty;
            var quoted = false;
            for (var i = 0; i < csv.Length; i++)
            {
                var c = csv[i];
                if (c == '"')
                {
                    quoted = !quoted;
                }

                if (!quoted && (c == '\n' || c == '\r'))
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        rows.Add(current);
                    }

                    current = string.Empty;
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    {
                        i++;
                    }

                    continue;
                }

                current += c;
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                rows.Add(current);
            }

            return rows;
        }

        private static List<string> SplitLine(string line)
        {
            var values = new List<string>();
            var current = string.Empty;
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += '"';
                        i++;
                        continue;
                    }

                    quoted = !quoted;
                    continue;
                }

                if (!quoted && c == ',')
                {
                    values.Add(current);
                    current = string.Empty;
                    continue;
                }

                current += c;
            }

            values.Add(current);
            return values;
        }
    }
}
