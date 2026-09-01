using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitPathAgent : MonoBehaviour
    {
        private static readonly Vector2 UnitColliderOffset = new Vector2(0f, 0.1f);
        private static readonly Vector2 UnitColliderSize = new Vector2(0.77f, 1f);
        private static readonly Dictionary<UnitTeam, Dictionary<Vector3Int, int>> OccupiedCellCountsByTeam =
            new Dictionary<UnitTeam, Dictionary<Vector3Int, int>>();

        private static ProjectSTilemapWorld occupancyWorld;

        [SerializeField] private ProjectSTilemapNavigator navigator;
        [SerializeField] private float stoppingDistance = 0.08f;
        [SerializeField] private float startWaypointSkipDistance = 0.35f;
        [SerializeField] private int occupiedDestinationSearchRadius = 4;
        [SerializeField] private int maxDestinationPathCandidates = 16;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly List<Vector3> candidatePath = new List<Vector3>();
        private readonly List<Vector3> scheduledDestinations = new List<Vector3>();
        private PrototypeUnitStatus status;
        private ProjectSTilemapWorld occupiedWorld;
        private Vector3Int occupiedCell;
        private readonly List<Vector3Int> occupiedCells = new List<Vector3Int>();
        private int waypointIndex;
        private Vector3 lastMoveDirection;
        private Vector3 requestedDestination;
        private bool hasOccupiedCell;
        private bool hasRequestedDestination;
        private bool hasPendingPathRequest;
        private bool forceOccupiedCell;
        private int nextPathRequestId;
        private int activePathRequestId;

        public bool HasPath => hasPendingPathRequest || HasActivePath;
        public IReadOnlyList<Vector3> CurrentPath => path;

        private bool HasActivePath => waypointIndex < path.Count;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            var unitRigidbody = GetComponent<Rigidbody2D>();
            if (unitRigidbody == null)
            {
                unitRigidbody = gameObject.AddComponent<Rigidbody2D>();
            }

            unitRigidbody.bodyType = RigidbodyType2D.Kinematic;
            unitRigidbody.gravityScale = 0f;

            ConfigureUnitCollider();

            ResolveNavigator(true);
        }

        private void OnEnable()
        {
            ResolveNavigator(true);
            UpdateOccupiedCell();
        }

        private void OnDisable()
        {
            UnregisterOccupiedCell();
        }

        private void Update()
        {
            if (!HasActivePath)
            {
                lastMoveDirection = Vector3.zero;
            }
            else
            {
                MoveAlongPath();
            }

            UpdateOccupiedCell();
        }

        public bool MoveTo(Vector3 destination)
        {
            if (status == null)
            {
                status = GetComponent<PrototypeUnitStatus>();
            }

            if (status.MovementDomain != MovementDomain.Ground || status.PlacementType != PlacementType.Movable)
            {
                ClearPath();
                return false;
            }

            ResolveNavigator();
            requestedDestination = destination;
            hasRequestedDestination = true;
            if (navigator != null)
            {
                SchedulePathRequest(destination);
                return true;
            }

            path.Clear();
            path.Add(destination);
            waypointIndex = 0;
            NormalizePathStart();
            return true;
        }

        public void ApplyScheduledPathResult(int requestId, bool success, IReadOnlyList<Vector3> result)
        {
            if (!IsPathRequestCurrent(requestId))
            {
                return;
            }

            hasPendingPathRequest = false;
            if (!success || result == null || result.Count == 0)
            {
                ClearPath();
                return;
            }

            path.Clear();
            path.AddRange(result);
            waypointIndex = 0;
            NormalizePathStart();
        }

        public bool IsPathRequestCurrent(int requestId)
        {
            return hasPendingPathRequest && requestId == activePathRequestId;
        }

        public void SetForceOccupiedCell(bool forceOccupied)
        {
            forceOccupiedCell = forceOccupied;
            UpdateOccupiedCell();
        }

        public void ClearPath()
        {
            path.Clear();
            waypointIndex = 0;
            lastMoveDirection = Vector3.zero;
            hasRequestedDestination = false;
            hasPendingPathRequest = false;
        }

        private void MoveAlongPath()
        {
            var target = path[waypointIndex];
            var current = transform.position;
            var flatDelta = new Vector3(target.x - current.x, target.y - current.y, 0f);
            if (flatDelta.magnitude <= stoppingDistance)
            {
                if (navigator != null && !navigator.IsSegmentWalkable(current, target))
                {
                    if (!TryRepathToRequestedDestination())
                    {
                        ClearPath();
                    }

                    return;
                }

                waypointIndex++;
                if (!HasPath)
                {
                    ClearPath();
                }
                else
                {
                    NormalizePathStart();
                }

                return;
            }

            var direction = flatDelta.normalized;
            var flatTarget = new Vector3(target.x, target.y, current.z);
            var nextPosition = Vector3.MoveTowards(current, flatTarget, GetMovementSpeed() * Time.deltaTime);
            if (navigator != null && !navigator.IsSegmentWalkable(current, nextPosition))
            {
                if (!TryRepathToRequestedDestination())
                {
                    ClearPath();
                }

                return;
            }

            transform.position = nextPosition;
            lastMoveDirection = direction;
        }

        private bool TryBuildPath(Vector3 destination)
        {
            if (navigator != null)
            {
                UpdateOccupiedCell();
                return TryBuildShortestPathToAvailableDestination(destination);
            }

            path.Clear();
            path.Add(destination);
            return true;
        }

        private void SchedulePathRequest(Vector3 destination)
        {
            UpdateOccupiedCell();
            path.Clear();
            waypointIndex = 0;
            lastMoveDirection = Vector3.zero;
            activePathRequestId = ++nextPathRequestId;
            hasPendingPathRequest = true;
            BuildScheduledDestinations(destination);

            UnitPathRequestScheduler.Instance.Enqueue(
                this,
                activePathRequestId,
                navigator,
                transform.position,
                scheduledDestinations,
                GetCurrentTeamOccupiedCells());
        }

        private void BuildScheduledDestinations(Vector3 destination)
        {
            scheduledDestinations.Clear();
            if (navigator == null || navigator.TilemapWorld == null)
            {
                scheduledDestinations.Add(destination);
                return;
            }

            var tilemapWorld = navigator.TilemapWorld;
            var destinationCell = tilemapWorld.WorldToCell(destination);
            var checkedCandidates = 0;

            foreach (var candidate in EnumerateDestinationCandidates(destinationCell))
            {
                checkedCandidates++;
                if (checkedCandidates > maxDestinationPathCandidates)
                {
                    break;
                }

                if (!IsCellAvailable(tilemapWorld, candidate))
                {
                    continue;
                }

                scheduledDestinations.Add(tilemapWorld.GetCellCenterWorld(candidate));
            }

            if (scheduledDestinations.Count == 0)
            {
                scheduledDestinations.Add(destination);
            }
        }

        private bool TryBuildShortestPathToAvailableDestination(Vector3 destination)
        {
            if (navigator == null || navigator.TilemapWorld == null)
            {
                return false;
            }

            var tilemapWorld = navigator.TilemapWorld;
            var destinationCell = tilemapWorld.WorldToCell(destination);
            var foundPath = false;
            var bestPathLength = float.PositiveInfinity;
            var bestDestinationDistance = float.PositiveInfinity;
            var checkedCandidates = 0;

            foreach (var candidate in EnumerateDestinationCandidates(destinationCell))
            {
                checkedCandidates++;
                if (checkedCandidates > maxDestinationPathCandidates)
                {
                    break;
                }

                if (!IsCellAvailable(tilemapWorld, candidate))
                {
                    continue;
                }

                var candidateWorld = tilemapWorld.GetCellCenterWorld(candidate);
                if (!navigator.TryFindPath(transform.position, candidateWorld, candidatePath, GetCurrentTeamOccupiedCells()))
                {
                    continue;
                }

                if (candidate == destinationCell)
                {
                    path.Clear();
                    path.AddRange(candidatePath);
                    return true;
                }

                var pathLength = GetPathLength(candidatePath);
                var destinationDistance = Vector3.SqrMagnitude(candidateWorld - destination);
                if (pathLength > bestPathLength
                    || (Mathf.Approximately(pathLength, bestPathLength) && destinationDistance >= bestDestinationDistance))
                {
                    continue;
                }

                bestPathLength = pathLength;
                bestDestinationDistance = destinationDistance;
                path.Clear();
                path.AddRange(candidatePath);
                foundPath = true;
            }

            return foundPath;
        }

        private IEnumerable<Vector3Int> EnumerateDestinationCandidates(Vector3Int destinationCell)
        {
            yield return destinationCell;

            for (var radius = 1; radius <= occupiedDestinationSearchRadius; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        var candidate = destinationCell + new Vector3Int(x, y, 0);
                        yield return candidate;
                    }
                }
            }
        }

        private bool IsCellAvailable(ProjectSTilemapWorld tilemapWorld, Vector3Int cell)
        {
            foreach (var footprintCell in EnumerateFootprintCells(cell))
            {
                if (!tilemapWorld.IsWalkable(footprintCell) || IsCellOccupied(tilemapWorld, footprintCell))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCellOccupied(ProjectSTilemapWorld tilemapWorld, Vector3Int cell)
        {
            return !(hasOccupiedCell && occupiedCell == cell)
                && occupancyWorld == tilemapWorld
                && GetOccupiedCellCounts(status.Team).ContainsKey(cell);
        }

        private void UpdateOccupiedCell()
        {
            if (navigator == null && ProjectSTilemapNavigator.ActiveInstance != null)
            {
                navigator = ProjectSTilemapNavigator.ActiveInstance;
            }

            if (navigator == null
                || navigator.TilemapWorld == null
                || !CanBlockGroundMovement()
                || (HasPath && !forceOccupiedCell))
            {
                UnregisterOccupiedCell();
                return;
            }

            var tilemapWorld = navigator.TilemapWorld;
            EnsureOccupancyWorld(tilemapWorld);
            if (hasOccupiedCell && occupiedWorld != tilemapWorld)
            {
                hasOccupiedCell = false;
                occupiedWorld = null;
            }

            var nextCell = tilemapWorld.WorldToCell(transform.position);
            if (hasOccupiedCell && occupiedCell == nextCell && IsCurrentFootprintRegistered(nextCell))
            {
                return;
            }

            UnregisterOccupiedCell();
            RegisterOccupiedCell(tilemapWorld, nextCell);
        }

        private void RegisterOccupiedCell(ProjectSTilemapWorld tilemapWorld, Vector3Int cell)
        {
            EnsureOccupancyWorld(tilemapWorld);
            var occupiedCellCounts = GetOccupiedCellCounts(status.Team);
            occupiedCells.Clear();
            foreach (var footprintCell in EnumerateFootprintCells(cell))
            {
                occupiedCellCounts.TryGetValue(footprintCell, out var count);
                occupiedCellCounts[footprintCell] = count + 1;
                occupiedCells.Add(footprintCell);
            }

            occupiedWorld = tilemapWorld;
            occupiedCell = cell;
            hasOccupiedCell = true;
        }

        private void UnregisterOccupiedCell()
        {
            if (!hasOccupiedCell || occupiedWorld == null)
            {
                hasOccupiedCell = false;
                occupiedWorld = null;
                return;
            }

            var occupiedCellCounts = GetOccupiedCellCounts(status.Team);
            if (occupiedWorld == occupancyWorld)
            {
                for (var i = 0; i < occupiedCells.Count; i++)
                {
                    var cell = occupiedCells[i];
                    if (!occupiedCellCounts.TryGetValue(cell, out var count))
                    {
                        continue;
                    }

                    if (count <= 1)
                    {
                        occupiedCellCounts.Remove(cell);
                    }
                    else
                    {
                        occupiedCellCounts[cell] = count - 1;
                    }
                }
            }

            occupiedCells.Clear();
            hasOccupiedCell = false;
            occupiedWorld = null;
        }

        private static void EnsureOccupancyWorld(ProjectSTilemapWorld tilemapWorld)
        {
            if (occupancyWorld == tilemapWorld)
            {
                return;
            }

            OccupiedCellCountsByTeam.Clear();
            occupancyWorld = tilemapWorld;
        }

        private ICollection<Vector3Int> GetCurrentTeamOccupiedCells()
        {
            return status != null ? GetOccupiedCellCounts(status.Team).Keys : null;
        }

        private static Dictionary<Vector3Int, int> GetOccupiedCellCounts(UnitTeam team)
        {
            if (!OccupiedCellCountsByTeam.TryGetValue(team, out var occupiedCellCounts))
            {
                occupiedCellCounts = new Dictionary<Vector3Int, int>();
                OccupiedCellCountsByTeam[team] = occupiedCellCounts;
            }

            return occupiedCellCounts;
        }

        private static float GetPathLength(IReadOnlyList<Vector3> points)
        {
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        private bool CanBlockGroundMovement()
        {
            return status != null
                && status.MovementDomain == MovementDomain.Ground
                && status.PlacementType == PlacementType.Movable;
        }

        private void NormalizePathStart()
        {
            var current = transform.position;
            while (HasPath)
            {
                var target = path[waypointIndex];
                var toTarget = new Vector3(target.x - current.x, target.y - current.y, 0f);
                var isNearCurrent = toTarget.magnitude <= Mathf.Max(stoppingDistance, startWaypointSkipDistance);
                var isBehindNextWaypoint = false;
                if (waypointIndex + 1 < path.Count)
                {
                    var next = path[waypointIndex + 1];
                    var currentToNext = new Vector3(next.x - current.x, next.y - current.y, 0f);
                    var targetToNext = new Vector3(next.x - target.x, next.y - target.y, 0f);
                    isBehindNextWaypoint = Vector3.Dot(toTarget, targetToNext) <= 0f
                        && Vector3.Dot(currentToNext, targetToNext) > 0f;
                }

                if (!isNearCurrent && !isBehindNextWaypoint)
                {
                    break;
                }

                waypointIndex++;
            }

            if (!HasPath)
            {
                ClearPath();
            }
        }

        private bool TryRepathToRequestedDestination()
        {
            if (!hasRequestedDestination || navigator == null)
            {
                return false;
            }

            var destination = requestedDestination;
            if (navigator != null)
            {
                SchedulePathRequest(destination);
                return true;
            }

            if (!TryBuildPath(destination))
            {
                return false;
            }

            requestedDestination = destination;
            hasRequestedDestination = true;
            waypointIndex = 0;
            NormalizePathStart();
            return HasPath;
        }

        private float GetMovementSpeed()
        {
            return status != null ? status.MovementSpeed : 3f;
        }

        private bool IsCurrentFootprintRegistered(Vector3Int centerCell)
        {
            var index = 0;
            foreach (var footprintCell in EnumerateFootprintCells(centerCell))
            {
                if (index >= occupiedCells.Count || occupiedCells[index] != footprintCell)
                {
                    return false;
                }

                index++;
            }

            return index == occupiedCells.Count;
        }

        private IEnumerable<Vector3Int> EnumerateFootprintCells(Vector3Int centerCell)
        {
            var footprint = status != null ? status.OccupiedCells : Vector2Int.one;
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            var startX = centerCell.x - (footprint.x - 1) / 2;
            var startY = centerCell.y - (footprint.y - 1) / 2;

            for (var y = 0; y < footprint.y; y++)
            {
                for (var x = 0; x < footprint.x; x++)
                {
                    yield return new Vector3Int(startX + x, startY + y, centerCell.z);
                }
            }
        }

        private void ResolveNavigator(bool allowSceneSearch = false)
        {
            if (navigator == null)
            {
                navigator = ProjectSTilemapNavigator.ActiveInstance;
            }

            if (navigator == null && allowSceneSearch)
            {
                navigator = FindFirstObjectByType<ProjectSTilemapNavigator>();
            }
        }

        private void ConfigureUnitCollider()
        {
            var boxCollider = GetComponent<BoxCollider2D>();
            foreach (var collider in GetComponents<Collider2D>())
            {
                if (collider is BoxCollider2D candidate)
                {
                    boxCollider = candidate;
                    continue;
                }

                Destroy(collider);
            }

            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            boxCollider.isTrigger = true;
            boxCollider.offset = UnitColliderOffset;
            boxCollider.size = UnitColliderSize;
        }
    }
}
