using System.Collections.Generic;
using ProjectS.Resources;
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

            completedUpgrades.Add(activeDefinition.UpgradeKind);
            UnitUpgradeStatModifiers.SetUpgrade(team, activeDefinition.UpgradeKind, activeDefinition.Value);
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
