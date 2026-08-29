using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;

namespace ProjectS.Units
{
    public sealed class UnitPathRequestScheduler : MonoBehaviour
    {
        private const int DefaultMaxRequestsPerFrame = 3;

        [SerializeField] private int maxRequestsPerFrame = DefaultMaxRequestsPerFrame;
        [SerializeField] private bool showDebugOverlay;

        private static UnitPathRequestScheduler instance;
        private readonly Queue<PathRequest> requests = new Queue<PathRequest>();
        private int enqueuedThisFrame;
        private int processedThisFrame;
        private int completedThisFrame;
        private int failedThisFrame;
        private int discardedThisFrame;
        private int totalEnqueued;
        private int totalProcessed;
        private int totalCompleted;
        private int totalFailed;
        private int totalDiscarded;
        private int peakPendingRequests;
        private int counterFrame = -1;

        public int MaxRequestsPerFrame => Mathf.Max(1, maxRequestsPerFrame);
        public int PendingRequestCount => requests.Count;
        public int EnqueuedThisFrame => enqueuedThisFrame;
        public int ProcessedThisFrame => processedThisFrame;
        public int CompletedThisFrame => completedThisFrame;
        public int FailedThisFrame => failedThisFrame;
        public int DiscardedThisFrame => discardedThisFrame;
        public int TotalEnqueued => totalEnqueued;
        public int TotalProcessed => totalProcessed;
        public int TotalCompleted => totalCompleted;
        public int TotalFailed => totalFailed;
        public int TotalDiscarded => totalDiscarded;
        public int PeakPendingRequests => peakPendingRequests;

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
                    requests.Enqueue(request);
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
            }
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
            {
                return;
            }

            GUI.Label(
                new Rect(12f, 12f, 360f, 92f),
                $"Path Requests\nPending: {PendingRequestCount} / Peak: {PeakPendingRequests}\n"
                    + $"Frame E/P/C/F/D: {EnqueuedThisFrame}/{ProcessedThisFrame}/{CompletedThisFrame}/{FailedThisFrame}/{DiscardedThisFrame}\n"
                    + $"Total E/P/C/F/D: {TotalEnqueued}/{TotalProcessed}/{TotalCompleted}/{TotalFailed}/{TotalDiscarded}");
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
                blockedCells != null ? new HashSet<Vector3Int>(blockedCells) : null));
            peakPendingRequests = Mathf.Max(peakPendingRequests, requests.Count);
        }

        public void Clear()
        {
            requests.Clear();
        }

        private static PathRequestResult ProcessRequest(ref PathRequest request)
        {
            if (request.Agent == null || request.Navigator == null)
            {
                return PathRequestResult.Discarded;
            }

            if (!request.Agent.IsPathRequestCurrent(request.RequestId))
            {
                return PathRequestResult.Discarded;
            }

            var result = new List<Vector3>();
            var destination = request.Destinations[request.NextDestinationIndex];
            var success = request.Navigator.TryFindPath(
                request.Start,
                destination,
                result,
                request.BlockedCells);

            if (success)
            {
                request.Agent.ApplyScheduledPathResult(request.RequestId, true, result);
                return PathRequestResult.Completed;
            }

            request.NextDestinationIndex++;
            if (request.NextDestinationIndex >= request.Destinations.Count)
            {
                request.Agent.ApplyScheduledPathResult(request.RequestId, false, result);
                return PathRequestResult.Failed;
            }

            return PathRequestResult.Pending;
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
                ICollection<Vector3Int> blockedCells)
            {
                Agent = agent;
                RequestId = requestId;
                Navigator = navigator;
                Start = start;
                Destinations = destinations;
                BlockedCells = blockedCells;
                NextDestinationIndex = 0;
            }

            public UnitPathAgent Agent { get; }
            public int RequestId { get; }
            public ProjectSTilemapNavigator Navigator { get; }
            public Vector3 Start { get; }
            public IReadOnlyList<Vector3> Destinations { get; }
            public ICollection<Vector3Int> BlockedCells { get; }
            public int NextDestinationIndex { get; set; }
        }
    }
}
