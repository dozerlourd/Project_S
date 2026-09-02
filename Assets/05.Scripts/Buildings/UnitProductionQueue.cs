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
                if (definition != null
                    && wallet != null
                    && !definition.Cost.IsEmpty
                    && failureReason.Contains("insufficient resources")
                    && !wallet.CanAfford(definition.Cost))
                {
                    wallet.TrySpend(definition.Cost);
                }

                return FailEnqueue(failureReason);
            }

            if (wallet != null && !definition.Cost.IsEmpty && !wallet.TrySpend(definition.Cost))
            {
                return FailEnqueue($"Cannot enqueue {definition.DisplayName}: insufficient resources for cost ({definition.Cost}).");
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

            if (status != null && status.Kind == BuildingKind.Production && definition.UnitType == PrototypeUnitType.Spliter)
            {
                failureReason = "Spliter can only be produced at a Spliter Production building.";
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

            var spawnPosition = GetSpawnPosition();
            var unitObject = Instantiate(definition.UnitPrefab, spawnPosition, Quaternion.identity);
            unitObject.SetActive(true);
            var unitStatus = unitObject.GetComponent<PrototypeUnitStatus>();
            if (unitStatus != null && status != null)
            {
                unitStatus.SetTeam(status.Team);
            }

            var commandAgent = unitObject.GetComponent<UnitCommandAgent>();
            if (commandAgent != null)
            {
                commandAgent.Issue(new UnitCommand(UnitCommandMode.Move, RallyPoint, null, false));
            }

            TryStartNextProduction();
        }

        private Vector3 GetSpawnPosition()
        {
            var position = transform.position + spawnOffset;
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

            if (status == null || status.Kind != BuildingKind.Production || producibleUnits.Length == 0)
            {
                return;
            }

            var spliterCount = 0;
            for (var i = 0; i < producibleUnits.Length; i++)
            {
                if (producibleUnits[i] != null && producibleUnits[i].UnitType == PrototypeUnitType.Spliter)
                {
                    spliterCount++;
                }
            }

            if (spliterCount == 0)
            {
                return;
            }

            var filtered = new UnitProductionDefinition[producibleUnits.Length - spliterCount];
            var targetIndex = 0;
            for (var i = 0; i < producibleUnits.Length; i++)
            {
                var definition = producibleUnits[i];
                if (definition == null || definition.UnitType != PrototypeUnitType.Spliter)
                {
                    filtered[targetIndex++] = definition;
                }
            }

            producibleUnits = filtered;
        }
    }
}
