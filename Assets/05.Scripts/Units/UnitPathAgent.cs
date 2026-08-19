using System.Collections.Generic;
using ProjectS.Tilemaps;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitPathAgent : MonoBehaviour
    {
        [SerializeField] private ProjectSTilemapNavigator navigator;
        [SerializeField] private float stoppingDistance = 0.08f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float personalSpaceRadius = 0.72f;
        [SerializeField] private float separationStrength = 2.6f;
        [SerializeField] private LayerMask unitSeparationMask = ~0;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly Collider2D[] separationBuffer = new Collider2D[16];
        private ContactFilter2D separationFilter;
        private PrototypeUnitStatus status;
        private int waypointIndex;

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

            if (GetComponent<Collider2D>() == null)
            {
                var collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
            }
            separationFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = unitSeparationMask,
                useTriggers = false
            };

            ResolveNavigator();
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

            ResolveNavigator();
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
            var current = transform.position;
            var flatDelta = new Vector3(target.x - current.x, target.y - current.y, 0f);
            if (flatDelta.magnitude <= stoppingDistance)
            {
                transform.position = target;
                waypointIndex++;
                if (!HasPath)
                {
                    ClearPath();
                }

                return;
            }

            var direction = flatDelta.normalized;
            var speed = status != null ? status.MovementSpeed : 3f;
            var flatTarget = new Vector3(target.x, target.y, current.z);
            var nextPosition = Vector3.MoveTowards(current, flatTarget, speed * Time.deltaTime);
            transform.position = nextPosition;

            if (direction.sqrMagnitude > 0.001f)
            {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                var targetRotation = Quaternion.Euler(0f, 0f, angle);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
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
    }
}
