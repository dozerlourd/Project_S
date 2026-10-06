using System;
using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Unlocks;
using ProjectS.Units;
using ProjectS.Upgrades;
using UnityEngine;

namespace ProjectS.AI
{
    public sealed class SimpleSkirmishAI : MonoBehaviour
    {
        [SerializeField] private UnitTeam team = UnitTeam.Team2;
        [SerializeField] private UnitTeam enemyTeam = UnitTeam.Team1;
        [SerializeField, Min(0.1f)] private float decisionInterval = 1f;
        [SerializeField, Min(0)] private int desiredWorkers = 4;
        [SerializeField, Min(1)] private int attackGroupSize = 7;
        [SerializeField, Min(0.1f)] private float attackCommandInterval = 18f;
        [SerializeField, Min(0f)] private float initialAttackDelay = 90f;
        [SerializeField, Min(0)] private int supplyBuffer = 2;
        [SerializeField, Min(0.1f)] private float constructionPlacementDistance = 4f;
        [SerializeField, Min(0.1f)] private float constructionRetryInterval = 6f;
        [SerializeField, Min(1f)] private float pendingConstructionStartTimeout = 15f;
        [SerializeField, Min(0.1f)] private float defenceRadius = 8f;
        [SerializeField, Min(1)] private int maximumMainBases = 2;
        [SerializeField, Min(0)] private int minimumCombatUnitsForExpansion = 5;
        [SerializeField, Min(0.1f)] private float expansionAttemptInterval = 12f;
        [SerializeField, Min(0.1f)] private float expansionMinimumDropOffDistance = 8f;
        [SerializeField, Min(0.1f)] private float expansionSiteOffset = 3f;
        [SerializeField, Min(1)] private int expansionCandidateSearchRadius = 8;
        [SerializeField] private PrototypeUnitType defaultWorkerType = PrototypeUnitType.Worker;
        [SerializeField, Min(1)] private int occasionalUnitFrequency = 5;
        [SerializeField, Min(0)] private int minimumCombatUnitsForOccasionalUnit = 5;
        [SerializeField, Min(1)] private int specializedUnitFrequency = 4;
        [SerializeField, Min(0)] private int minimumCombatUnitsForSpecializedUnit = 6;
        [SerializeField, Min(0.1f)] private float specializedConstructionAttemptInterval = 8f;
        [SerializeField, Min(0)] private int minimumCombatUnitsForResearch = 6;
        [SerializeField] private ResourceAmount researchResourceReserve = new ResourceAmount(200, 50);
        [SerializeField, Min(0.1f)] private float researchAttemptInterval = 5f;
        [SerializeField] private ResourceType preferredResourceType = ResourceType.Minerals;
        [SerializeField] private Vector3 fallbackAttackPoint;

        private float nextDecisionTime;
        private float nextAttackCommandTime;
        private float nextResearchAttemptTime;
        private float nextSpecializedConstructionAttemptTime;
        private float nextConstructionAttemptTime;
        private float nextExpansionAttemptTime;
        private float pendingConstructionIssuedAt;
        private int successfulCombatProductions;
        private int successfulSpecializedProductions;
        private int nextSpecializedUnitIndex;
        private ConstructionSite pendingConstructionSite;
        private BuildingKind pendingConstructionKind;
        private bool hasPendingConstruction;
        private AiBuildingTemplateRegistry buildingTemplates;

        public void Configure(
            UnitTeam controlledTeam,
            UnitTeam targetTeam,
            int workerTarget,
            int attackSize,
            Vector3 attackPoint)
        {
            team = controlledTeam;
            enemyTeam = targetTeam;
            desiredWorkers = Mathf.Max(0, workerTarget);
            attackGroupSize = Mathf.Max(1, attackSize);
            fallbackAttackPoint = attackPoint;
        }

        public void ConfigureTempo(float decisionSeconds, float attackSeconds)
        {
            decisionInterval = Mathf.Max(0.1f, decisionSeconds);
            attackCommandInterval = Mathf.Max(0.1f, attackSeconds);
        }

        private void Start()
        {
            nextAttackCommandTime = Mathf.Max(nextAttackCommandTime, Time.time + initialAttackDelay);
        }

