using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class WorkerGatherController : MonoBehaviour, IUnitInteractionHandler
    {
        [SerializeField, Min(1)] private int carryCapacity = 5;
        [SerializeField, Min(0.1f)] private float fallbackGatherRange = 0.85f;
        [SerializeField, Min(0.1f)] private float fallbackDropOffRange = 1.25f;

        private PrototypeUnitStatus status;
        private UnitCommandAgent commandAgent;
        private UnitPathAgent pathAgent;
        private ResourceNode targetNode;
        private ResourceDropOff targetDropOff;
        private ResourceType carriedType;
        private GatherState state = GatherState.Idle;
        private int carriedAmount;
        private float gatherTimer;

        public bool HasCarriedResources => carriedAmount > 0;
        public ResourceType CarriedType => carriedType;
        public int CarriedAmount => carriedAmount;

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

            if (targetNode == null || (!targetNode.CanGather() && carriedAmount <= 0))
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
                pathAgent.MoveTo(targetDropOff.InteractionPoint);
                return true;
            }

            return false;
        }

        private bool StartGathering(ResourceNode resourceNode)
        {
            if (status == null || !status.CanGatherResources || resourceNode == null || !resourceNode.CanGather())
            {
                return false;
            }

            targetNode = resourceNode;
            targetDropOff = null;
            carriedAmount = 0;
            gatherTimer = 0f;
            state = GatherState.MovingToResource;
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

            targetDropOff = ResourceDropOff.FindNearest(status.Team, transform.position);
            if (targetDropOff == null)
            {
                state = GatherState.Idle;
                commandAgent.Stop();
                return;
            }

            state = GatherState.ReturningToDropOff;
            pathAgent.MoveTo(targetDropOff.InteractionPoint);
        }

        private void UpdateReturnToDropOff()
        {
            if (targetDropOff == null)
            {
                targetDropOff = ResourceDropOff.FindNearest(status.Team, transform.position);
                if (targetDropOff == null)
                {
                    return;
                }

                pathAgent.MoveTo(targetDropOff.InteractionPoint);
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
            carriedAmount = 0;
            if (!deposited || targetNode == null || !targetNode.CanGather())
            {
                state = GatherState.Idle;
                commandAgent.Stop();
                return;
            }

            state = GatherState.MovingToResource;
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

        private enum GatherState
        {
            Idle,
            MovingToResource,
            Gathering,
            ReturningToDropOff
        }
    }
}
