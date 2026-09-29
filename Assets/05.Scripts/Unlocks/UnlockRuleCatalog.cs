using System;
using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Unlocks
{
    public enum UnlockRuleTargetType
    {
        Unit,
        Building
    }

    [Serializable]
    public sealed class UnlockRuleEntry
    {
        [SerializeField] private string ruleId = string.Empty;
        [SerializeField] private UnlockRuleTargetType targetType;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private string[] requiredBuildings = Array.Empty<string>();
        [SerializeField] private string[] requiredResearchIds = Array.Empty<string>();
        [SerializeField] private string[] prerequisiteUnlockIds = Array.Empty<string>();
        [SerializeField] private bool active;
        [SerializeField] private string notes = string.Empty;

        public string RuleId => ruleId;
        public UnlockRuleTargetType TargetType => targetType;
        public string TargetId => targetId;
        public IReadOnlyList<string> RequiredBuildings => requiredBuildings;
        public IReadOnlyList<string> RequiredResearchIds => requiredResearchIds;
        public IReadOnlyList<string> PrerequisiteUnlockIds => prerequisiteUnlockIds;
        public bool Active => active;
        public string Notes => notes;

        public static UnlockRuleEntry FromCsvRow(
            IReadOnlyDictionary<string, string> row,
            int sourceRowNumber)
        {
            var entry = new UnlockRuleEntry
            {
                ruleId = RequiredValue(row, "규칙ID", sourceRowNumber),
                targetType = ParseTargetType(RequiredValue(row, "대상종류", sourceRowNumber), sourceRowNumber),
                targetId = RequiredValue(row, "대상ID", sourceRowNumber),
                requiredBuildings = ParseList(Value(row, "필요건물")),
                requiredResearchIds = ParseList(Value(row, "필요연구")),
                prerequisiteUnlockIds = ParseList(Value(row, "선행해금")),
                active = ParseActive(Value(row, "활성"), sourceRowNumber),
                notes = Value(row, "메모")
            };

            entry.ValidateTarget(sourceRowNumber);
            entry.ValidateRequiredBuildings(sourceRowNumber);
            return entry;
        }

        private void ValidateTarget(int sourceRowNumber)
        {
            var valid = targetType == UnlockRuleTargetType.Unit
                ? Enum.TryParse(targetId, true, out PrototypeUnitType _)
                : Enum.TryParse(targetId, true, out BuildingKind _);
            if (!valid)
            {
                throw new FormatException(
                    $"CSV {sourceRowNumber}행의 대상ID '{targetId}'가 대상종류 {targetType}에 맞지 않습니다.");
            }
        }

        private void ValidateRequiredBuildings(int sourceRowNumber)
        {
            for (var i = 0; i < requiredBuildings.Length; i++)
            {
                if (!Enum.TryParse(requiredBuildings[i], true, out BuildingKind _))
                {
                    throw new FormatException(
                        $"CSV {sourceRowNumber}행의 필요건물 '{requiredBuildings[i]}'을 BuildingKind에서 찾을 수 없습니다.");
                }
            }
        }

        private static UnlockRuleTargetType ParseTargetType(string value, int sourceRowNumber)
        {
            if (string.Equals(value, "유닛", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                return UnlockRuleTargetType.Unit;
            }

            if (string.Equals(value, "건물", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Building", StringComparison.OrdinalIgnoreCase))
            {
                return UnlockRuleTargetType.Building;
            }

            throw new FormatException(
                $"CSV {sourceRowNumber}행의 대상종류는 '유닛' 또는 '건물'이어야 합니다: '{value}'.");
        }

        private static bool ParseActive(string value, int sourceRowNumber)
        {
            if (string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "아니오", StringComparison.OrdinalIgnoreCase)
                || value == "0")
            {
                return false;
            }

            if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "예", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return true;
            }

            throw new FormatException(
                $"CSV {sourceRowNumber}행의 활성 값은 TRUE/FALSE, 예/아니오 또는 1/0이어야 합니다: '{value}'.");
        }

        private static string[] ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var values = value.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            for (var i = 0; i < values.Length; i++)
            {
                var item = values[i].Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    result.Add(item);
                }
            }

            return result.ToArray();
        }

        private static string RequiredValue(
            IReadOnlyDictionary<string, string> row,
            string header,
            int sourceRowNumber)
        {
            var value = Value(row, header);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException($"CSV {sourceRowNumber}행의 '{header}' 값이 비어 있습니다.");
            }

            return value;
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string header)
        {
            return row != null && row.TryGetValue(header, out var value)
                ? value?.Trim() ?? string.Empty
                : string.Empty;
        }
    }

    [CreateAssetMenu(fileName = ResourceName, menuName = "Project S/Unlocks/Unlock Rule Catalog")]
    public sealed class UnlockRuleCatalog : ScriptableObject
    {
        public const string ResourceName = "UnlockRuleCatalog";

        [SerializeField] private TextAsset sourceCsv;
        [SerializeField] private UnlockRuleEntry[] entries = Array.Empty<UnlockRuleEntry>();

        public TextAsset SourceCsv => sourceCsv;
        public IReadOnlyList<UnlockRuleEntry> Entries => entries;

        public void Configure(TextAsset csv, UnlockRuleEntry[] newEntries)
        {
            sourceCsv = csv;
            entries = newEntries ?? Array.Empty<UnlockRuleEntry>();
        }
    }

    public static class UnlockRuleCsvParser
    {
        private static readonly string[] RequiredHeaders =
        {
            "규칙ID",
            "대상종류",
            "대상ID",
            "필요건물",
            "필요연구",
            "선행해금",
            "활성",
            "메모"
        };

        public static UnlockRuleEntry[] Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return Array.Empty<UnlockRuleEntry>();
            }

            var rows = SplitRows(csv);
            if (rows.Count == 0)
            {
                return Array.Empty<UnlockRuleEntry>();
            }

            var headers = SplitLine(rows[0]);
            for (var i = 0; i < headers.Count; i++)
            {
                headers[i] = headers[i].Trim().TrimStart('\uFEFF');
            }

            ValidateHeaders(headers);
            var entries = new List<UnlockRuleEntry>();
            var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var values = SplitLine(rows[rowIndex]);
                if (IsEmptyRow(values))
                {
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var column = 0; column < headers.Count; column++)
                {
                    row[headers[column]] = column < values.Count ? values[column].Trim() : string.Empty;
                }

                var entry = UnlockRuleEntry.FromCsvRow(row, rowIndex + 1);
                if (!ruleIds.Add(entry.RuleId))
                {
                    throw new FormatException(
                        $"CSV {rowIndex + 1}행의 규칙ID '{entry.RuleId}'가 중복되었습니다.");
                }

                entries.Add(entry);
            }

            return entries.ToArray();
        }

        private static void ValidateHeaders(IReadOnlyList<string> headers)
        {
            for (var i = 0; i < RequiredHeaders.Length; i++)
            {
                var found = false;
                for (var headerIndex = 0; headerIndex < headers.Count; headerIndex++)
                {
                    if (string.Equals(headers[headerIndex], RequiredHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new FormatException($"CSV에 필수 열 '{RequiredHeaders[i]}'이 없습니다.");
                }
            }
        }

        private static bool IsEmptyRow(IReadOnlyList<string> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return false;
                }
            }

            return true;
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
