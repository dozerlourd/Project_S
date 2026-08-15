using System.Collections.Generic;
using ProjectS.Maps;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class UnitPathAgent : MonoBehaviour
    {
        [SerializeField] private MapPathfinder pathfinder;
        [SerializeField] private float stoppingDistance = 0.08f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float personalSpaceRadius = 0.72f;
        [SerializeField] private float separationStrength = 2.6f;
        [SerializeField] private LayerMask unitSeparationMask = ~0;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly Collider[] separationBuffer = new Collider[16];
        private PrototypeUnitStatus status;
        private int waypointIndex;

        public bool HasPath => waypointIndex < path.Count;
        public IReadOnlyList<Vector3> CurrentPath => path;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            var unitRigidbody = GetComponent<Rigidbody>();
            unitRigidbody.isKinematic = true;
            unitRigidbody.useGravity = false;

            if (pathfinder == null)
            {
                pathfinder = FindFirstObjectByType<MapPathfinder>();
            }

            SnapToGround();
        }

        private void Update()
        {
            if (!HasPath)
            {
                ApplyUnitSeparation(Time.deltaTime);
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

            if (pathfinder == null)
            {
                pathfinder = FindFirstObjectByType<MapPathfinder>();
            }

            if (pathfinder == null || !pathfinder.TryFindPath(transform.position, destination, path))
            {
                ClearPath();
                return false;
            }

            SnapToWaypointHeight(path[0]);
            waypointIndex = path.Count > 1 ? 1 : 0;
            return true;
        }

        public void ClearPath()
        {
            path.Clear();
            waypointIndex = 0;
        }

        private void MoveAlongPath()
        {
            var target = path[waypointIndex];
            var current = new Vector3(transform.position.x, target.y, transform.position.z);
            var flatDelta = new Vector3(target.x - current.x, 0f, target.z - current.z);
            if (flatDelta.magnitude <= stoppingDistance)
            {
                SnapToWaypointHeight(target);
                transform.position = new Vector3(target.x, target.y, target.z);
                waypointIndex++;
                if (!HasPath)
                {
                    ClearPath();
                }

                return;
            }

            var direction = flatDelta.normalized;
            var speed = status != null ? status.MovementSpeed : 3f;
            var nextPosition = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
            transform.position = new Vector3(nextPosition.x, target.y, nextPosition.z);

            if (direction.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void ApplyUnitSeparation(float deltaTime)
        {
            if (personalSpaceRadius <= 0f || separationStrength <= 0f)
            {
                return;
            }

            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                personalSpaceRadius,
                separationBuffer,
                unitSeparationMask,
                QueryTriggerInteraction.Ignore);

            var push = Vector3.zero;
            for (var i = 0; i < hitCount; i++)
            {
                var otherAgent = separationBuffer[i].GetComponentInParent<UnitPathAgent>();
                if (otherAgent == null || otherAgent == this)
                {
                    continue;
                }

                var delta = transform.position - otherAgent.transform.position;
                delta.y = 0f;
                var distance = delta.magnitude;
                if (distance <= 0.001f || distance >= personalSpaceRadius)
                {
                    continue;
                }

                push += delta.normalized * ((personalSpaceRadius - distance) / personalSpaceRadius);
            }

            if (push.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var nextPosition = transform.position + push.normalized * (separationStrength * deltaTime);
            if (pathfinder != null && pathfinder.IsSegmentWalkable(transform.position, nextPosition))
            {
                transform.position = new Vector3(nextPosition.x, transform.position.y, nextPosition.z);
            }
        }

        private void SnapToGround()
        {
            if (pathfinder == null)
            {
                return;
            }

            if (pathfinder.TryFindPath(transform.position, transform.position, path) && path.Count > 0)
            {
                SnapToWaypointHeight(path[0]);
                ClearPath();
            }
        }

        private void SnapToWaypointHeight(Vector3 waypoint)
        {
            transform.position = new Vector3(transform.position.x, waypoint.y, transform.position.z);
        }
    }
}
