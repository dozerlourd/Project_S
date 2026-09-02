using System.Collections.Generic;
using System.Diagnostics;
using ProjectS.Tilemaps;
using UnityEngine;

namespace ProjectS.Units
{
    public sealed class UnitPathRequestScheduler : MonoBehaviour
    {
        private const int DefaultMaxRequestsPerFrame = 3;

        [SerializeField] private int maxRequestsPerFrame = DefaultMaxRequestsPerFrame;
        [SerializeField] private bool showDebugOverlay;
        [SerializeField] private bool logMetrics;
        [SerializeField] private float metricsLogInterval = 2f;

        private static UnitPathRequestScheduler instance;
        private readonly Queue<PathRequest> requests = new Queue<PathRequest>();
        private readonly List<Vector3> reusableFailedResult = new List<Vector3>();
        private int enqueuedThisFrame;
        private int processedThisFrame;
        private int completedThisFrame;
        private int failedThisFrame;
        private int discardedThisFrame;
        private int fallbackAttemptsThisFrame;
        private int pathfindAttemptsThisFrame;
        private int totalEnqueued;
        private int totalProcessed;
        private int totalCompleted;
        private int totalFailed;
        private int totalDiscarded;
        private int totalFallbackAttempts;
        private int totalPathfindAttempts;
        private int totalTerminalQueueWaitFrames;
        private int longestQueueWaitFrames;
        private int peakPendingRequests;
        private int counterFrame = -1;
        private long pathfindingTicksThisFrame;
        private long totalPathfindingTicks;
        private long maxPathfindingTicks;
        private float nextMetricsLogTime;

        public int MaxRequestsPerFrame => Mathf.Max(1, maxRequestsPerFrame);
        public int PendingRequestCount => requests.Count;
        public int EnqueuedThisFrame => enqueuedThisFrame;
        public int ProcessedThisFrame => processedThisFrame;
        public int CompletedThisFrame => completedThisFrame;
        public int FailedThisFrame => failedThisFrame;
        public int DiscardedThisFrame => discardedThisFrame;
        public int FallbackAttemptsThisFrame => fallbackAttemptsThisFrame;
        public int PathfindAttemptsThisFrame => pathfindAttemptsThisFrame;
        public int TotalEnqueued => totalEnqueued;
        public int TotalProcessed => totalProcessed;
        public int TotalCompleted => totalCompleted;
        public int TotalFailed => totalFailed;
        public int TotalDiscarded => totalDiscarded;
        public int TotalFallbackAttempts => totalFallbackAttempts;
        public int TotalPathfindAttempts => totalPathfindAttempts;
        public int LongestQueueWaitFrames => longestQueueWaitFrames;
        public int PeakPendingRequests => peakPendingRequests;
        public float PathfindingMillisecondsThisFrame => TicksToMilliseconds(pathfindingTicksThisFrame);
        public float AveragePathfindingMilliseconds =>
            totalPathfindAttempts > 0 ? TicksToMilliseconds(totalPathfindingTicks) / totalPathfindAttempts : 0f;
        public float MaxPathfindingMilliseconds => TicksToMilliseconds(maxPathfindingTicks);
        public float AverageTerminalQueueWaitFrames
        {
            get
            {
                var terminalCount = totalCompleted + totalFailed + totalDiscarded;
                return terminalCount > 0 ? totalTerminalQueueWaitFrames / (float)terminalCount : 0f;
            }
        }

