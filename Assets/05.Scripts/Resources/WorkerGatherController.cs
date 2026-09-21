using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectS.Resources
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class WorkerGatherController : MonoBehaviour, IUnitInteractionHandler, IUnitCommandInterruptHandler
    {
        [SerializeField, Min(1)] private int carryCapacity = 5;
        [FormerlySerializedAs("fallbackGatherRange")]
        [SerializeField, Min(0.05f)] private float gatherRange = 0.2f;
        [FormerlySerializedAs("fallbackDropOffRange")]
        [SerializeField, Min(0.05f)] private float dropOffRange = 0.45f;
        [SerializeField, Min(0.05f)] private float resourcePathRetryInterval = 0.25f;
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
        private float nextResourcePathRetryTime;
        private string lastFailureReason;

        public bool HasCarriedResources => carriedAmount > 0;
        public ResourceType CarriedType => carriedType;
        public int CarriedAmount => carriedAmount;
        public string LastFailureReason => lastFailureReason;
        public string GatherStateName => state.ToString();
        public string DebugStateSnapshot =>
            $"state={state}, mode={commandAgent?.Mode}, action={commandAgent?.ActionState}, "
            + $"position={transform.position}, target={(targetNode != null ? targetNode.name : "none")}, "
            + $"dropOff={(targetDropOff != null ? targetDropOff.name : "none")}, carried={carriedAmount}, "
            + $"dropOffDistance={GetColliderDistance(targetDropOff)}, dropOffRange={GetDropOffRange(targetDropOff)}, "
            + $"hasPath={pathAgent?.HasPath}, pending={pathAgent?.HasPendingPathRequest}, "
            + $"waypoint={pathAgent?.CurrentWaypoint}, requested={pathAgent?.RequestedDestination}";

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            commandAgent = GetComponent<UnitCommandAgent>();
            pathAgent = GetComponent<UnitPathAgent>();
            UpdateOccupancyParticipation();
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

            UpdateOccupancyParticipation();

            if (targetNode == null || (!targetNode.isActiveAndEnabled && carriedAmount <= 0) || (!targetNode.CanGather() && carriedAmount <= 0))
            {
                ContinueWithNearestAvailableNode();
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
                UpdateOccupancyParticipation();
                LogGathering($"Returning carried {carriedAmount} {carriedType} to {FormatTargetName(targetDropOff)}.");
                MoveToDropOffInteractionPoint();
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
            nextResourcePathRetryTime = 0f;
            LogGathering($"Accepted resource gather command for {FormatTargetName(targetNode)}.");
            LogGathering($"Started gathering route to {FormatTargetName(targetNode)}.");
            MoveToResourceInteractionPoint();
            return true;
        }

        private void UpdateMoveToResource()
        {
            if (targetNode == null)
            {
                CancelGathering();
                return;
            }

            if (!IsInResourceRange(targetNode, GetGatherRange(targetNode)))
            {
                if (!pathAgent.HasPath && Time.time >= nextResourcePathRetryTime)
                {
                    nextResourcePathRetryTime = Time.time + Mathf.Max(0.05f, resourcePathRetryInterval);
                    MoveToResourceInteractionPoint();
                }

                return;
            }

            pathAgent.ClearPath();
            gatherTimer = 0f;
            state = GatherState.Gathering;
            UpdateOccupancyParticipation();
            LogGathering(
                $"Reached interaction range for {targetNode.ResourceType} at {transform.position}. "
                + $"Target point: {targetNode.InteractionPoint}. Starting gather at {FormatTargetName(targetNode)}.");
        }

        private void UpdateGathering()
        {
            if (targetNode == null || !targetNode.CanGather())
            {
                ContinueWithNearestAvailableNode();
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
            UpdateOccupancyParticipation();
            LogGathering($"Found drop-off {FormatTargetName(targetDropOff)}. Returning with {carriedAmount} {carriedType}.");
            MoveToDropOffInteractionPoint();
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

                MoveToDropOffInteractionPoint();
                LogGathering($"Repathing to replacement drop-off {FormatTargetName(targetDropOff)}.");
            }

            if (!IsInDropOffRange(targetDropOff, GetDropOffRange(targetDropOff)))
            {
                if (!pathAgent.HasPath)
                {
                    MoveToDropOffInteractionPoint();
                }

                return;
            }

            var deposited = targetDropOff.TryDeposit(status.Team, CreateCarriedAmount());
            var depositedAmount = carriedAmount;
            var depositedType = carriedType;
            carriedAmount = 0;
            if (!deposited)
            {
                Fail("Failed to deposit carried resources.");
                state = GatherState.Idle;
                commandAgent.Stop();
                return;
            }

            LogGathering($"Deposited {depositedAmount} {depositedType} at {FormatTargetName(targetDropOff)}.");
            if (targetNode == null || !targetNode.isActiveAndEnabled || !targetNode.CanGather())
            {
                ContinueWithNearestAvailableNode();
                return;
            }

            state = GatherState.MovingToResource;
            UpdateOccupancyParticipation();
            LogGathering($"Repeating gather route to {FormatTargetName(targetNode)}.");
            MoveToResourceInteractionPoint();
        }

        private void MoveToResourceInteractionPoint()
        {
            if (targetNode == null)
            {
                return;
            }

            var resourceCollider = targetNode.GetComponent<Collider2D>();
            var range = GetGatherRange(targetNode);
            if (resourceCollider != null)
            {
                pathAgent.MoveToResourceInteraction(targetNode.InteractionPoint, resourceCollider, range);
                return;
            }

            pathAgent.MoveTo(targetNode.InteractionPoint);
        }

        private void MoveToDropOffInteractionPoint()
        {
            if (targetDropOff == null)
            {
                return;
            }

            var dropOffCollider = targetDropOff.GetComponent<Collider2D>();
            if (dropOffCollider != null)
            {
                pathAgent.MoveToInteraction(
                    targetDropOff.InteractionPoint,
                    dropOffCollider,
                    GetDropOffRange(targetDropOff));
                return;
            }

            pathAgent.MoveTo(targetDropOff.InteractionPoint);
        }

        private float GetGatherRange(ResourceNode resourceNode)
        {
            return resourceNode != null
                ? Mathf.Min(Mathf.Max(0.05f, gatherRange), Mathf.Max(0.05f, resourceNode.InteractionRange))
                : Mathf.Max(0.05f, gatherRange);
        }

        private float GetDropOffRange(ResourceDropOff dropOff)
        {
            return dropOff != null
                ? Mathf.Min(Mathf.Max(0.05f, dropOffRange), Mathf.Max(0.05f, dropOff.InteractionRange))
                : Mathf.Max(0.05f, dropOffRange);
        }

        private void ContinueWithNearestAvailableNode()
        {
            var resourceType = targetNode != null ? targetNode.ResourceType : carriedType;
            var replacement = ResourceNode.FindNearestAvailable(transform.position, resourceType);
            if (replacement == null)
            {
                CancelGathering(true);
                return;
            }

            targetNode = replacement;
            targetDropOff = null;
            gatherTimer = 0f;
            nextResourcePathRetryTime = 0f;
            state = GatherState.MovingToResource;
            UpdateOccupancyParticipation();
            LogGathering($"Resource node depleted. Continuing at nearest {resourceType} node {FormatTargetName(targetNode)}.");
            MoveToResourceInteractionPoint();
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

        private bool IsInResourceRange(ResourceNode resourceNode, float range)
        {
            if (resourceNode == null)
            {
                return false;
            }

            var unitCollider = GetComponent<Collider2D>();
            var resourceCollider = resourceNode.GetComponent<Collider2D>();
            if (unitCollider != null && resourceCollider != null)
            {
                var distance = unitCollider.Distance(resourceCollider);
                if (distance.isValid)
                {
                    return distance.distance <= Mathf.Max(0.1f, range);
                }
            }

            return IsInRange(resourceNode.InteractionPoint, range);
        }

        private bool IsInDropOffRange(ResourceDropOff dropOff, float range)
        {
            var unitCollider = GetComponent<Collider2D>();
            var dropOffCollider = dropOff != null ? dropOff.GetComponent<Collider2D>() : null;
            if (unitCollider != null && dropOffCollider != null)
            {
                var distance = unitCollider.Distance(dropOffCollider);
                if (distance.isValid)
                {
                    return distance.distance <= Mathf.Max(0.1f, range);
                }
            }

            return dropOff != null && IsInRange(dropOff.InteractionPoint, range);
        }

        private float GetColliderDistance(ResourceDropOff dropOff)
        {
            var unitCollider = GetComponent<Collider2D>();
            var dropOffCollider = dropOff != null ? dropOff.GetComponent<Collider2D>() : null;
            if (unitCollider == null || dropOffCollider == null)
            {
                return -1f;
            }

            var distance = unitCollider.Distance(dropOffCollider);
            return distance.isValid ? distance.distance : -1f;
        }

        private void CancelGathering(bool stopCommand = true)
        {
            targetNode = null;
            targetDropOff = null;
            carriedAmount = 0;
            gatherTimer = 0f;
            state = GatherState.Idle;
            UpdateOccupancyParticipation();
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

        private void UpdateOccupancyParticipation()
        {
            if (pathAgent != null)
            {
                pathAgent.SetOccupancyParticipation(
                    state != GatherState.Gathering && state != GatherState.ReturningToDropOff);
            }
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