        private void Update()
        {
            if (ProjectS.RtsMatchController.ActiveInstance != null
                && ProjectS.RtsMatchController.ActiveInstance.IsMatchOver)
            {
                enabled = false;
                return;
            }

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            AssignIdleWorkersToResources();
            RunBaseOperations();
            RunProduction();
            RunEasyResearch();
            IssueAttackIfReady();
        }

        private void AssignIdleWorkersToResources()
        {
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.Mode != UnitCommandMode.Idle)
                {
                    continue;
                }

                var status = unit.Status;
                if (status == null || !status.CanGatherResources)
                {
                    continue;
                }

                var node = ResourceNode.FindNearestAvailable(unit.transform.position, preferredResourceType);
                if (node != null)
                {
                    unit.Issue(new UnitCommand(UnitCommandMode.Interact, node.InteractionPoint, null, node, false));
                }
            }
        }

        private void RunProduction()
        {
            var workers = CountUnits(defaultWorkerType);
            var combatUnits = CountCombatUnits();
            var buildings = BuildingRegistry.GetBuildings(team);
            var specializedQueues = new List<UnitProductionQueue>(3);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.Completed)
                {
                    continue;
                }

                var queue = building.GetComponent<UnitProductionQueue>();
                if (queue == null || queue.QueuedCount >= queue.MaxQueueSize)
                {
                    continue;
                }

                if (IsSpecializedProductionBuilding(building.Kind))
                {
                    specializedQueues.Add(queue);
                    continue;
                }

                if (workers < desiredWorkers && queue.TryEnqueue(defaultWorkerType))
                {
                    workers++;
                    continue;
                }

                if (TryEnqueueEasyCombatUnit(queue, combatUnits))
                {
                    combatUnits++;
                }
            }

            TryEnqueueSpecializedCombatUnit(specializedQueues, combatUnits);
        }

        private bool TryEnqueueEasyCombatUnit(UnitProductionQueue queue, int currentCombatUnits)
        {
            var desiredType = SelectEasyCombatUnitType(currentCombatUnits);
            if (TryEnqueueAvailableUnit(queue, desiredType))
            {
                successfulCombatProductions++;
                return true;
            }

            if (desiredType != PrototypeUnitType.Soldier
                && TryEnqueueAvailableUnit(queue, PrototypeUnitType.Soldier))
            {
                successfulCombatProductions++;
                return true;
            }

            return false;
        }

        private PrototypeUnitType SelectEasyCombatUnitType(int currentCombatUnits)
        {
            if (currentCombatUnits < minimumCombatUnitsForOccasionalUnit)
            {
                return PrototypeUnitType.Soldier;
            }

            var frequency = Mathf.Max(1, occasionalUnitFrequency);
            var nextProductionNumber = successfulCombatProductions + 1;
            if (nextProductionNumber % frequency != 0)
            {
                return PrototypeUnitType.Soldier;
            }

            var supportProductionNumber = nextProductionNumber / frequency;
            return supportProductionNumber % 2 == 1
                ? PrototypeUnitType.Ranger
                : PrototypeUnitType.Striker;
        }

        private bool TryEnqueueSpecializedCombatUnit(
            IReadOnlyList<UnitProductionQueue> queues,
            int currentCombatUnits)
        {
            if (!ShouldAttemptSpecializedProduction(currentCombatUnits))
            {
                return false;
            }

            for (var offset = 0; offset < 3; offset++)
            {
                var candidateIndex = (nextSpecializedUnitIndex + offset) % 3;
                var candidateType = GetSpecializedUnitType(candidateIndex);
                for (var queueIndex = 0; queueIndex < queues.Count; queueIndex++)
                {
                    var queue = queues[queueIndex];
                    if (queue == null || queue.QueuedCount >= queue.MaxQueueSize)
                    {
                        continue;
                    }

                    if (!TryEnqueueAvailableUnit(queue, candidateType))
                    {
                        continue;
                    }

                    successfulSpecializedProductions++;
                    nextSpecializedUnitIndex = (candidateIndex + 1) % 3;
                    return true;
                }
            }

            return false;
        }

        private bool ShouldAttemptSpecializedProduction(int currentCombatUnits)
        {
            if (currentCombatUnits < minimumCombatUnitsForSpecializedUnit)
            {
                return false;
            }

            var frequency = Mathf.Max(1, specializedUnitFrequency);
            return successfulCombatProductions >= (successfulSpecializedProductions + 1) * frequency;
        }

        private static bool IsSpecializedProductionBuilding(BuildingKind buildingKind)
        {
            return buildingKind == BuildingKind.VehicleFactory
                || buildingKind == BuildingKind.MaintenanceBay
                || buildingKind == BuildingKind.SignalRelay;
        }

        private static PrototypeUnitType GetSpecializedUnitType(int index)
        {
            switch (index % 3)
            {
                case 0: return PrototypeUnitType.Siege;
                case 1: return PrototypeUnitType.Medic;
                default: return PrototypeUnitType.Scout;
            }
        }

        private static bool TryEnqueueAvailableUnit(UnitProductionQueue queue, PrototypeUnitType unitType)
        {
            var definition = FindProductionDefinition(queue, unitType);
            return definition != null
                && queue.CanEnqueue(definition, out _)
                && queue.TryEnqueue(definition);
        }

        private static UnitProductionDefinition FindProductionDefinition(
            UnitProductionQueue queue,
            PrototypeUnitType unitType)
        {
            if (queue == null)
            {
                return null;
            }

            var definitions = queue.ProducibleUnits;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.UnitType == unitType)
                {
                    return definition;
                }
            }

            return null;
        }

        private void RunEasyResearch()
        {
            if (Time.time < nextResearchAttemptTime)
            {
                return;
            }

            nextResearchAttemptTime = Time.time + researchAttemptInterval;
            if (CountCombatUnits() < minimumCombatUnitsForResearch)
            {
                return;
            }

            var research = TeamUpgradeResearch.FindForTeam(team);
            if (research == null || research.ActiveDefinition != null)
            {
                return;
            }

            var definition = FindNextEasyUpgrade(research);
            var wallet = PlayerResourceWallet.FindForTeam(team);
            if (definition == null || !HasResearchBudget(wallet, definition))
            {
                return;
            }

            research.TryStartResearch(definition);
        }

        private static UnitUpgradeDefinition FindNextEasyUpgrade(TeamUpgradeResearch research)
        {
            var weapon = FindUpgradeDefinition(research, UnitUpgradeKind.AttackDamage);
            if (weapon == null || research.GetStatus(weapon) != UnitUpgradeResearchStatus.Completed)
            {
                return weapon != null && research.GetStatus(weapon) == UnitUpgradeResearchStatus.Available
                    ? weapon
                    : null;
            }

            var mobility = FindUpgradeDefinition(research, UnitUpgradeKind.MovementSpeed);
            return mobility != null && research.GetStatus(mobility) == UnitUpgradeResearchStatus.Available
                ? mobility
                : null;
        }

        private static UnitUpgradeDefinition FindUpgradeDefinition(
            TeamUpgradeResearch research,
            UnitUpgradeKind upgradeKind)
        {
            var definitions = research.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.UpgradeKind == upgradeKind)
                {
                    return definition;
                }
            }

            return null;
        }

        private bool HasResearchBudget(PlayerResourceWallet wallet, UnitUpgradeDefinition definition)
        {
            return wallet != null
                && definition != null
                && wallet.Minerals >= definition.Cost.Minerals + researchResourceReserve.Minerals
                && wallet.Gas >= definition.Cost.Gas + researchResourceReserve.Gas;
        }

        private void RunBaseOperations()
        {
            RefreshPendingConstruction();

            if (hasPendingConstruction || Time.time < nextConstructionAttemptTime)
            {
                return;
            }

            if (NeedsSupplyDepot() && TryGetBuildingKind("SupplyDepot", out var supplyDepotKind))
            {
                TryBeginConstruction(supplyDepotKind);
                return;
            }

            if (!HasCombatProductionBuilding())
            {
                TryBeginConstruction(BuildingKind.Production);
                return;
            }

            if (TryBeginSpecializedProductionBuilding())
            {
                return;
            }

            TryBeginExpansion();
        }

        private bool TryBeginSpecializedProductionBuilding()
        {
            var combatUnits = CountCombatUnits();
            if (Time.time < nextSpecializedConstructionAttemptTime
                || !ShouldAttemptSpecializedProduction(combatUnits))
            {
                return false;
            }

            nextSpecializedConstructionAttemptTime = Time.time + Mathf.Max(0.1f, specializedConstructionAttemptInterval);
            ResolveBuildingTemplates();
            if (buildingTemplates == null || buildingTemplates.Team != team)
            {
                return false;
            }

            for (var offset = 0; offset < 3; offset++)
            {
                var candidateIndex = (nextSpecializedUnitIndex + offset) % 3;
                var unitType = GetSpecializedUnitType(candidateIndex);
                var buildingKind = GetSpecializedProductionBuilding(candidateIndex);
                if (HasCompletedBuilding(buildingKind))
                {
                    if (HasUsableSpecializedProductionQueue(buildingKind, unitType))
                    {
                        return false;
                    }

                    continue;
                }

                if (!TeamUnlockState.CanBuild(team, buildingKind, "build", buildingKind.ToString(), out _)
                    || !buildingTemplates.TryGetTemplate(
                        buildingKind,
                        out _,
                        out var cost,
                        out _,
                        out var footprint))
                {
                    continue;
                }

                var wallet = buildingTemplates.Wallet;
                if (!cost.IsEmpty && (wallet == null || !wallet.CanAfford(cost)))
                {
                    continue;
                }

                var desiredPosition = GetConstructionPosition(buildingKind);
                if (!ConstructionSite.TryFindNearestValidPlacement(
                        buildingTemplates.TilemapWorld,
                        desiredPosition,
                        footprint,
                        expansionCandidateSearchRadius,
                        out var placementPosition))
                {
                    continue;
                }

                return TryBeginConstructionAt(buildingKind, placementPosition);
            }

            return false;
        }

        private bool HasUsableSpecializedProductionQueue(
            BuildingKind buildingKind,
            PrototypeUnitType unitType)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || !building.Completed
                    || !building.gameObject.activeInHierarchy
                    || building.Kind != buildingKind)
                {
                    continue;
                }

                var queue = building.GetComponent<UnitProductionQueue>();
                var definition = FindProductionDefinition(queue, unitType);
                if (definition != null
                    && definition.CanMeetAdditionalProductionConditions(team, "produce", out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static BuildingKind GetSpecializedProductionBuilding(int index)
        {
            switch (index % 3)
            {
                case 0: return BuildingKind.VehicleFactory;
                case 1: return BuildingKind.MaintenanceBay;
                default: return BuildingKind.SignalRelay;
            }
        }

        private bool NeedsSupplyDepot()
        {
            var supply = SupplyManager.FindForTeam(team);
            return supply != null && supply.AvailableSupply <= supplyBuffer;
        }

        private bool HasCombatProductionBuilding()
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null
                    && building.Completed
                    && building.gameObject.activeInHierarchy
                    && building.Kind == BuildingKind.Production)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryBeginConstruction(BuildingKind buildingKind)
        {
            TryBeginConstructionAt(buildingKind, GetConstructionPosition(buildingKind));
        }

        private bool TryBeginConstructionAt(BuildingKind buildingKind, Vector3 position)
        {
            var builder = FindNearestBuilder(GetConstructionAnchor());
            if (builder == null)
            {
                ScheduleConstructionRetry();
                return false;
            }

            if (!TryRequestConstructionSite(buildingKind, position, out var site) || site == null)
            {
                ScheduleConstructionRetry();
                return false;
            }

            pendingConstructionSite = site;
            pendingConstructionKind = buildingKind;
            pendingConstructionIssuedAt = Time.time;
            hasPendingConstruction = true;
            builder.Issue(new UnitCommand(UnitCommandMode.Interact, site.InteractionPoint, null, site, false));
            return true;
        }

        private void TryBeginExpansion()
        {
            if (Time.time < nextExpansionAttemptTime)
            {
                return;
            }

            nextExpansionAttemptTime = Time.time + Mathf.Max(0.1f, expansionAttemptInterval);
            if (CountCombatUnits() < minimumCombatUnitsForExpansion
                || CountCompletedMainBases() >= maximumMainBases)
            {
                return;
            }

            var resourceNode = FindExpansionResourceNode();
            if (resourceNode == null)
            {
                return;
            }

            var candidatePositions = GetExpansionCandidatePositions(resourceNode);
            for (var i = 0; i < candidatePositions.Count; i++)
            {
                if (TryBeginConstructionAt(
                        BuildingKind.MainBase,
                        candidatePositions[i]))
                {
                    return;
                }
            }
        }

        private List<Vector3> GetExpansionCandidatePositions(ResourceNode resourceNode)
        {
            var candidates = new List<Vector3>();
            if (resourceNode == null)
            {
                return candidates;
            }

            var minimumRadius = Mathf.Max(1, Mathf.RoundToInt(expansionSiteOffset));
            var maximumRadius = Mathf.Max(minimumRadius, expansionCandidateSearchRadius);
            for (var radius = minimumRadius; radius <= maximumRadius; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        candidates.Add(resourceNode.transform.position + new Vector3(x, y, 0f));
                    }
                }
            }

            return candidates;
        }

        private ResourceNode FindExpansionResourceNode()
        {
            ResourceNode best = null;
            var bestDistance = float.PositiveInfinity;
            var anchor = GetConstructionAnchor();
            var nodes = ResourceNode.AllNodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || !node.isActiveAndEnabled || !node.CanGather())
                {
                    continue;
                }

                var dropOff = ResourceDropOff.FindNearest(team, node.transform.position);
                if (dropOff != null
                    && Vector3.Distance(dropOff.transform.position, node.transform.position) < expansionMinimumDropOffDistance)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(node.transform.position - anchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            return best;
        }

        private int CountCompletedMainBases()
        {
            var count = 0;
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null
                    && building.Completed
                    && building.gameObject.activeInHierarchy
                    && building.Kind == BuildingKind.MainBase)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshPendingConstruction()
        {
            if (!hasPendingConstruction)
            {
                return;
            }

            if (pendingConstructionSite != null && !pendingConstructionSite.Completed)
            {
                if (!pendingConstructionSite.HasConstructionStarted
                    && Time.time >= pendingConstructionIssuedAt + pendingConstructionStartTimeout)
                {
                    pendingConstructionSite.CancelPendingConstruction();
                    ClearPendingConstruction();
                    ScheduleConstructionRetry();
                }

                return;
            }

            var completed = pendingConstructionSite != null && pendingConstructionSite.Completed;
            ClearPendingConstruction();
            if (HasCompletedBuilding(pendingConstructionKind))
            {
                return;
            }

            if (!completed)
            {
                ScheduleConstructionRetry();
            }
        }

        private void ClearPendingConstruction()
        {
            hasPendingConstruction = false;
            pendingConstructionSite = null;
            pendingConstructionIssuedAt = 0f;
        }

        private void ScheduleConstructionRetry()
        {
            nextConstructionAttemptTime = Time.time + Mathf.Max(0.1f, constructionRetryInterval);
        }

        private bool HasCompletedBuilding(BuildingKind buildingKind)
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null
                    && building.Completed
                    && building.gameObject.activeInHierarchy
                    && building.Kind == buildingKind)
                {
                    return true;
                }
            }

            return false;
        }

        private UnitCommandAgent FindNearestBuilder(Vector3 position)
        {
            UnitCommandAgent nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var status = unit != null ? unit.Status : null;
                if (status == null || !status.Roles.HasFlag(UnitRole.Builder))
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(unit.transform.position - position);
                if (distance < nearestDistance)
                {
                    nearest = unit;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private Vector3 GetConstructionAnchor()
        {
            var buildings = BuildingRegistry.GetBuildings(team);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null && building.Completed && building.Kind == BuildingKind.MainBase)
                {
                    return building.transform.position;
                }
            }

            return buildings.Count > 0 && buildings[0] != null
                ? buildings[0].transform.position
                : transform.position;
        }

        private Vector3 GetConstructionPosition(BuildingKind buildingKind)
        {
            var anchor = GetConstructionAnchor();
            var direction = buildingKind == BuildingKind.Production ? Vector3.right : Vector3.up;
            return anchor + direction * constructionPlacementDistance;
        }

        private bool TryRequestConstructionSite(BuildingKind buildingKind, Vector3 position, out ConstructionSite site)
        {
            site = null;
            ResolveBuildingTemplates();
            if (buildingTemplates == null || buildingTemplates.Team != team)
            {
                return false;
            }

            return buildingTemplates.TryCreateConstructionSite(buildingKind, position, out site);
        }

        private void ResolveBuildingTemplates()
        {
            if (buildingTemplates != null)
            {
                return;
            }

            buildingTemplates = GetComponent<AiBuildingTemplateRegistry>();
            if (buildingTemplates == null)
            {
                buildingTemplates = FindFirstObjectByType<AiBuildingTemplateRegistry>();
            }
        }

        private static bool TryGetBuildingKind(string name, out BuildingKind buildingKind)
        {
            return Enum.TryParse(name, out buildingKind) && Enum.IsDefined(typeof(BuildingKind), buildingKind);
        }

        private void IssueAttackIfReady()
        {
            if (IssueDefenceIfThreatened())
            {
                return;
            }

            if (Time.time < nextAttackCommandTime)
            {
                return;
            }

            var combatCount = CountCombatUnits();
            if (combatCount < attackGroupSize)
            {
                return;
            }

            var target = FindAttackTarget();
            nextAttackCommandTime = Time.time + attackCommandInterval;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var status = unit != null ? unit.Status : null;
                if (status == null || !status.Roles.HasFlag(UnitRole.Combat))
                {
                    continue;
                }

                unit.Issue(new UnitCommand(UnitCommandMode.AttackMove, target, null, false));
            }
        }

        private bool IssueDefenceIfThreatened()
        {
            var threat = FindDefenceThreat();
            if (threat == null)
            {
                return false;
            }

            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var status = unit != null ? unit.Status : null;
                if (status != null && status.Roles.HasFlag(UnitRole.Combat))
                {
                    unit.Issue(new UnitCommand(UnitCommandMode.AttackMove, threat.transform.position, null, false));
                }
            }

            return true;
        }

        private UnitCommandAgent FindDefenceThreat()
        {
            var enemyUnits = UnitRegistry.GetAgents(enemyTeam);
            var buildings = BuildingRegistry.GetBuildings(team);
            UnitCommandAgent closestThreat = null;
            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < enemyUnits.Count; i++)
            {
                var enemy = enemyUnits[i];
                var enemyStatus = enemy != null ? enemy.Status : null;
                if (enemyStatus == null || !enemyStatus.Roles.HasFlag(UnitRole.Combat))
                {
                    continue;
                }

                for (var buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
                {
                    var building = buildings[buildingIndex];
                    if (!IsDefendedBuilding(building))
                    {
                        continue;
                    }

                    var distance = Vector3.SqrMagnitude(enemy.transform.position - building.transform.position);
                    if (distance <= defenceRadius * defenceRadius && distance < closestDistance)
                    {
                        closestThreat = enemy;
                        closestDistance = distance;
                    }
                }
            }

            return closestThreat;
        }

        private static bool IsDefendedBuilding(BuildingStatus building)
        {
            return building != null
                && building.Completed
                && building.gameObject.activeInHierarchy
                && (building.Kind == BuildingKind.MainBase
                    || building.Kind == BuildingKind.Production
                    || building.Kind == BuildingKind.SpliterProduction
                    || building.SupplyProvided > 0);
        }

        private int CountUnits(PrototypeUnitType unitType)
        {
            var count = 0;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var status = units[i] != null ? units[i].Status : null;
                if (status != null && status.UnitType == unitType)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountCombatUnits()
        {
            var count = 0;
            var units = UnitRegistry.GetAgents(team);
            for (var i = 0; i < units.Count; i++)
            {
                var status = units[i] != null ? units[i].Status : null;
                if (status != null && status.Roles.HasFlag(UnitRole.Combat))
                {
                    count++;
                }
            }

            return count;
        }

        private Vector3 FindAttackTarget()
        {
            var enemyBuildings = BuildingRegistry.GetBuildings(enemyTeam);
            BuildingStatus bestBuilding = null;
            var bestPriority = AttackTargetPriority.Other;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < enemyBuildings.Count; i++)
            {
                var building = enemyBuildings[i];
                if (building == null || !building.Completed || !building.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var priority = building.TargetPriority;
                var distance = Vector3.Distance(fallbackAttackPoint, building.transform.position);
                if (bestBuilding == null
                    || priority < bestPriority
                    || (priority == bestPriority && distance < bestDistance))
                {
                    bestBuilding = building;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }

            if (bestBuilding != null)
            {
                return bestBuilding.transform.position;
            }

            var enemyUnits = UnitRegistry.GetAgents(enemyTeam);
            for (var i = 0; i < enemyUnits.Count; i++)
            {
                var unit = enemyUnits[i];
                if (unit != null && unit.gameObject.activeInHierarchy)
                {
                    return unit.transform.position;
                }
            }

            return fallbackAttackPoint;
        }
    }
}
