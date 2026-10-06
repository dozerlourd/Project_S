using System;
using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Unlocks;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Upgrades
{
    public enum UnitUpgradeResearchStatus
    {
        Available,
        Researching,
        Completed
    }

    public sealed class TeamUpgradeResearch : MonoBehaviour
    {
        private static readonly Dictionary<UnitTeam, TeamUpgradeResearch> ResearchByTeam =
            new Dictionary<UnitTeam, TeamUpgradeResearch>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private UnitUpgradeDefinition[] definitions;

        private readonly HashSet<UnitUpgradeKind> completedUpgrades = new HashSet<UnitUpgradeKind>();
        private UnitUpgradeDefinition activeDefinition;
        private float activeElapsed;
        private string lastFailureReason;
        private bool registered;

        public UnitTeam Team => team;
        public IReadOnlyList<UnitUpgradeDefinition> Definitions => definitions;
        public UnitUpgradeDefinition ActiveDefinition => activeDefinition;
        public float ActiveProgress01 => activeDefinition == null
            ? 0f
            : Mathf.Clamp01(activeElapsed / activeDefinition.ResearchDuration);
        public string LastFailureReason => lastFailureReason;

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
            UnitUpgradeStatModifiers.ClearTeam(team);
        }

        private void Update()
        {
            if (activeDefinition == null)
            {
                return;
            }

            activeElapsed += Time.deltaTime;
            if (activeElapsed >= activeDefinition.ResearchDuration)
            {
                CompleteActiveResearch();
            }
        }

        public static TeamUpgradeResearch FindForTeam(UnitTeam team)
        {
            return ResearchByTeam.TryGetValue(team, out var research) ? research : null;
        }

        public void Configure(UnitTeam ownerTeam, PlayerResourceWallet ownerWallet, UnitUpgradeDefinition[] upgradeDefinitions)
        {
            Unregister();
            UnitUpgradeStatModifiers.ClearTeam(team);
            team = ownerTeam;
            wallet = ownerWallet;
            definitions = upgradeDefinitions ?? new UnitUpgradeDefinition[0];
            completedUpgrades.Clear();
            activeDefinition = null;
            activeElapsed = 0f;
            lastFailureReason = string.Empty;
            if (isActiveAndEnabled)
            {
                Register();
            }
        }

        public UnitUpgradeResearchStatus GetStatus(UnitUpgradeDefinition definition)
        {
            if (definition == null)
            {
                return UnitUpgradeResearchStatus.Available;
            }

            if (activeDefinition == definition)
            {
                return UnitUpgradeResearchStatus.Researching;
            }

            return completedUpgrades.Contains(definition.UpgradeKind)
                ? UnitUpgradeResearchStatus.Completed
                : UnitUpgradeResearchStatus.Available;
        }

        public bool TryStartResearch(int definitionIndex)
        {
            if (definitions == null || definitionIndex < 0 || definitionIndex >= definitions.Length)
            {
                lastFailureReason = "Unknown upgrade.";
                return false;
            }

            return TryStartResearch(definitions[definitionIndex]);
        }

        public bool TryStartResearch(UnitUpgradeDefinition definition)
        {
            if (definition == null)
            {
                lastFailureReason = "Unknown upgrade.";
                return false;
            }

            if (activeDefinition != null)
            {
                lastFailureReason = $"Research already in progress: {activeDefinition.DisplayName}.";
                return false;
            }

            if (completedUpgrades.Contains(definition.UpgradeKind))
            {
                lastFailureReason = $"Research already completed: {definition.DisplayName}.";
                return false;
            }

            ResolveWallet();
            if (wallet == null)
            {
                lastFailureReason = "No resource wallet is available.";
                return false;
            }

            if (!wallet.TrySpend(definition.Cost))
            {
                lastFailureReason = wallet.LastFailureReason;
                return false;
            }

            activeDefinition = definition;
            activeElapsed = 0f;
            lastFailureReason = string.Empty;
            return true;
        }

        private void CompleteActiveResearch()
        {
            if (activeDefinition == null)
            {
                return;
            }

            var completedDefinition = activeDefinition;
            completedUpgrades.Add(completedDefinition.UpgradeKind);
            UnitUpgradeStatModifiers.SetUpgrade(team, completedDefinition.UpgradeKind, completedDefinition.Value);
            TeamUnlockState.FindForTeam(team)?.MarkResearchCompleted(completedDefinition.ResearchId);
            activeDefinition = null;
            activeElapsed = 0f;
            lastFailureReason = string.Empty;
        }

        private void ResolveWallet()
        {
            var currentWallet = PlayerResourceWallet.FindForTeam(team);
            if (currentWallet != null && currentWallet != wallet)
            {
                wallet = currentWallet;
            }
        }

        private void Register()
        {
            if (ResearchByTeam.TryGetValue(team, out var existing) && existing != null && existing != this)
            {
                return;
            }

            ResearchByTeam[team] = this;
            registered = true;
        }

        private void Unregister()
        {
            if (!registered)
            {
                return;
            }

            if (ResearchByTeam.TryGetValue(team, out var research) && research == this)
            {
                ResearchByTeam.Remove(team);
            }

            registered = false;
        }
    }
}

