using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class UnitCommandAgent : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float targetScanInterval = 0.2f;
        [SerializeField] private float targetRepathDistance = 0.25f;
        [SerializeField] private float targetRepathInterval = 0.25f;
        [SerializeField] private float attackRangeStopBuffer = 0.08f;

        private readonly Collider2D[] targetBuffer = new Collider2D[32];
        private ContactFilter2D targetFilter;
        private PrototypeUnitStatus status;
        private UnitPathAgent pathAgent;
        private Collider2D attackCollider;
        private UnitCommandMode mode = UnitCommandMode.Idle;
        private UnitActionState actionState = UnitActionState.Idle;
        private Vector3 commandDestination;
        private Vector3 patrolStart;
        private Vector3 patrolEnd;
        private PrototypeUnitStatus priorityTarget;
        private Vector3 lastFocusPathTarget;
        private bool hasFocusPathTarget;
        private bool targetMustStayDetected;
        private float nextScanTime;
        private float nextTargetRepathTime;

        public UnitCommandMode Mode => mode;
        public UnitActionState ActionState => actionState;
        public PrototypeUnitStatus PriorityTarget => priorityTarget;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            pathAgent = GetComponent<UnitPathAgent>();
            attackCollider = GetComponent<Collider2D>();
            targetFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetMask,
                useTriggers = true
            };
        }

        private void Update()
        {
            if (CanAcquireTargetsForCurrentState())
            {
                ScanForTargets();
            }

            if (priorityTarget != null)
            {
                UpdateTargetEngagement();
            }

            if (actionState == UnitActionState.Patrolling && !pathAgent.HasPath && priorityTarget == null)
            {
                SwapPatrolEndpoint();
            }

            if ((actionState == UnitActionState.Moving || actionState == UnitActionState.AttackMoving)
                && !pathAgent.HasPath
                && priorityTarget == null)
            {
                CompleteCurrentCommand();
            }
        }

        public void Issue(UnitCommand command)
        {
            Execute(command);
        }

        public void Stop()
        {
            ClearTarget();
            mode = UnitCommandMode.Idle;
            actionState = UnitActionState.Idle;
            pathAgent.ClearPath();
        }

        public void HoldPosition()
        {
            ClearTarget();
            mode = UnitCommandMode.HoldPosition;
            actionState = UnitActionState.HoldingPosition;
            pathAgent.ClearPath();
        }

        private void Execute(UnitCommand command)
        {
            pathAgent.ClearPath();
            ClearTarget();
            commandDestination = command.Destination;
            mode = command.Mode;

            switch (command.Mode)
            {
                case UnitCommandMode.Move:
                    actionState = UnitActionState.Moving;
                    pathAgent.MoveTo(command.Destination);
                    break;
                case UnitCommandMode.AttackMove:
                    actionState = UnitActionState.AttackMoving;
                    pathAgent.MoveTo(command.Destination);
                    break;
                case UnitCommandMode.FocusAttack:
                    priorityTarget = command.Target;
                    targetMustStayDetected = false;
                    UpdateTargetEngagement();
                    break;
                case UnitCommandMode.HoldPosition:
                    actionState = UnitActionState.HoldingPosition;
                    break;
                case UnitCommandMode.Patrol:
                    patrolStart = transform.position;
                    patrolEnd = command.Destination;
                    actionState = UnitActionState.Patrolling;
                    pathAgent.MoveTo(patrolEnd);
                    break;
                default:
                    actionState = UnitActionState.Idle;
                    break;
            }
        }

        private void UpdateTargetEngagement()
        {
            if (!IsAttackableTarget(priorityTarget)
                || (targetMustStayDetected && !IsInDetectionRange(priorityTarget)))
            {
                ClearTarget();
                ResumeInterruptedCommand();
                return;
            }

            if (actionState == UnitActionState.HoldingPosition && !IsInAttackRange(priorityTarget))
            {
                ClearTarget();
                return;
            }

            if (IsInAttackRange(priorityTarget))
            {
                actionState = UnitActionState.AttackingTarget;
                hasFocusPathTarget = false;
                pathAgent.ClearPath();
                return;
            }

            actionState = UnitActionState.ChasingTarget;
            MoveTowardTarget(priorityTarget);
        }

        private void MoveTowardTarget(PrototypeUnitStatus target)
        {
            var targetPosition = target.transform.position;
            if (Time.time < nextTargetRepathTime
                && (!pathAgent.HasPath || !hasFocusPathTarget || Vector3.Distance(lastFocusPathTarget, targetPosition) >= targetRepathDistance))
            {
                return;
            }

            if (pathAgent.HasPath
                && hasFocusPathTarget
                && Vector3.Distance(lastFocusPathTarget, targetPosition) < targetRepathDistance)
            {
                return;
            }

            pathAgent.MoveTo(targetPosition);
            lastFocusPathTarget = targetPosition;
            hasFocusPathTarget = true;
            nextTargetRepathTime = Time.time + targetRepathInterval;
        }

        private void ScanForTargets()
        {
            if (Time.time < nextScanTime || !CanAttack())
            {
                return;
            }

            nextScanTime = Time.time + targetScanInterval;
            var scanRange = actionState == UnitActionState.HoldingPosition
                ? status.AttackRange
                : status.DetectionRange;

            if (!TryAcquireTarget(scanRange, out var target))
            {
                return;
            }

            priorityTarget = target;
            targetMustStayDetected = actionState != UnitActionState.HoldingPosition;
            UpdateTargetEngagement();
        }

        private bool TryAcquireTarget(float scanRange, out PrototypeUnitStatus target)
        {
            target = null;
            targetFilter.layerMask = targetMask;
            var hitCount = Physics2D.OverlapCircle(transform.position, scanRange, targetFilter, targetBuffer);
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < hitCount; i++)
            {
                var candidate = targetBuffer[i].GetComponentInParent<PrototypeUnitStatus>();
                if (!IsAttackableTarget(candidate) || !IsInDetectionRange(candidate))
                {
                    continue;
                }

                var distance = GetTargetDistance(candidate);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = candidate;
            }

            return target != null;
        }

        private bool CanAcquireTargetsForCurrentState()
        {
            return priorityTarget == null
                && (actionState == UnitActionState.AttackMoving
                    || actionState == UnitActionState.Patrolling
                    || actionState == UnitActionState.HoldingPosition);
        }

        private void ResumeInterruptedCommand()
        {
            switch (mode)
            {
                case UnitCommandMode.AttackMove:
                    actionState = UnitActionState.AttackMoving;
                    pathAgent.MoveTo(commandDestination);
                    break;
                case UnitCommandMode.Patrol:
                    actionState = UnitActionState.Patrolling;
                    if (!pathAgent.HasPath)
                    {
                        pathAgent.MoveTo(patrolEnd);
                    }

                    break;
                case UnitCommandMode.HoldPosition:
                    actionState = UnitActionState.HoldingPosition;
                    pathAgent.ClearPath();
                    break;
                default:
                    CompleteCurrentCommand();
                    break;
            }
        }

        private bool CanAttack()
        {
            return status.Roles.HasFlag(UnitRole.Combat) || status.PhysicalAttackPower > 0f || status.MagicalAttackPower > 0f;
        }

        private bool IsEnemy(PrototypeUnitStatus other)
        {
            return other != status && other.Team != status.Team;
        }

        private bool IsAttackableTarget(PrototypeUnitStatus target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || !IsEnemy(target))
            {
                return false;
            }

            var health = target.GetComponent<UnitHealth>();
            return health == null || !health.IsDead;
        }

        private bool IsInAttackRange(PrototypeUnitStatus target)
        {
            return GetTargetDistance(target) <= Mathf.Max(0.05f, status.AttackRange - attackRangeStopBuffer);
        }

        private bool IsInDetectionRange(PrototypeUnitStatus target)
        {
            return GetTargetDistance(target) <= status.DetectionRange;
        }

        private float GetTargetDistance(PrototypeUnitStatus target)
        {
            if (target == null)
            {
                return float.PositiveInfinity;
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            var targetCollider = target.GetComponent<Collider2D>();
            if (attackCollider != null && targetCollider != null)
            {
                var colliderDistance = attackCollider.Distance(targetCollider);
                if (colliderDistance.isValid)
                {
                    return Mathf.Max(0f, colliderDistance.distance);
                }
            }

            return Vector3.Distance(transform.position, target.transform.position);
        }

        private void SwapPatrolEndpoint()
        {
            var currentTarget = Vector3.Distance(transform.position, patrolEnd) < Vector3.Distance(transform.position, patrolStart)
                ? patrolStart
                : patrolEnd;
            pathAgent.MoveTo(currentTarget);
        }

        private void CompleteCurrentCommand()
        {
            mode = UnitCommandMode.Idle;
            actionState = UnitActionState.Idle;
            ClearTarget();
        }

        private void ClearTarget()
        {
            priorityTarget = null;
            hasFocusPathTarget = false;
            targetMustStayDetected = false;
            nextTargetRepathTime = 0f;
        }
    }
}
