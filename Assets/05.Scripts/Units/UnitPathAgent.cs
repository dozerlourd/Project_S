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

        [SerializeField] private ProjectSTilemapNavigator navigator;
        [SerializeField] private float stoppingDistance = 0.08f;
        [SerializeField] private float startWaypointSkipDistance = 0.35f;
        [SerializeField] private float personalSpaceRadius = 0.72f;
        [SerializeField] private float separationStrength = 2.6f;
        [SerializeField] private LayerMask unitSeparationMask = ~0;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly Collider2D[] separationBuffer = new Collider2D[16];
        private ContactFilter2D separationFilter;
        private PrototypeUnitStatus status;
        private int waypointIndex;
        private Vector3 lastMoveDirection;
        private Vector3 requestedDestination;
        private bool hasRequestedDestination;

        public bool HasPath => waypointIndex < path.Count;
        public IReadOnlyList<Vector3> CurrentPath => path;

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

            separationFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = unitSeparationMask,
                useTriggers = true
            };

            ResolveNavigator();
        }

        private void Update()
        {
            if (!HasPath)
            {
                lastMoveDirection = Vector3.zero;
                return;
            }

            MoveAlongPath();
            ApplyUnitSeparation(Time.deltaTime);
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
            if (navigator != null && !navigator.TryFindPath(transform.position, destination, path))
            {
                ClearPath();
                return false;
            }

            if (navigator == null)
            {
                path.Clear();
                path.Add(destination);
            }

            waypointIndex = 0;
            NormalizePathStart();
            return true;
        }

        public void ClearPath()
        {
            path.Clear();
            waypointIndex = 0;
            lastMoveDirection = Vector3.zero;
            hasRequestedDestination = false;
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

                transform.position = target;
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
            var speed = status != null ? status.MovementSpeed : 3f;
            var flatTarget = new Vector3(target.x, target.y, current.z);
            var nextPosition = Vector3.MoveTowards(current, flatTarget, speed * Time.deltaTime);
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

        private void ApplyUnitSeparation(float deltaTime)
        {
            if (personalSpaceRadius <= 0f || separationStrength <= 0f)
            {
                return;
            }

            separationFilter.layerMask = unitSeparationMask;
            var hitCount = Physics2D.OverlapCircle(transform.position, personalSpaceRadius, separationFilter, separationBuffer);

            var push = Vector3.zero;
            for (var i = 0; i < hitCount; i++)
            {
                var otherAgent = separationBuffer[i].GetComponentInParent<UnitPathAgent>();
                if (otherAgent == null || otherAgent == this)
                {
                    continue;
                }

                var delta = transform.position - otherAgent.transform.position;
                delta.z = 0f;
                var distance = delta.magnitude;
                if (distance <= 0.001f || distance >= personalSpaceRadius)
                {
                    continue;
                }

                push += delta.normalized * ((personalSpaceRadius - distance) / personalSpaceRadius);
            }

            if (HasPath && lastMoveDirection.sqrMagnitude > 0.0001f)
            {
                push = Vector3.ProjectOnPlane(push, lastMoveDirection);
            }

            if (push.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var nextPosition = transform.position + push.normalized * (separationStrength * deltaTime);
            if (navigator == null || navigator.IsSegmentWalkable(transform.position, nextPosition))
            {
                transform.position = nextPosition;
            }
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
            if (!navigator.TryFindPath(transform.position, destination, path))
            {
                return false;
            }

            requestedDestination = destination;
            hasRequestedDestination = true;
            waypointIndex = 0;
            NormalizePathStart();
            return HasPath;
        }

        private void ResolveNavigator()
        {
            if (navigator == null)
            {
                navigator = ProjectSTilemapNavigator.ActiveInstance;
            }

            if (navigator == null)
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