namespace ProjectS.Unlocks
{
    public enum UnlockRequirementKind
    {
        None,
        TeamUnlock
    }

    public sealed class TeamUnlockState : MonoBehaviour
    {
        private static readonly Dictionary<UnitTeam, TeamUnlockState> StatesByTeam =
            new Dictionary<UnitTeam, TeamUnlockState>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private string[] initiallyUnlockedIds = Array.Empty<string>();
        [SerializeField] private UnlockRuleCatalog ruleCatalog;

        private readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedResearchIds = new HashSet<string>(StringComparer.Ordinal);
        private bool initialized;

        public UnitTeam Team => team;

        private void OnEnable()
        {
            InitializeUnlocks();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public static TeamUnlockState FindForTeam(UnitTeam team)
        {
            return StatesByTeam.TryGetValue(team, out var state) ? state : null;
        }

        public static bool IsUnlocked(UnitTeam team, string unlockId)
        {
            if (string.IsNullOrWhiteSpace(unlockId))
            {
                return true;
            }

            var state = FindForTeam(team);
            return state != null && state.IsUnlocked(unlockId);
        }

        public static bool IsResearchCompleted(UnitTeam team, string researchId)
        {
            if (string.IsNullOrWhiteSpace(researchId))
            {
                return true;
            }

            var state = FindForTeam(team);
            return state != null && state.IsResearchCompleted(researchId);
        }

        public static bool CanProduce(
            UnitTeam team,
            PrototypeUnitType unitType,
            string action,
            string itemName,
            out string failureReason)
        {
            var catalog = ResolveRuleCatalog(team);
            if (catalog == null)
            {
                failureReason = string.Empty;
                return true;
            }

            return catalog.CanAccess(
                UnlockRuleTargetType.Unit,
                unitType.ToString(),
                team,
                action,
                itemName,
                out failureReason);
        }

        public static bool CanBuild(
            UnitTeam team,
            BuildingKind buildingKind,
            string action,
            string itemName,
            out string failureReason)
        {
            var catalog = ResolveRuleCatalog(team);
            if (catalog == null)
            {
                failureReason = string.Empty;
                return true;
            }

            return catalog.CanAccess(
                UnlockRuleTargetType.Building,
                buildingKind.ToString(),
                team,
                action,
                itemName,
                out failureReason);
        }

        public void Configure(UnitTeam ownerTeam, string[] unlockedIdsAtStart = null)
        {
            Unregister();
            team = ownerTeam;
            initiallyUnlockedIds = unlockedIdsAtStart ?? Array.Empty<string>();
            completedResearchIds.Clear();
            initialized = false;
            InitializeUnlocks();
            if (isActiveAndEnabled)
            {
                Register();
            }
        }

        public bool IsUnlocked(string unlockId)
        {
            InitializeUnlocks();
            return !string.IsNullOrWhiteSpace(unlockId) && unlockedIds.Contains(unlockId.Trim());
        }

        public bool IsResearchCompleted(string researchId)
        {
            return !string.IsNullOrWhiteSpace(researchId)
                && completedResearchIds.Contains(researchId.Trim());
        }

        public void ConfigureRuleCatalog(UnlockRuleCatalog catalog)
        {
            ruleCatalog = catalog;
        }

        public void Unlock(string unlockId)
        {
            InitializeUnlocks();
            if (!string.IsNullOrWhiteSpace(unlockId))
            {
                unlockedIds.Add(unlockId.Trim());
            }
        }

        public void Revoke(string unlockId)
        {
            InitializeUnlocks();
            if (!string.IsNullOrWhiteSpace(unlockId))
            {
                unlockedIds.Remove(unlockId.Trim());
            }
        }

        public void MarkResearchCompleted(string researchId)
        {
            if (!string.IsNullOrWhiteSpace(researchId))
            {
                completedResearchIds.Add(researchId.Trim());
            }
        }

        public void RevokeResearch(string researchId)
        {
            if (!string.IsNullOrWhiteSpace(researchId))
            {
                completedResearchIds.Remove(researchId.Trim());
            }
        }

        private void InitializeUnlocks()
        {
            if (initialized)
            {
                return;
            }

            unlockedIds.Clear();
            var startingIds = initiallyUnlockedIds ?? Array.Empty<string>();
            for (var i = 0; i < startingIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(startingIds[i]))
                {
                    unlockedIds.Add(startingIds[i].Trim());
                }
            }

            initialized = true;
        }

