using System.Collections.Generic;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class UnitProductionQueue : MonoBehaviour, IUnitRallyPointService
    {
        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private SupplyManager supplyManager;
        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private UnitProductionDefinition[] producibleUnits = new UnitProductionDefinition[0];
        [SerializeField, Min(1)] private int maxQueueSize = 5;
        [SerializeField] private Vector3 spawnOffset = new Vector3(1.5f, 0f, 0f);
        [SerializeField] private Vector3 rallyOffset = new Vector3(3f, 0f, 0f);

        private readonly Queue<UnitProductionDefinition> queue = new Queue<UnitProductionDefinition>();
        private BuildingStatus status;
        private UnitProductionDefinition activeProduction;
        private float activeProgress;
        private bool hasRallyPoint;
        private Vector3 rallyPoint;
        private string lastEnqueueFailureReason;
        private string lastCancellationFailureReason;

        public IReadOnlyList<UnitProductionDefinition> ProducibleUnits
        {
            get
            {
                RemoveUnsupportedDefinitions();
                return producibleUnits;
            }
        }
        public int PendingCount => queue.Count;
        public int QueuedCount => queue.Count + (activeProduction != null ? 1 : 0);
        public int MaxQueueSize => Mathf.Max(1, maxQueueSize);
        public UnitProductionDefinition ActiveProduction => activeProduction;
        public UnitTeam Team => status != null ? status.Team : UnitTeam.Team1;
        public string LastEnqueueFailureReason => lastEnqueueFailureReason;
        public string LastCancellationFailureReason => lastCancellationFailureReason;
        public float ActiveProgress => activeProgress;
        public float ActiveProgress01 => activeProduction != null
            ? Mathf.Clamp01(activeProgress / activeProduction.ProductionTime)
            : 0f;
        public Vector3 RallyPoint => hasRallyPoint ? rallyPoint : transform.position + rallyOffset;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            CancelAllProduction();
        }

        private void Update()
        {
            if (ProjectS.RtsMatchController.ActiveInstance != null
                && ProjectS.RtsMatchController.ActiveInstance.IsMatchOver)
            {
                return;
            }

            ResolveReferences();
            if (!CanProduce())
            {
                return;
            }

            if (activeProduction == null)
            {
                TryStartNextProduction();
            }

            if (activeProduction == null)
            {
                return;
            }

            activeProgress += Time.deltaTime;
            if (activeProgress >= activeProduction.ProductionTime)
            {
                CompleteActiveProduction();
            }
        }

        public bool TryEnqueue(int definitionIndex)
        {
            RemoveUnsupportedDefinitions();
            if (definitionIndex < 0 || definitionIndex >= producibleUnits.Length)
            {
                return FailEnqueue($"Invalid production definition index: {definitionIndex}.");
            }

            return TryEnqueue(producibleUnits[definitionIndex]);
        }

        public bool TryEnqueue(PrototypeUnitType unitType)
        {
            RemoveUnsupportedDefinitions();
            for (var i = 0; i < producibleUnits.Length; i++)
            {
                var definition = producibleUnits[i];
                if (definition != null && definition.UnitType == unitType)
                {
                    return TryEnqueue(definition);
                }
            }

            return FailEnqueue($"No producible unit definition found for {unitType}.");
        }

        public bool TryEnqueue(UnitProductionDefinition definition)
        {
            ResolveReferences();
            if (!CanEnqueue(definition, out var failureReason))
            {
                return FailEnqueue(failureReason);
            }

            if (wallet != null && !definition.Cost.IsEmpty && !wallet.TrySpend(definition.Cost))
            {
                return FailEnqueue($"Cannot enqueue {definition.DisplayName}: insufficient resources for cost ({definition.Cost}).");
            }

            if (!TryReserveSupply(definition, out failureReason))
            {
                if (wallet != null && !definition.Cost.IsEmpty)
                {
                    wallet.Add(definition.Cost);
                }

                return FailEnqueue(failureReason);
            }

            queue.Enqueue(definition);
            lastEnqueueFailureReason = string.Empty;
            lastCancellationFailureReason = string.Empty;
            TryStartNextProduction();
            return true;
        }

        public bool CanEnqueue(UnitProductionDefinition definition, out string failureReason)
        {
            ResolveReferences();
            RemoveUnsupportedDefinitions();
            if (ProjectS.RtsMatchController.ActiveInstance != null
                && ProjectS.RtsMatchController.ActiveInstance.IsMatchOver)
            {
                failureReason = "Cannot enqueue production: match has ended.";
                return false;
            }

            if (definition == null)
            {
                failureReason = "Cannot enqueue production: definition is missing.";
                return false;
            }

            if (status != null && !definition.CanBeProducedAt(status.Kind))
            {
                failureReason = definition.GetProductionBuildingFailureReason(status.Kind);
                return false;
            }

            if (definition.UnitPrefab == null)
            {
                failureReason = $"Cannot enqueue {definition.DisplayName}: unit prefab is missing.";
                return false;
            }

            if (!CanProduce())
            {
                failureReason = "Cannot enqueue production: building is not completed.";
                return false;
            }

            var requirements = definition.Requirements;
            for (var i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (requirement != null && !requirement.IsMet(status.Team))
                {
                    failureReason = requirement.GetFailureReason(definition);
                    return false;
                }
            }

            if (status != null && !definition.CanBeProducedBy(status.Team, out failureReason))
            {
                return false;
            }

            if (QueuedCount >= MaxQueueSize)
            {
                failureReason = "Cannot enqueue production: queue is full.";
                return false;
            }

            if (!definition.Cost.IsEmpty && wallet == null)
            {
                failureReason = $"Cannot enqueue {definition.DisplayName}: no resource wallet is available.";
                return false;
            }

            if (wallet != null && !wallet.CanAfford(definition.Cost))
            {
                failureReason = $"Cannot enqueue {definition.DisplayName}: insufficient resources for cost ({definition.Cost}).";
                return false;
            }

            if (supplyManager != null && !supplyManager.CanReserve(GetRequiredSupply(definition)))
            {
                failureReason = $"Cannot enqueue {definition.DisplayName}: insufficient supply ({supplyManager.CurrentSupply + supplyManager.ReservedSupply}/{supplyManager.MaxSupply}).";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public UnitProductionDefinition GetPendingProduction(int index)
        {
            if (index < 0 || index >= queue.Count)
            {
                return null;
            }

            var currentIndex = 0;
            foreach (var definition in queue)
            {
                if (currentIndex == index)
                {
                    return definition;
                }

                currentIndex++;
            }

            return null;
        }

        public bool TryCancelActiveProduction()
        {
            ResolveReferences();
            if (activeProduction == null)
            {
                return FailCancellation("Cannot cancel production: no active production is running.");
            }

            var cancelledProduction = activeProduction;
            if (!TryRefund(cancelledProduction))
            {
                return false;
            }

            activeProduction = null;
            activeProgress = 0f;
            ReleaseSupplyReservation(cancelledProduction);
            lastCancellationFailureReason = string.Empty;
            lastEnqueueFailureReason = string.Empty;
            TryStartNextProduction();
            return true;
        }

        public bool TryCancelPendingProduction(int index)
        {
            ResolveReferences();
            if (index < 0 || index >= queue.Count)
            {
                return FailCancellation($"Cannot cancel pending production: invalid queue index {index}.");
            }

            var originalQueue = new List<UnitProductionDefinition>(queue);
            var cancelledProduction = originalQueue[index];
            if (!TryRefund(cancelledProduction))
            {
                return false;
            }

            ReleaseSupplyReservation(cancelledProduction);

            queue.Clear();
            for (var i = 0; i < originalQueue.Count; i++)
            {
                if (i != index)
                {
                    queue.Enqueue(originalQueue[i]);
                }
            }

            lastCancellationFailureReason = string.Empty;
            lastEnqueueFailureReason = string.Empty;
            return true;
        }

        public void CancelAllProduction()
        {
            ResolveReferences();
            CancelAndRefund(activeProduction);
            activeProduction = null;
            activeProgress = 0f;

            foreach (var definition in queue)
            {
                CancelAndRefund(definition);
            }

            queue.Clear();
            lastEnqueueFailureReason = string.Empty;
            lastCancellationFailureReason = string.Empty;
        }

        public void SetRallyPoint(Vector3 point)
        {
            rallyPoint = point;
            hasRallyPoint = true;
        }

        public void Configure(
            PlayerResourceWallet resourceWallet,
            ProjectSTilemapWorld world,
            UnitProductionDefinition[] definitions,
            int queueSize,
            Vector3 unitSpawnOffset,
            Vector3 unitRallyOffset)
        {
            wallet = resourceWallet;
            tilemapWorld = world;
            producibleUnits = definitions ?? new UnitProductionDefinition[0];
            RemoveUnsupportedDefinitions();
            maxQueueSize = Mathf.Max(1, queueSize);
            spawnOffset = unitSpawnOffset;
            rallyOffset = unitRallyOffset;
        }

        private void TryStartNextProduction()
        {
            if (activeProduction != null || queue.Count <= 0)
            {
                return;
            }

            activeProduction = queue.Dequeue();
            activeProgress = 0f;
        }

        private void CompleteActiveProduction()
        {
            var definition = activeProduction;
            activeProduction = null;
            activeProgress = 0f;
            if (definition == null || definition.UnitPrefab == null)
            {
                TryStartNextProduction();
                return;
            }

            var outputCount = definition.UnitsPerProduction;
            for (var i = 0; i < outputCount; i++)
            {
                var unitObject = Instantiate(definition.UnitPrefab, GetSpawnPosition(i, outputCount), Quaternion.identity);
                var unitStatus = unitObject.GetComponent<PrototypeUnitStatus>();
                if (unitStatus != null && status != null)
                {
                    unitStatus.SetTeam(status.Team);
                    unitStatus.ConfigureSupplyCost(definition.SupplyCost);
                }

                if (supplyManager != null && !supplyManager.CommitReservation(definition.SupplyCost, unitStatus))
                {
                    Debug.LogWarning($"Could not complete supply reservation for {definition.DisplayName}.", this);
                    ReleaseSupplyReservation(definition);
                }

                unitObject.SetActive(true);

                var commandAgent = unitObject.GetComponent<UnitCommandAgent>();
                if (commandAgent != null)
                {
                    commandAgent.Issue(new UnitCommand(UnitCommandMode.Move, RallyPoint, null, false));
                }
            }

            TryStartNextProduction();
        }

        private Vector3 GetSpawnPosition(int outputIndex = 0, int outputCount = 1)
        {
            var spacing = outputCount > 1 ? 0.65f : 0f;
            var position = transform.position + spawnOffset + new Vector3((outputIndex - (outputCount - 1) * 0.5f) * spacing, 0f, 0f);
            if (tilemapWorld == null)
            {
                return position;
            }

            var cell = tilemapWorld.WorldToCell(position);
            if (tilemapWorld.IsWalkable(cell))
            {
                return tilemapWorld.GetCellCenterWorld(cell);
            }

            const int maxRadius = 5;
            for (var radius = 1; radius <= maxRadius; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        var candidate = cell + new Vector3Int(x, y, 0);
                        if (tilemapWorld.IsWalkable(candidate))
                        {
                            return tilemapWorld.GetCellCenterWorld(candidate);
                        }
                    }
                }
            }

            return position;
        }

        private bool CanProduce()
        {
            return status != null && status.Completed;
        }

        private void ResolveReferences()
        {
            if (status == null)
            {
                status = GetComponent<BuildingStatus>();
            }

            if (status != null && (wallet == null || wallet.Team != status.Team))
            {
                wallet = PlayerResourceWallet.FindForTeam(status.Team);
            }

            if (status != null && (supplyManager == null || supplyManager.Team != status.Team))
            {
                supplyManager = SupplyManager.FindForTeam(status.Team);
            }

            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance;
            }
        }

        private bool FailEnqueue(string reason)
        {
            lastEnqueueFailureReason = reason;
            Debug.LogWarning(reason, this);
            return false;
        }

        private bool TryRefund(UnitProductionDefinition definition)
        {
            if (definition == null || definition.Cost.IsEmpty)
            {
                return true;
            }

            if (wallet == null)
            {
                return FailCancellation($"Cannot cancel {definition.DisplayName}: no resource wallet is available.");
            }

            wallet.Add(definition.Cost);
            return true;
        }

        private bool TryReserveSupply(UnitProductionDefinition definition, out string failureReason)
        {
            var requiredSupply = GetRequiredSupply(definition);
            if (requiredSupply <= 0)
            {
                failureReason = string.Empty;
                return true;
            }

            if (supplyManager == null)
            {
                // Standalone test scenes and legacy content can run without the match-level supply system.
                failureReason = string.Empty;
                return true;
            }

            if (!supplyManager.TryReserve(requiredSupply))
            {
                failureReason = $"Cannot enqueue {definition.DisplayName}: {supplyManager.LastFailureReason}";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void ReleaseSupplyReservation(UnitProductionDefinition definition)
        {
            var requiredSupply = GetRequiredSupply(definition);
            if (requiredSupply > 0)
            {
                supplyManager?.ReleaseReservation(requiredSupply);
            }
        }

        private static int GetRequiredSupply(UnitProductionDefinition definition)
        {
            return definition == null ? 0 : definition.SupplyCost * definition.UnitsPerProduction;
        }

        private void CancelAndRefund(UnitProductionDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            TryRefund(definition);
            ReleaseSupplyReservation(definition);
        }

        private bool FailCancellation(string reason)
        {
            lastCancellationFailureReason = reason;
            Debug.LogWarning(reason, this);
            return false;
        }

        private void RemoveUnsupportedDefinitions()
        {
            if (status == null)
            {
                status = GetComponent<BuildingStatus>();
            }

            if (status == null || producibleUnits.Length == 0)
            {
                return;
            }

            var supportedCount = 0;
            for (var i = 0; i < producibleUnits.Length; i++)
            {
                var definition = producibleUnits[i];
                if (definition != null && definition.CanBeProducedAt(status.Kind))
                {
                    supportedCount++;
                }
            }

            if (supportedCount == producibleUnits.Length)
            {
                return;
            }

            var filtered = new UnitProductionDefinition[supportedCount];
            var targetIndex = 0;
            for (var i = 0; i < producibleUnits.Length; i++)
            {
                var definition = producibleUnits[i];
                if (definition != null && definition.CanBeProducedAt(status.Kind))
                {
                    filtered[targetIndex++] = definition;
                }
            }

            producibleUnits = filtered;
        }
    }
}
