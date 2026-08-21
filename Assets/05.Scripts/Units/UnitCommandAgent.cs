using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class UnitCommandAgent : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float targetScanInterval = 0.2f;

        private readonly Queue<UnitCommand> queuedCommands = new Queue<UnitCommand>();
        private readonly Collider2D[] targetBuffer = new Collider2D[32];
        private ContactFilter2D targetFilter;
        private PrototypeUnitStatus status;
        private UnitPathAgent pathAgent;
        private UnitCommandMode mode = UnitCommandMode.Idle;
        private Vector3 commandDestination;
        private Vector3 patrolStart;
        private Vector3 patrolEnd;
        private PrototypeUnitStatus priorityTarget;
        private float nextScanTime;

        public UnitCommandMode Mode => mode;
        public PrototypeUnitStatus PriorityTarget => priorityTarget;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            pathAgent = GetComponent<UnitPathAgent>();
            targetFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetMask,
                useTriggers = true
            };
        }

        private void Update()
        {
            if (mode == UnitCommandMode.AttackMove || mode == UnitCommandMode.Patrol || mode == UnitCommandMode.HoldPosition)
            {
                ScanForTargets();
            }

            if (mode == UnitCommandMode.FocusAttack)
            {
                UpdateFocusAttack();
            }

            if (mode == UnitCommandMode.Patrol && !pathAgent.HasPath && priorityTarget == null)
            {
                SwapPatrolEndpoint();
            }

            if ((mode == UnitCommandMode.Move || mode == UnitCommandMode.AttackMove) && !pathAgent.HasPath && priorityTarget == null)
            {
                CompleteCurrentCommand();
            }
        }

        public void Issue(UnitCommand command)
        {
            if (command.Queue)
            {
                queuedCommands.Enqueue(command);
                return;
            }

            queuedCommands.Clear();
            Execute(command);
        }

        public void Stop()
        {
            queuedCommands.Clear();
            priorityTarget = null;
            mode = UnitCommandMode.Idle;
            pathAgent.ClearPath();
        }

        public void HoldPosition()
        {
            queuedCommands.Clear();
            priorityTarget = null;
            mode = UnitCommandMode.HoldPosition;
            pathAgent.ClearPath();
        }

        private void Execute(UnitCommand command)
        {
            priorityTarget = command.Target;
            commandDestination = command.Destination;
            mode = command.Mode;

            switch (command.Mode)
            {
                case UnitCommandMode.Move:
                    pathAgent.MoveTo(command.Destination);
                    break;
                case UnitCommandMode.AttackMove:
                    pathAgent.MoveTo(command.Destination);
                    break;
                case UnitCommandMode.FocusAttack:
                    UpdateFocusAttack();
                    break;
                case UnitCommandMode.HoldPosition:
                    pathAgent.ClearPath();
                    break;
                case UnitCommandMode.Patrol:
                    patrolStart = transform.position;
                    patrolEnd = command.Destination;
                    pathAgent.MoveTo(patrolEnd);
                    break;
                default:
                    pathAgent.ClearPath();
                    break;
            }
        }

        private void UpdateFocusAttack()
        {
            if (priorityTarget == null)
            {
                CompleteCurrentCommand();
                return;
            }

            var distance = Vector3.Distance(transform.position, priorityTarget.transform.position);
            if (distance > status.AttackRange)
            {
                pathAgent.MoveTo(priorityTarget.transform.position);
                return;
            }

            pathAgent.ClearPath();
        }

        private void ScanForTargets()
        {
            if (Time.time < nextScanTime || !CanAttack())
            {
                return;
            }

            nextScanTime = Time.time + targetScanInterval;
            if (priorityTarget != null && IsEnemy(priorityTarget) && IsInAttackRange(priorityTarget))
            {
                pathAgent.ClearPath();
                return;
            }

            targetFilter.layerMask = targetMask;
            var hitCount = Physics2D.OverlapCircle(transform.position, status.AttackRange, targetFilter, targetBuffer);
            for (var i = 0; i < hitCount; i++)
            {
                var target = targetBuffer[i].GetComponentInParent<PrototypeUnitStatus>();
                if (target == null || !IsEnemy(target))
                {
                    continue;
                }

                priorityTarget = target;
                pathAgent.ClearPath();
                return;
            }

            priorityTarget = null;
            if (mode == UnitCommandMode.AttackMove && !pathAgent.HasPath)
            {
                pathAgent.MoveTo(commandDestination);
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

        private bool IsInAttackRange(PrototypeUnitStatus target)
        {
            return Vector3.Distance(transform.position, target.transform.position) <= status.AttackRange;
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
            if (queuedCommands.Count > 0)
            {
                Execute(queuedCommands.Dequeue());
                return;
            }

            mode = UnitCommandMode.Idle;
            priorityTarget = null;
        }
    }
}