        private void Register()
        {
            if (StatesByTeam.TryGetValue(team, out var existing) && existing != null && existing != this)
            {
                return;
            }

            StatesByTeam[team] = this;
        }

        private void Unregister()
        {
            if (StatesByTeam.TryGetValue(team, out var state) && state == this)
            {
                StatesByTeam.Remove(team);
            }
        }

        private static UnlockRuleCatalog ResolveRuleCatalog(UnitTeam team)
        {
            var state = FindForTeam(team);
            if (state != null)
            {
                if (state.ruleCatalog == null)
                {
                    state.ruleCatalog = UnlockRuleCatalog.LoadDefault();
                }

                return state.ruleCatalog;
            }

            return UnlockRuleCatalog.LoadDefault();
        }
    }

    [Serializable]
    public sealed class UnlockRequirement
    {
        [SerializeField] private UnlockRequirementKind kind;
        [SerializeField] private string requiredUnlockId = string.Empty;

        public UnlockRequirementKind Kind => kind;
        public string RequiredUnlockId => requiredUnlockId;

        public bool IsMet(UnitTeam team)
        {
            switch (kind)
            {
                case UnlockRequirementKind.None:
                    return true;
                case UnlockRequirementKind.TeamUnlock:
                    return TeamUnlockState.IsUnlocked(team, requiredUnlockId);
                default:
                    return false;
            }
        }

        public string GetFailureReason(string action, string itemName)
        {
            switch (kind)
            {
                case UnlockRequirementKind.TeamUnlock:
                    return $"Cannot {action} {itemName}: requires team unlock '{requiredUnlockId}'.";
                default:
                    return $"Cannot {action} {itemName}: unlock requirement is not met.";
            }
        }

        public void ConfigureTeamUnlock(string unlockId)
        {
            kind = UnlockRequirementKind.TeamUnlock;
            requiredUnlockId = unlockId?.Trim() ?? string.Empty;
        }

        public static bool AreMet(
            IReadOnlyList<UnlockRequirement> requirements,
            UnitTeam team,
            string action,
            string itemName,
            out string failureReason)
        {
            if (requirements != null)
            {
                for (var i = 0; i < requirements.Count; i++)
                {
                    var requirement = requirements[i];
                    if (requirement != null && !requirement.IsMet(team))
                    {
                        failureReason = requirement.GetFailureReason(action, itemName);
                        return false;
                    }
                }
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
