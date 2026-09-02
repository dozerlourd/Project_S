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
        private IUnitCommandInterruptHandler[] interruptHandlers;
        private bool isRegistered;
        private UnitCommandMode mode = UnitCommandMode.Idle;
        private UnitActionState actionState = UnitActionState.Idle;
        private UnitCommand latestCommand;
        private int latestCommandId;
        private Vector3 commandDestination;
        private Vector3 patrolStart;
        private Vector3 patrolEnd;
        private IUnitAttackTarget priorityTarget;
        private Vector3 lastFocusPathTarget;
        private Vector3 focusTargetOffset;
        private bool hasFocusPathTarget;
        private bool targetMustStayDetected;
        private float nextScanTime;
        private float nextTargetRepathTime;
        private string lastInteractionFailureReason;

        public UnitCommandMode Mode => mode;
        public UnitActionState ActionState => actionState;
        public UnitCommand LatestCommand => latestCommand;
        public int LatestCommandId => latestCommandId;
        public IUnitAttackTarget PriorityTarget => priorityTarget;
        public Vector3 CommandDestination => commandDestination;
        public PrototypeUnitStatus Status => status;
        public string LastInteractionFailureReason => lastInteractionFailureReason;

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
            var commandId = BeginNewCommand(command);
            Execute(command, commandId);
        }

        public void Stop()
        {
            Issue(new UnitCommand(UnitCommandMode.Idle, transform.position, null, false));
        }

        public void HoldPosition()
        {
            Issue(new UnitCommand(UnitCommandMode.HoldPosition, transform.position, null, false));
        }

        public bool TryRetaliate(IUnitAttackTarget attacker)
        {
            if ((mode != UnitCommandMode.Idle && mode != UnitCommandMode.HoldPosition)
                || !CanAttack()
                || !IsAttackableTarget(attacker))
            {
                return false;
            }

            if (mode == UnitCommandMode.HoldPosition && !IsInAttackRange(attacker))
            {
                return false;
            }

            if (mode == UnitCommandMode.Idle && !IsInDetectionRange(attacker))
            {
                return false;
            }

            priorityTarget = attacker;
            targetMustStayDetected = true;
            UpdateTargetEngagement();
            return priorityTarget != null;
        }

        private int BeginNewCommand(UnitCommand command)
        {
            ResolveReferences();
            if (latestCommandId > 0 || mode != UnitCommandMode.Idle || actionState != UnitActionState.Idle)
            {
                NotifyCommandInterrupted();
            }

            latestCommand = command;
            latestCommandId++;
            pathAgent.ClearPath();
            ClearTarget();
            lastInteractionFailureReason = string.Empty;
            commandDestination = command.Destination;
            mode = command.Mode;
            return latestCommandId;
        }

        private bool IsLatestCommand(int commandId)
        {
            return commandId == latestCommandId;
        }

        private void Execute(UnitCommand command, int commandId)
        {
            if (!IsLatestCommand(commandId))
            {
                return;
            }

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
                    focusTargetOffset = command.Target != null
                        ? command.Destination - command.Target.SelectionTransform.position
                        : Vector3.zero;
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

            SyncPathOccupationOverride();
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

            if (!CanChasePriorityTarget() && !IsInAttackRange(priorityTarget))
            {
                ClearTarget();
                ResumeInterruptedCommand();
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

        private void MoveTowardTarget(IUnitAttackTarget target)
        {
            var targetPosition = target.SelectionTransform.position + focusTargetOffset;

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
            var scanRange = status.DetectionRange;

            if (!TryAcquireTarget(scanRange, out var target))
            {
                return;
            }

            priorityTarget = target;
            targetMustStayDetected = true;
            UpdateTargetEngagement();
        }

        private bool TryAcquireTarget(float scanRange, out IUnitAttackTarget target)
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

                var candidates = UnitAttackTargetRegistry.GetTargets(team);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
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
                && (actionState == UnitActionState.Idle
                    || actionState == UnitActionState.AttackMoving
                    || actionState == UnitActionState.Patrolling
                    || actionState == UnitActionState.HoldingPosition);
        }

        private void ResumeInterruptedCommand()
        {
            switch (mode)
            {
                case UnitCommandMode.Idle:
                    actionState = UnitActionState.Idle;
                    pathAgent.ClearPath();
                    break;
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
                case UnitCommandMode.FocusAttack:
                    CompleteCurrentCommand();
                    break;
                default:
                    CompleteCurrentCommand();
                    break;
            }
        }

        private bool CanChasePriorityTarget()
        {
            return mode != UnitCommandMode.HoldPosition;
        }

        private bool CanAttack()
        {
            return status != null
                && (status.Roles.HasFlag(UnitRole.Combat)
                    || status.PhysicalAttackPower > 0f
                    || status.MagicalAttackPower > 0f);
        }

        private bool IsEnemy(IUnitAttackTarget other)
        {
            return status != null && !ReferenceEquals(other, status) && other.Team != status.Team;
        }

        private bool IsAttackableTarget(IUnitAttackTarget target)
        {
            if (IsMissingTarget(target) || !IsEnemy(target))
            {
                return false;
            }

            var targetObject = target.SelectionGameObject;
            return targetObject != null && targetObject.activeInHierarchy && target.IsAlive;
        }

        private bool IsInAttackRange(IUnitAttackTarget target)
        {
            return GetTargetDistance(target) <= Mathf.Max(0.05f, status.AttackRange - attackRangeStopBuffer);
        }

        private bool IsInDetectionRange(IUnitAttackTarget target)
        {
            return GetTargetDistance(target) <= status.DetectionRange;
        }

        private float GetTargetDistance(IUnitAttackTarget target)
        {
            if (IsMissingTarget(target) || target.SelectionTransform == null)
            {
                return float.PositiveInfinity;
            }

            if (attackCollider == null)
            {
                attackCollider = GetComponent<Collider2D>();
            }

            var targetCollider = target.AttackCollider;
            if (attackCollider != null && targetCollider != null)
            {
                var colliderDistance = attackCollider.Distance(targetCollider);
                if (colliderDistance.isValid)
                {
                    return Mathf.Max(0f, colliderDistance.distance);
                }
            }

            return Vector3.Distance(transform.position, target.SelectionTransform.position);
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
            latestCommand = new UnitCommand(UnitCommandMode.Idle, transform.position, null, false);
            latestCommandId++;
            ClearTarget();
        }

        private void ClearTarget()
        {
            priorityTarget = null;
            focusTargetOffset = Vector3.zero;
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

            if (interruptHandlers == null || interruptHandlers.Length == 0)
            {
                interruptHandlers = GetComponents<IUnitCommandInterruptHandler>();
            }
        }

        private void NotifyCommandInterrupted()
        {
            ResolveReferences();
            if (interruptHandlers == null || interruptHandlers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < interruptHandlers.Length; i++)
            {
                interruptHandlers[i]?.OnUnitCommandInterrupted();
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
            if (target == null)
            {
                lastInteractionFailureReason = "Interaction command failed because the target is missing.";
                return false;
            }

            if (!target.CanInteract(this))
            {
                lastInteractionFailureReason =
                    $"Interaction command failed because {target.GetType().Name} rejected the selected unit.";
                return false;
            }

            ResolveReferences();
            if (interactionHandlers == null || interactionHandlers.Length == 0)
            {
                lastInteractionFailureReason =
                    "Interaction command failed because the unit has no interaction handlers.";
                return false;
            }

            for (var i = 0; i < interactionHandlers.Length; i++)
            {
                if (interactionHandlers[i] != null && interactionHandlers[i].TryHandleInteractionCommand(target))
                {
                    lastInteractionFailureReason = string.Empty;
                    return true;
                }
            }

            lastInteractionFailureReason =
                $"Interaction command failed because no handler accepted {target.GetType().Name}.";
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
                Gizmos.DrawLine(transform.position, priorityTarget.SelectionTransform.position);
            }

#if UNITY_EDITOR
            Handles.Label(transform.position + Vector3.up * 0.8f, actionState.ToString());
#endif
        }

        private static bool IsMissingTarget(IUnitAttackTarget target)
        {
            if (target == null)
            {
                return true;
            }

            return target is Object unityObject && unityObject == null;
        }
    }
}
