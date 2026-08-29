using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class UnitCommandAgent : MonoBehaviour
    {
        [SerializeField] private float targetScanInterval = 0.2f;
        [SerializeField] private float targetScanJitter = 0.05f;
        [SerializeField] private float targetRepathDistance = 0.25f;
        [SerializeField] private float targetRepathInterval = 0.25f;
        [SerializeField] private float targetRepathJitter = 0.08f;
        [SerializeField] private float attackRangeStopBuffer = 0.08f;
        [SerializeField] private bool showActionStateGizmos = true;
        [SerializeField] private Color detectionRangeGizmoColor = new Color(1f, 0.85f, 0.1f, 0.28f);
        [SerializeField] private Color attackRangeGizmoColor = new Color(1f, 0.2f, 0.1f, 0.35f);
        [SerializeField] private Color targetLineGizmoColor = new Color(1f, 0.1f, 0.1f, 0.8f);

        private PrototypeUnitStatus status;
        private UnitPathAgent pathAgent;
        private Collider2D attackCollider;
        private IUnitInteractionHandler[] interactionHandlers;
        private bool isRegistered;
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
        public PrototypeUnitStatus Status => status;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterAgent();
            ScheduleNextScan(true);
        }

        private void OnDisable()
        {
            UnregisterAgent();
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

            SyncPathOccupationOverride();
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
                case UnitCommandMode.Interact:
                    if (TryStartInteraction(command.InteractableTarget))
                    {
                        actionState = UnitActionState.Interacting;
                        break;
                    }

                    CompleteCurrentCommand();
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

            if (pathAgent.HasPath
                && hasFocusPathTarget
                && Vector3.Distance(lastFocusPathTarget, targetPosition) < targetRepathDistance)
            {
                return;
            }

            if (Time.time < nextTargetRepathTime)
            {
                return;
            }

            if (pathAgent.MoveTo(targetPosition))
            {
                lastFocusPathTarget = targetPosition;
                hasFocusPathTarget = true;
                ScheduleNextTargetRepath();
            }
        }

        private void ScanForTargets()
        {
            if (Time.time < nextScanTime || !CanAttack())
            {
                return;
            }

            ScheduleNextScan(false);
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
            var bestDistance = float.PositiveInfinity;
            var teams = UnitRegistry.AllTeams;

            for (var teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                var team = teams[teamIndex];
                if (status != null && team == status.Team)
                {
                    continue;
                }

                var agents = UnitRegistry.GetAgents(team);
                for (var i = 0; i < agents.Count; i++)
                {
                    var candidateAgent = agents[i];
                    if (candidateAgent == null)
                    {
                        continue;
                    }

                    var candidate = candidateAgent.Status;
                    if (!IsAttackableTarget(candidate))
                    {
                        continue;
                    }

                    var distance = GetTargetDistance(candidate);
                    if (distance > scanRange || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    target = candidate;
                }
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
            SyncPathOccupationOverride();
        }

        private void ResolveReferences()
        {
            if (status == null)
            {
                status = GetComponent<PrototypeUnitStatus>();
            }

            if (pathAgent == null)
            {
                pathAgent = GetComponent<UnitPathAgent>();
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            if (interactionHandlers == null || interactionHandlers.Length == 0)
            {
                interactionHandlers = GetComponents<IUnitInteractionHandler>();
            }
        }

        private void RegisterAgent()
        {
            if (isRegistered)
            {
                return;
            }

            UnitRegistry.Register(this, status);
            isRegistered = true;
        }

        private void UnregisterAgent()
        {
            if (!isRegistered)
            {
                return;
            }

            UnitRegistry.Unregister(this, status);
            isRegistered = false;
        }

        private void ScheduleNextScan(bool initial)
        {
            var interval = Mathf.Max(0.02f, targetScanInterval);
            var jitter = Mathf.Max(0f, targetScanJitter);
            var randomOffset = jitter > 0f ? Random.Range(0f, jitter) : 0f;
            nextScanTime = Time.time + (initial ? randomOffset : interval + randomOffset);
        }

        private bool TryStartInteraction(IUnitInteractableTarget target)
        {
            if (target == null || !target.CanInteract(this))
            {
                return false;
            }

            ResolveReferences();
            if (interactionHandlers == null || interactionHandlers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < interactionHandlers.Length; i++)
            {
                if (interactionHandlers[i] != null && interactionHandlers[i].TryHandleInteractionCommand(target))
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleNextTargetRepath()
        {
            var interval = Mathf.Max(0.02f, targetRepathInterval);
            var jitter = Mathf.Max(0f, targetRepathJitter);
            nextTargetRepathTime = Time.time + interval + (jitter > 0f ? Random.Range(0f, jitter) : 0f);
        }

        private void SyncPathOccupationOverride()
        {
            if (pathAgent != null)
            {
                pathAgent.SetForceOccupiedCell(actionState == UnitActionState.AttackingTarget);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showActionStateGizmos)
            {
                return;
            }

            ResolveReferences();
            if (status == null)
            {
                return;
            }

            Gizmos.color = detectionRangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position, status.DetectionRange);
            Gizmos.color = attackRangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position, status.AttackRange);

            if (priorityTarget != null)
            {
                Gizmos.color = targetLineGizmoColor;
                Gizmos.DrawLine(transform.position, priorityTarget.transform.position);
            }

#if UNITY_EDITOR
            Handles.Label(transform.position + Vector3.up * 0.8f, actionState.ToString());
#endif
        }
    }
}
