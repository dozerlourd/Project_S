using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;

namespace ProjectS.Units
{
    public sealed class UnitPathRequestScheduler : MonoBehaviour
    {
        private const int DefaultMaxRequestsPerFrame = 3;

        [SerializeField] private int maxRequestsPerFrame = DefaultMaxRequestsPerFrame;

        private static UnitPathRequestScheduler instance;
        private readonly Queue<PathRequest> requests = new Queue<PathRequest>();

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
            var remainingAttempts = Mathf.Max(1, maxRequestsPerFrame);
            while (remainingAttempts > 0 && requests.Count > 0)
            {
                var request = requests.Dequeue();
                remainingAttempts--;
                if (!ProcessRequest(ref request))
                {
                    requests.Enqueue(request);
                }
            }
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

            requests.Enqueue(new PathRequest(
                agent,
                requestId,
                navigator,
                start,
                new List<Vector3>(destinations),
                blockedCells != null ? new HashSet<Vector3Int>(blockedCells) : null));
        }

        private static bool ProcessRequest(ref PathRequest request)
        {
            if (request.Agent == null || request.Navigator == null)
            {
                return true;
            }

            if (!request.Agent.IsPathRequestCurrent(request.RequestId))
            {
                return true;
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
                return true;
            }

            request.NextDestinationIndex++;
            if (request.NextDestinationIndex >= request.Destinations.Count)
            {
                request.Agent.ApplyScheduledPathResult(request.RequestId, false, result);
                return true;
            }

            return false;
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
