using System.Collections.Generic;
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
        [SerializeField, Min(1f)] private float targetRetentionRangeMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float targetSwitchDistanceAdvantage = 0.4f;
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
        private bool hasExplicitFocusTarget;
        private bool isRetreatingFromTarget;
        private float nextScanTime;
        private float nextTargetRepathTime;
        private string lastInteractionFailureReason;
        private readonly List<IUnitAttackTarget> targetCandidates = new List<IUnitAttackTarget>();

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
                    hasExplicitFocusTarget = priorityTarget != null;
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
                || (targetMustStayDetected && !IsWithinTargetRetentionRange(priorityTarget)))
            {
                ClearTarget();
                ResumeInterruptedCommand();
                return;
            }

            var targetDistance = GetTargetDistance(priorityTarget);
            if (ShouldRetreatFromTarget(targetDistance))
            {
                actionState = UnitActionState.RetreatingFromTarget;
                if (!MoveAwayFromTarget(priorityTarget, targetDistance))
                {
                    isRetreatingFromTarget = false;
                    EnterAttackState();
                }

                return;
            }

            if (!CanChasePriorityTarget() && !IsInAttackRange(targetDistance))
            {
                ClearTarget();
                ResumeInterruptedCommand();
                return;
            }

            if (IsReadyToAttackTarget(targetDistance))
            {
                EnterAttackState();
                return;
            }

            isRetreatingFromTarget = false;
            actionState = UnitActionState.ChasingTarget;
            MoveTowardTarget(priorityTarget);
        }

        private void EnterAttackState()
        {
            actionState = UnitActionState.AttackingTarget;
            hasFocusPathTarget = false;
            pathAgent.ClearPath();
        }

        private void MoveTowardTarget(IUnitAttackTarget target)
        {
            var targetPosition = target.SelectionTransform.position + focusTargetOffset;
            MoveTowardPosition(targetPosition);
        }

        private bool MoveAwayFromTarget(IUnitAttackTarget target, float targetDistance)
        {
            if (Time.time < nextTargetRepathTime || target == null || target.SelectionTransform == null)
            {
                return true;
            }

            var away = transform.position - target.SelectionTransform.position;
            away.z = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = GetDeterministicRetreatDirection(target);
            }
            else
            {
                away.Normalize();
            }

            var profile = UnitTacticalBehaviorProfiles.Get(status.UnitType);
            var safeDistance = status.AttackRange * profile.RetreatReleaseRangeRatio;
            var retreatDistance = Mathf.Max(0.5f, safeDistance - targetDistance + 0.2f);
            return MoveTowardPosition(transform.position + away * retreatDistance);
        }

        private bool MoveTowardPosition(Vector3 targetPosition)
        {

            if (pathAgent.HasPath
                && hasFocusPathTarget
                && Vector3.Distance(lastFocusPathTarget, targetPosition) < targetRepathDistance)
            {
                return true;
            }

            if (Time.time < nextTargetRepathTime)
            {
                return true;
            }

            if (pathAgent.MoveTo(targetPosition))
            {
                lastFocusPathTarget = targetPosition;
                hasFocusPathTarget = true;
                ScheduleNextTargetRepath();
                return true;
            }

            return false;
        }

        private void ScanForTargets()
        {
            if (Time.time < nextScanTime || !CanAttack())
            {
                return;
            }

            ScheduleNextScan(false);
            var scanRange = mode == UnitCommandMode.HoldPosition
                ? status.AttackRange
                : status.DetectionRange;

            if (!TryAcquireTarget(scanRange, out var target))
            {
                return;
            }

            if (priorityTarget != null)
            {
                var recentAttacker = UnitTargetPriority.GetRecentAttacker(status);
                if (!UnitTargetPriority.ShouldSwitchTarget(
                        priorityTarget,
                        GetTargetDistance(priorityTarget),
                        target,
                        GetTargetDistance(target),
                        recentAttacker,
                        targetSwitchDistanceAdvantage))
                {
                    return;
                }
            }

            priorityTarget = target;
            targetMustStayDetected = true;
            hasFocusPathTarget = false;
            UpdateTargetEngagement();
        }

        private bool TryAcquireTarget(float scanRange, out IUnitAttackTarget target)
        {
            target = null;
            var bestDistance = float.PositiveInfinity;
            var recentAttacker = UnitTargetPriority.GetRecentAttacker(status);
            if (IsAttackableTarget(priorityTarget)
                && GetTargetDistance(priorityTarget) <= scanRange * Mathf.Max(1f, targetRetentionRangeMultiplier))
            {
                target = priorityTarget;
                bestDistance = GetTargetDistance(priorityTarget);
            }

            UnitAttackTargetRegistry.QueryNearbyEnemies(status.Team, transform.position, scanRange, targetCandidates);
            for (var i = 0; i < targetCandidates.Count; i++)
            {
                var candidate = targetCandidates[i];
                if (!IsAttackableTarget(candidate))
                {
                    continue;
                }

                var distance = GetTargetDistance(candidate);
                if (distance > scanRange
                    || !UnitTargetPriority.IsPreferredTarget(
                        candidate,
                        distance,
                        target,
                        bestDistance,
                        recentAttacker))
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
            return !hasExplicitFocusTarget
                && (mode == UnitCommandMode.Idle
                    || mode == UnitCommandMode.AttackMove
                    || mode == UnitCommandMode.Patrol
                    || mode == UnitCommandMode.HoldPosition);
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
            return IsInAttackRange(GetTargetDistance(target));
        }

        private bool IsInAttackRange(float targetDistance)
        {
            return targetDistance <= Mathf.Max(0.05f, status.AttackRange - attackRangeStopBuffer);
        }

        private bool IsReadyToAttackTarget(float targetDistance)
        {
            if (!CanChasePriorityTarget())
            {
                return IsInAttackRange(targetDistance);
            }

            var profile = UnitTacticalBehaviorProfiles.Get(status.UnitType);
            var entryRange = Mathf.Max(
                0.05f,
                (status.AttackRange - attackRangeStopBuffer) * profile.AttackEntryRangeRatio);
            return targetDistance <= entryRange;
        }

        private bool ShouldRetreatFromTarget(float targetDistance)
        {
            var profile = UnitTacticalBehaviorProfiles.Get(status.UnitType);
            if (profile.EngagementStyle != UnitEngagementStyle.KeepDistance || !CanChasePriorityTarget())
            {
                isRetreatingFromTarget = false;
                return false;
            }

            var triggerDistance = status.AttackRange * profile.RetreatTriggerRangeRatio;
            var releaseDistance = status.AttackRange * profile.RetreatReleaseRangeRatio;
            if (isRetreatingFromTarget)
            {
                if (targetDistance < releaseDistance)
                {
                    return true;
                }

                isRetreatingFromTarget = false;
                return false;
            }

            isRetreatingFromTarget = targetDistance < triggerDistance;
            return isRetreatingFromTarget;
        }

        private bool IsInDetectionRange(IUnitAttackTarget target)
        {
            return GetTargetDistance(target) <= status.DetectionRange;
        }

        private bool IsWithinTargetRetentionRange(IUnitAttackTarget target)
        {
            var baseRange = mode == UnitCommandMode.HoldPosition
                ? status.AttackRange
                : status.DetectionRange;
            return GetTargetDistance(target) <= baseRange * Mathf.Max(1f, targetRetentionRangeMultiplier);
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
            hasExplicitFocusTarget = false;
            isRetreatingFromTarget = false;
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
            var profile = status != null
                ? UnitTacticalBehaviorProfiles.Get(status.UnitType)
                : UnitTacticalBehaviorProfiles.Get(PrototypeUnitType.Soldier);
            var interval = Mathf.Max(0.02f, targetRepathInterval * profile.TargetRepathIntervalMultiplier);
            var jitter = Mathf.Max(0f, targetRepathJitter);
            nextTargetRepathTime = Time.time + interval + (jitter > 0f ? Random.Range(0f, jitter) : 0f);
        }

        private Vector3 GetDeterministicRetreatDirection(IUnitAttackTarget target)
        {
            var targetObject = target != null ? target.SelectionGameObject : null;
            var targetId = targetObject != null ? targetObject.GetInstanceID() : 0;
            var sign = ((gameObject.GetInstanceID() ^ targetId) & 1) == 0 ? 1f : -1f;
            return new Vector3(sign, 0f, 0f);
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