        public static UnitPathRequestScheduler Instance
        {
            get
            {
                if (instance == null)
                {
                    var schedulerObject = new GameObject(nameof(UnitPathRequestScheduler));
                    instance = schedulerObject.AddComponent<UnitPathRequestScheduler>();
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            EnsureFrameCounters();
            var remainingAttempts = MaxRequestsPerFrame;
            while (remainingAttempts > 0 && requests.Count > 0)
            {
                var request = requests.Dequeue();
                remainingAttempts--;
                processedThisFrame++;
                totalProcessed++;
                var result = ProcessRequest(ref request);
                if (result == PathRequestResult.Pending)
                {
                    fallbackAttemptsThisFrame++;
                    totalFallbackAttempts++;
                    requests.Enqueue(request);
                    peakPendingRequests = Mathf.Max(peakPendingRequests, requests.Count);
                    continue;
                }

                if (result == PathRequestResult.Completed)
                {
                    completedThisFrame++;
                    totalCompleted++;
                }
                else if (result == PathRequestResult.Failed)
                {
                    failedThisFrame++;
                    totalFailed++;
                }
                else if (result == PathRequestResult.Discarded)
                {
                    discardedThisFrame++;
                    totalDiscarded++;
                }

                RecordTerminalQueueWait(request);
            }

            LogMetricsIfNeeded();
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
            {
                return;
            }

            var tilemapWorld = ProjectSTilemapNavigator.ActiveInstance != null
                ? ProjectSTilemapNavigator.ActiveInstance.TilemapWorld
                : null;
            var cacheMetrics = tilemapWorld != null
                ? $"\nTile cache: samples {tilemapWorld.CachedSampleCount}, rebuilds {tilemapWorld.CacheRebuildCount}, hit/miss {tilemapWorld.SampleCacheHits}/{tilemapWorld.SampleCacheMisses}"
                : string.Empty;
            GUI.Label(
                new Rect(12f, 12f, 520f, 138f),
                $"Path Requests\nPending: {PendingRequestCount} / Peak: {PeakPendingRequests}\n"
                    + $"Frame E/P/C/F/D: {EnqueuedThisFrame}/{ProcessedThisFrame}/{CompletedThisFrame}/{FailedThisFrame}/{DiscardedThisFrame}\n"
                    + $"Total E/P/C/F/D: {TotalEnqueued}/{TotalProcessed}/{TotalCompleted}/{TotalFailed}/{TotalDiscarded}\n"
                    + $"Path attempts: {PathfindAttemptsThisFrame} frame / {TotalPathfindAttempts} total, fallbacks {FallbackAttemptsThisFrame}/{TotalFallbackAttempts}\n"
                    + $"Path ms: {PathfindingMillisecondsThisFrame:0.###} frame / avg {AveragePathfindingMilliseconds:0.###} / max {MaxPathfindingMilliseconds:0.###}\n"
                    + $"Queue wait frames: avg {AverageTerminalQueueWaitFrames:0.#} / max {LongestQueueWaitFrames}"
                    + cacheMetrics);
        }

        public void Enqueue(
            UnitPathAgent agent,
            int requestId,
            ProjectSTilemapNavigator navigator,
            Vector3 start,
            IReadOnlyList<Vector3> destinations,
            ICollection<Vector3Int> blockedCells)
        {
            if (agent == null || navigator == null || destinations == null || destinations.Count == 0)
            {
                return;
            }

            EnsureFrameCounters();
            enqueuedThisFrame++;
            totalEnqueued++;
            requests.Enqueue(new PathRequest(
                agent,
                requestId,
                navigator,
                start,
                new List<Vector3>(destinations),
                blockedCells != null ? new HashSet<Vector3Int>(blockedCells) : null,
                Time.frameCount));
            peakPendingRequests = Mathf.Max(peakPendingRequests, requests.Count);
        }

        public void Clear()
        {
            requests.Clear();
            reusableFailedResult.Clear();
        }

        private PathRequestResult ProcessRequest(ref PathRequest request)
        {
            if (request.Agent == null || request.Navigator == null)
            {
                return PathRequestResult.Discarded;
            }

            if (!request.Agent.IsPathRequestCurrent(request.RequestId))
            {
                return PathRequestResult.Discarded;
            }

            request.Result.Clear();
            var destination = request.Destinations[request.NextDestinationIndex];
            var startTicks = Stopwatch.GetTimestamp();
            var success = request.Navigator.TryFindPath(
                request.Start,
                destination,
                request.Result,
                request.BlockedCells);
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            pathfindAttemptsThisFrame++;
            totalPathfindAttempts++;
            pathfindingTicksThisFrame += elapsedTicks;
            totalPathfindingTicks += elapsedTicks;
            if (elapsedTicks > maxPathfindingTicks)
            {
                maxPathfindingTicks = elapsedTicks;
            }

            if (success)
            {
                request.Agent.ApplyScheduledPathResult(request.RequestId, true, request.Result);
                return PathRequestResult.Completed;
            }

            request.NextDestinationIndex++;
            if (request.NextDestinationIndex >= request.Destinations.Count)
            {
                reusableFailedResult.Clear();
                request.Agent.ApplyScheduledPathResult(request.RequestId, false, reusableFailedResult);
                return PathRequestResult.Failed;
            }

            return PathRequestResult.Pending;
        }

        private void RecordTerminalQueueWait(PathRequest request)
        {
            var waitFrames = Mathf.Max(0, Time.frameCount - request.EnqueuedFrame);
            totalTerminalQueueWaitFrames += waitFrames;
            if (waitFrames > longestQueueWaitFrames)
            {
                longestQueueWaitFrames = waitFrames;
            }
        }

        private void LogMetricsIfNeeded()
        {
            if (!logMetrics || Time.unscaledTime < nextMetricsLogTime)
            {
                return;
            }

            nextMetricsLogTime = Time.unscaledTime + Mathf.Max(0.25f, metricsLogInterval);
            UnityEngine.Debug.Log(
                $"[{nameof(UnitPathRequestScheduler)}] pending={PendingRequestCount}, peak={PeakPendingRequests}, "
                    + $"frame E/P/C/F/D={EnqueuedThisFrame}/{ProcessedThisFrame}/{CompletedThisFrame}/{FailedThisFrame}/{DiscardedThisFrame}, "
                    + $"total E/P/C/F/D={TotalEnqueued}/{TotalProcessed}/{TotalCompleted}/{TotalFailed}/{TotalDiscarded}, "
                    + $"pathAttempts={TotalPathfindAttempts}, fallbacks={TotalFallbackAttempts}, pathMs frame/avg/max={PathfindingMillisecondsThisFrame:0.###}/{AveragePathfindingMilliseconds:0.###}/{MaxPathfindingMilliseconds:0.###}, "
                    + $"queueWait avg/max={AverageTerminalQueueWaitFrames:0.#}/{LongestQueueWaitFrames}");
        }

        private void EnsureFrameCounters()
        {
            if (counterFrame == Time.frameCount)
            {
                return;
            }

            counterFrame = Time.frameCount;
            ResetFrameCounters();
        }

        private void ResetFrameCounters()
        {
            enqueuedThisFrame = 0;
            processedThisFrame = 0;
            completedThisFrame = 0;
            failedThisFrame = 0;
            discardedThisFrame = 0;
            fallbackAttemptsThisFrame = 0;
            pathfindAttemptsThisFrame = 0;
            pathfindingTicksThisFrame = 0L;
        }

        private static float TicksToMilliseconds(long ticks)
        {
            return ticks * 1000f / Stopwatch.Frequency;
        }

        private enum PathRequestResult
        {
            Pending,
            Completed,
            Failed,
            Discarded
        }

        private struct PathRequest
        {
            public PathRequest(
                UnitPathAgent agent,
                int requestId,
                ProjectSTilemapNavigator navigator,
                Vector3 start,
                IReadOnlyList<Vector3> destinations,
                ICollection<Vector3Int> blockedCells,
                int enqueuedFrame)
            {
                Agent = agent;
                RequestId = requestId;
                Navigator = navigator;
                Start = start;
                Destinations = destinations;
                BlockedCells = blockedCells;
                EnqueuedFrame = enqueuedFrame;
                Result = new List<Vector3>();
                NextDestinationIndex = 0;
            }

            public UnitPathAgent Agent { get; }
            public int RequestId { get; }
            public ProjectSTilemapNavigator Navigator { get; }
            public Vector3 Start { get; }
            public IReadOnlyList<Vector3> Destinations { get; }
            public ICollection<Vector3Int> BlockedCells { get; }
            public int EnqueuedFrame { get; }
            public List<Vector3> Result { get; }
            public int NextDestinationIndex { get; set; }
        }
    }
}
