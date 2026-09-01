using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class WorkerGatherController : MonoBehaviour, IUnitInteractionHandler, IUnitCommandInterruptHandler
    {
        [SerializeField, Min(1)] private int carryCapacity = 5;
        [SerializeField, Min(0.1f)] private float fallbackGatherRange = 0.85f;
        [SerializeField, Min(0.1f)] private float fallbackDropOffRange = 1.25f;
        [SerializeField] private bool logGathering = true;

        private PrototypeUnitStatus status;
        private UnitCommandAgent commandAgent;
        private UnitPathAgent pathAgent;
        private ResourceNode targetNode;
        private ResourceDropOff targetDropOff;
        private ResourceType carriedType;
        private GatherState state = GatherState.Idle;
        private int carriedAmount;
        private float gatherTimer;
        private string lastFailureReason;

        public bool HasCarriedResources => carriedAmount > 0;
        public ResourceType CarriedType => carriedType;
        public int CarriedAmount => carriedAmount;
        public string LastFailureReason => lastFailureReason;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            commandAgent = GetComponent<UnitCommandAgent>();
            pathAgent = GetComponent<UnitPathAgent>();
        }

        private void Update()
        {
            if (state == GatherState.Idle)
            {
                return;
            }

            if (commandAgent.Mode != UnitCommandMode.Interact)
            {
                CancelGathering(false);
                return;
            }

            if (targetNode == null || (!targetNode.isActiveAndEnabled && carriedAmount <= 0) || (!targetNode.CanGather() && carriedAmount <= 0))
            {
                CancelGathering(true);
                return;
            }

            switch (state)
            {
                case GatherState.MovingToResource:
                    UpdateMoveToResource();
                    break;
                case GatherState.Gathering:
                    UpdateGathering();
                    break;
                case GatherState.ReturningToDropOff:
                    UpdateReturnToDropOff();
                    break;
            }
        }

        public bool TryHandleInteractionCommand(IUnitInteractableTarget target)
        {
            if (target is ResourceNode resourceNode)
            {
                return StartGathering(resourceNode);
            }

            if (target is ResourceDropOff dropOff && carriedAmount > 0 && dropOff.Team == status.Team)
            {
                targetDropOff = dropOff;
                state = GatherState.ReturningToDropOff;
                LogGathering($"Returning carried {carriedAmount} {carriedType} to {FormatTargetName(targetDropOff)}.");
                pathAgent.MoveTo(targetDropOff.InteractionPoint);
                return true;
            }

            if (target is ResourceDropOff)
            {
                return Fail("Cannot deposit resources at an enemy or unavailable drop-off.");
            }

            return false;
        }

        public void OnUnitCommandInterrupted()
        {
            CancelGathering(false);
        }

        private bool StartGathering(ResourceNode resourceNode)
        {
            if (status == null || !status.CanGatherResources || resourceNode == null || !resourceNode.CanGather())
            {
                return Fail("Cannot start gathering because the worker or resource node is unavailable.");
            }

            targetNode = resourceNode;
            targetDropOff = null;
            carriedAmount = 0;
            gatherTimer = 0f;
            lastFailureReason = string.Empty;
            state = GatherState.MovingToResource;
            LogGathering($"Accepted resource gather command for {FormatTargetName(targetNode)}.");
            LogGathering($"Started gathering route to {FormatTargetName(targetNode)}.");
            pathAgent.MoveTo(targetNode.InteractionPoint);
            return true;
        }

        private void UpdateMoveToResource()
        {
            if (targetNode == null)
            {
                CancelGathering();
                return;
            }

            if (!IsInRange(targetNode.InteractionPoint, Mathf.Max(fallbackGatherRange, targetNode.InteractionRange)))
            {
                if (!pathAgent.HasPath)
                {
                    pathAgent.MoveTo(targetNode.InteractionPoint);
                }

                return;
            }

            pathAgent.ClearPath();
            gatherTimer = 0f;
            state = GatherState.Gathering;
            LogGathering($"Started gathering {targetNode.ResourceType} from {FormatTargetName(targetNode)}.");
        }

        private void UpdateGathering()
        {
            if (targetNode == null || !targetNode.CanGather())
            {
                CancelGathering();
                return;
            }

            gatherTimer += Time.deltaTime;
            if (gatherTimer < targetNode.GatherDuration)
            {
                return;
            }

            carriedType = targetNode.ResourceType;
            carriedAmount = Mathf.Min(carryCapacity, targetNode.TryGather());
            if (carriedAmount <= 0)
            {
                CancelGathering();
                return;
            }

            LogGathering(
                $"Gathered {carriedAmount} {carriedType} from {FormatTargetName(targetNode)}. Remaining: {targetNode.RemainingAmount}.");

            targetDropOff = ResourceDropOff.FindNearest(status.Team, transform.position);
            if (targetDropOff == null)
            {
                Fail("No available resource drop-off found for carried resources.");
                state = GatherState.Idle;
                commandAgent.Stop();
                return;
            }

            state = GatherState.ReturningToDropOff;
            LogGathering($"Found drop-off {FormatTargetName(targetDropOff)}. Returning with {carriedAmount} {carriedType}.");
            pathAgent.MoveTo(targetDropOff.InteractionPoint);
        }

        private void UpdateReturnToDropOff()
        {
            if (targetDropOff == null)
            {
                targetDropOff = ResourceDropOff.FindNearest(status.Team, transform.position);
                if (targetDropOff == null)
                {
                    Fail("Lost access to a resource drop-off while returning carried resources.");
                    state = GatherState.Idle;
                    commandAgent.Stop();
                    return;
                }

                pathAgent.MoveTo(targetDropOff.InteractionPoint);
                LogGathering($"Repathing to replacement drop-off {FormatTargetName(targetDropOff)}.");
            }

            if (!IsInRange(targetDropOff.InteractionPoint, Mathf.Max(fallbackDropOffRange, targetDropOff.InteractionRange)))
            {
                if (!pathAgent.HasPath)
                {
                    pathAgent.MoveTo(targetDropOff.InteractionPoint);
                }

                return;
            }

            var deposited = targetDropOff.TryDeposit(status.Team, CreateCarriedAmount());
            var depositedAmount = carriedAmount;
            var depositedType = carriedType;
            carriedAmount = 0;
            if (!deposited || targetNode == null || !targetNode.isActiveAndEnabled || !targetNode.CanGather())
            {
                if (!deposited)
                {
                    Fail("Failed to deposit carried resources.");
                }

                state = GatherState.Idle;
                commandAgent.Stop();
                return;
            }

            LogGathering($"Deposited {depositedAmount} {depositedType} at {FormatTargetName(targetDropOff)}.");
            state = GatherState.MovingToResource;
            LogGathering($"Repeating gather route to {FormatTargetName(targetNode)}.");
            pathAgent.MoveTo(targetNode.InteractionPoint);
        }

        private ResourceAmount CreateCarriedAmount()
        {
            return carriedType == ResourceType.Minerals
                ? new ResourceAmount(carriedAmount, 0)
                : new ResourceAmount(0, carriedAmount);
        }

        private bool IsInRange(Vector3 point, float range)
        {
            return Vector3.Distance(transform.position, point) <= Mathf.Max(0.1f, range);
        }

        private void CancelGathering(bool stopCommand = true)
        {
            targetNode = null;
            targetDropOff = null;
            carriedAmount = 0;
            gatherTimer = 0f;
            state = GatherState.Idle;
            if (stopCommand && commandAgent != null && commandAgent.Mode == UnitCommandMode.Interact)
            {
                commandAgent.Stop();
            }
        }

        private bool Fail(string reason)
        {
            lastFailureReason = reason;
            Debug.LogWarning(reason, this);
            return false;
        }

        private void LogGathering(string message)
        {
            if (logGathering)
            {
                Debug.Log($"[WorkerGather] {name}: {message}", this);
            }
        }

        private static string FormatTargetName(Component target)
        {
            return target != null ? target.gameObject.name : "MissingTarget";
        }

        private enum GatherState
        {
            Idle,
            MovingToResource,
            Gathering,
            ReturningToDropOff
        }
    }
}
