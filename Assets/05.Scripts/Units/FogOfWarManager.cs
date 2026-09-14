using System;
using System.Collections.Generic;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Visibility
{
    public enum FogVisibilityState : byte
    {
        Unexplored,
        Explored,
        Visible
    }

    public readonly struct FogObservedEnemy
    {
        public readonly IUnitAttackTarget Target;
        public readonly UnitTeam Team;
        public readonly Vector3 LastObservedPosition;
        public readonly bool IsCurrentlyVisible;

        public FogObservedEnemy(
            IUnitAttackTarget target,
            UnitTeam team,
            Vector3 lastObservedPosition,
            bool isCurrentlyVisible)
        {
            Target = target;
            Team = team;
            LastObservedPosition = lastObservedPosition;
            IsCurrentlyVisible = isCurrentlyVisible;
        }
    }

    public sealed class FogOfWarManager : MonoBehaviour
    {
        private sealed class ProviderCoverage
        {
            public Vector3Int Cell;
            public float Radius;
            public readonly List<int> CellIndices = new List<int>();
        }

        private sealed class EnemyObservation
        {
            public UnitTeam Team;
            public Vector3 LastObservedPosition;
            public bool IsCurrentlyVisible;
        }

        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;

        private readonly Dictionary<IFogVisionProvider, ProviderCoverage> providerCoverages =
            new Dictionary<IFogVisionProvider, ProviderCoverage>();
        private readonly HashSet<IFogVisionProvider> activeProviders = new HashSet<IFogVisionProvider>();
        private readonly List<IFogVisionProvider> providersToRemove = new List<IFogVisionProvider>();
        private readonly Dictionary<IUnitAttackTarget, EnemyObservation> enemyObservations =
            new Dictionary<IUnitAttackTarget, EnemyObservation>();
        private readonly HashSet<IUnitAttackTarget> registeredEnemies = new HashSet<IUnitAttackTarget>();
        private readonly List<IUnitAttackTarget> enemyObservationsToRemove = new List<IUnitAttackTarget>();
        private readonly List<FogObservedEnemy> observedEnemySnapshots = new List<FogObservedEnemy>();
        private FogVisibilityState[] visibilityStates;
        private int[] visibilityCounts;
        private BoundsInt visibilityBounds;
        private int observedRegistryVersion = -1;
        private int observedTerrainRevision = -1;
        private ProjectSTilemapWorld observedWorld;

        public static FogOfWarManager ActiveInstance { get; private set; }
        public UnitTeam PlayerTeam => playerTeam;
        public ProjectSTilemapWorld TilemapWorld => tilemapWorld;
        public int VisibilityRevision { get; private set; }
        public int RebuildCount { get; private set; }
        public BoundsInt VisibilityBounds => visibilityBounds;
        public IReadOnlyList<FogObservedEnemy> ObservedEnemies => observedEnemySnapshots;
        public event Action VisibilityChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeManager()
        {
            if (FindFirstObjectByType<FogOfWarManager>() == null)
            {
                new GameObject("Fog Of War").AddComponent<FogOfWarManager>();
            }
        }

        private void Awake()
        {
            ActiveInstance = this;
            EnsureOverlay();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            observedRegistryVersion = -1;
            observedTerrainRevision = -1;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        private void Update()
        {
            RefreshIfNeeded();
            RefreshEnemyObservations();
        }

        public void Configure(ProjectSTilemapWorld world, UnitTeam team)
        {
            tilemapWorld = world;
            playerTeam = team;
            observedRegistryVersion = -1;
            observedTerrainRevision = -1;
            ClearEnemyObservations();
            RefreshIfNeeded();
            RefreshEnemyObservations();
        }

        public void RefreshNow()
        {
            observedRegistryVersion = -1;
            observedTerrainRevision = -1;
            RefreshIfNeeded();
            RefreshEnemyObservations();
        }

        public FogVisibilityState GetVisibility(Vector3Int cell)
        {
            return TryGetCellIndex(cell, out var index) && visibilityStates != null
                ? visibilityStates[index]
                : FogVisibilityState.Unexplored;
        }

        public FogVisibilityState GetVisibility(Vector3 worldPosition)
        {
            return tilemapWorld != null
                ? GetVisibility(tilemapWorld.WorldToCell(worldPosition))
                : FogVisibilityState.Unexplored;
        }

        public bool IsWorldPositionVisible(Vector3 worldPosition)
        {
            return GetVisibility(worldPosition) == FogVisibilityState.Visible;
        }

        public bool TryGetEnemyObservation(IUnitAttackTarget target, out FogObservedEnemy observation)
        {
            if (target != null && enemyObservations.TryGetValue(target, out var record))
            {
                observation = new FogObservedEnemy(
                    target,
                    record.Team,
                    record.LastObservedPosition,
                    record.IsCurrentlyVisible);
                return true;
            }

            observation = default;
            return false;
        }

        private void RefreshEnemyObservations()
        {
            registeredEnemies.Clear();
            var targets = UnitAttackTargetRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (IsMissingTarget(target)
                    || !target.IsAlive
                    || target.Team == playerTeam
                    || target.SelectionTransform == null)
                {
                    continue;
                }

                registeredEnemies.Add(target);
                var position = target.SelectionTransform.position;
                var isVisible = IsWorldPositionVisible(position);
                if (isVisible)
                {
                    if (!enemyObservations.TryGetValue(target, out var observation))
                    {
                        observation = new EnemyObservation();
                        enemyObservations.Add(target, observation);
                    }

                    observation.Team = target.Team;
                    observation.LastObservedPosition = position;
                    observation.IsCurrentlyVisible = true;
                }
                else if (enemyObservations.TryGetValue(target, out var observation))
                {
                    observation.IsCurrentlyVisible = false;
                }
            }

            enemyObservationsToRemove.Clear();
            foreach (var pair in enemyObservations)
            {
                if (!registeredEnemies.Contains(pair.Key))
                {
                    enemyObservationsToRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < enemyObservationsToRemove.Count; i++)
            {
                enemyObservations.Remove(enemyObservationsToRemove[i]);
            }

            observedEnemySnapshots.Clear();
            foreach (var pair in enemyObservations)
            {
                var observation = pair.Value;
                observedEnemySnapshots.Add(new FogObservedEnemy(
                    pair.Key,
                    observation.Team,
                    observation.LastObservedPosition,
                    observation.IsCurrentlyVisible));
            }
        }

        private void ClearEnemyObservations()
        {
            enemyObservations.Clear();
            registeredEnemies.Clear();
            enemyObservationsToRemove.Clear();
            observedEnemySnapshots.Clear();
        }

        private static bool IsMissingTarget(IUnitAttackTarget target)
        {
            return target == null || target is UnityEngine.Object unityObject && unityObject == null;
        }

        private void RefreshIfNeeded()
        {
            ResolveWorld();
            if (tilemapWorld == null)
            {
                return;
            }

            var terrainChanged = observedWorld != tilemapWorld
                || observedTerrainRevision != tilemapWorld.NavigationRevision
                || visibilityStates == null;
            var providersChanged = observedRegistryVersion != FogOfWarRegistry.GetVersion(playerTeam);
            if (!terrainChanged && !providersChanged)
            {
                return;
            }

            var changed = terrainChanged
                ? RebuildAllVisibility(observedWorld != tilemapWorld)
                : SynchronizeProviderCoverage();

            observedWorld = tilemapWorld;
            observedRegistryVersion = FogOfWarRegistry.GetVersion(playerTeam);
            observedTerrainRevision = tilemapWorld.NavigationRevision;
            RebuildCount++;
            if (changed)
            {
                VisibilityRevision++;
                VisibilityChanged?.Invoke();
            }
        }

        private bool RebuildAllVisibility(bool resetExploration)
        {
            var bounds = tilemapWorld.CellBounds;
            var cellCount = Mathf.Max(0, bounds.size.x * bounds.size.y);
            var boundsChanged = visibilityStates == null
                || visibilityStates.Length != cellCount
                || visibilityBounds != bounds;

            if (boundsChanged || resetExploration)
            {
                visibilityStates = new FogVisibilityState[cellCount];
                visibilityCounts = new int[cellCount];
                visibilityBounds = bounds;
            }
            else
            {
                Array.Clear(visibilityCounts, 0, visibilityCounts.Length);
                for (var i = 0; i < visibilityStates.Length; i++)
                {
                    if (visibilityStates[i] == FogVisibilityState.Visible)
                    {
                        visibilityStates[i] = FogVisibilityState.Explored;
                    }
                }
            }

            providerCoverages.Clear();
            var providers = FogOfWarRegistry.GetProviders(playerTeam);
            for (var i = 0; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (IsEligibleProvider(provider))
                {
                    AddProviderCoverage(provider);
                }
            }

            return true;
        }

        private bool SynchronizeProviderCoverage()
        {
            var changed = false;
            activeProviders.Clear();
            var providers = FogOfWarRegistry.GetProviders(playerTeam);
            for (var i = 0; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (!IsEligibleProvider(provider))
                {
                    continue;
                }

                activeProviders.Add(provider);
                var currentCell = tilemapWorld.WorldToCell(provider.VisionTransform.position);
                var currentRadius = Mathf.Max(0f, provider.VisionRadius);
                if (providerCoverages.TryGetValue(provider, out var coverage))
                {
                    if (coverage.Cell == currentCell && Mathf.Approximately(coverage.Radius, currentRadius))
                    {
                        continue;
                    }

                    RemoveProviderCoverage(coverage);
                    providerCoverages.Remove(provider);
                }

                AddProviderCoverage(provider);
                changed = true;
            }

            providersToRemove.Clear();
            foreach (var pair in providerCoverages)
            {
                if (!activeProviders.Contains(pair.Key))
                {
                    providersToRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < providersToRemove.Count; i++)
            {
                var provider = providersToRemove[i];
                RemoveProviderCoverage(providerCoverages[provider]);
                providerCoverages.Remove(provider);
                changed = true;
            }

            return changed;
        }

        private void AddProviderCoverage(IFogVisionProvider provider)
        {
            var coverage = new ProviderCoverage
            {
                Cell = tilemapWorld.WorldToCell(provider.VisionTransform.position),
                Radius = Mathf.Max(0f, provider.VisionRadius)
            };
            CollectVisibleCells(coverage.Cell, coverage.Radius, coverage.CellIndices);
            for (var i = 0; i < coverage.CellIndices.Count; i++)
            {
                var index = coverage.CellIndices[i];
                visibilityCounts[index]++;
                visibilityStates[index] = FogVisibilityState.Visible;
            }

            providerCoverages.Add(provider, coverage);
        }

        private void RemoveProviderCoverage(ProviderCoverage coverage)
        {
            for (var i = 0; i < coverage.CellIndices.Count; i++)
            {
                var index = coverage.CellIndices[i];
                visibilityCounts[index] = Mathf.Max(0, visibilityCounts[index] - 1);
                if (visibilityCounts[index] == 0)
                {
                    visibilityStates[index] = FogVisibilityState.Explored;
                }
            }
        }

        private void CollectVisibleCells(Vector3Int origin, float radius, List<int> results)
        {
            results.Clear();
            var originWorld = tilemapWorld.GetCellCenterWorld(origin);
            var cellSize = tilemapWorld.Grid != null ? tilemapWorld.Grid.cellSize : Vector3.one;
            var radiusX = Mathf.CeilToInt(radius / Mathf.Max(0.01f, Mathf.Abs(cellSize.x)));
            var radiusY = Mathf.CeilToInt(radius / Mathf.Max(0.01f, Mathf.Abs(cellSize.y)));
            var radiusSquared = radius * radius;

            for (var y = origin.y - radiusY; y <= origin.y + radiusY; y++)
            {
                for (var x = origin.x - radiusX; x <= origin.x + radiusX; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!TryGetCellIndex(cell, out var index))
                    {
                        continue;
                    }

                    var delta = tilemapWorld.GetCellCenterWorld(cell) - originWorld;
                    if (delta.sqrMagnitude > radiusSquared || !HasLineOfSight(origin, cell))
                    {
                        continue;
                    }

                    results.Add(index);
                }
            }
        }

        private bool HasLineOfSight(Vector3Int origin, Vector3Int target)
        {
            var x = origin.x;
            var y = origin.y;
            var deltaX = Mathf.Abs(target.x - origin.x);
            var stepX = origin.x < target.x ? 1 : -1;
            var deltaY = -Mathf.Abs(target.y - origin.y);
            var stepY = origin.y < target.y ? 1 : -1;
            var error = deltaX + deltaY;

            while (x != target.x || y != target.y)
            {
                var doubleError = error * 2;
                if (doubleError >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubleError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }

                var cell = new Vector3Int(x, y, 0);
                if (cell == target)
                {
                    return true;
                }

                if (tilemapWorld.TrySample(cell, out var sample) && sample.BlocksVision)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetCellIndex(Vector3Int cell, out int index)
        {
            if (cell.x < visibilityBounds.xMin
                || cell.y < visibilityBounds.yMin
                || cell.x >= visibilityBounds.xMax
                || cell.y >= visibilityBounds.yMax)
            {
                index = -1;
                return false;
            }

            index = (cell.y - visibilityBounds.yMin) * visibilityBounds.size.x
                + cell.x - visibilityBounds.xMin;
            return true;
        }

        private bool IsEligibleProvider(IFogVisionProvider provider)
        {
            return provider != null
                && provider.Team == playerTeam
                && provider.IsVisionActive
                && provider.VisionTransform != null;
        }

        private void ResolveWorld()
        {
            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance ?? FindFirstObjectByType<ProjectSTilemapWorld>();
            }
        }

        private void EnsureOverlay()
        {
            var overlay = GetComponent<FogOfWarOverlay>();
            if (overlay == null)
            {
                overlay = gameObject.AddComponent<FogOfWarOverlay>();
            }

            overlay.Attach(this);
        }
    }
}
