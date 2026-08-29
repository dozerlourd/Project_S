using UnityEngine;

namespace ProjectS.Units
{
    public enum UnitCommandMode
    {
        Idle,
        Move,
        AttackMove,
        FocusAttack,
        HoldPosition,
        Patrol,
        Interact
    }

    public enum UnitActionState
    {
        Idle,
        Moving,
        AttackMoving,
        ChasingTarget,
        AttackingTarget,
        HoldingPosition,
        Patrolling,
        Interacting
    }

    public interface IUnitInteractableTarget
    {
        Vector3 InteractionPoint { get; }
        float InteractionRange { get; }
        bool CanInteract(UnitCommandAgent agent);
    }

    public interface IUnitInteractionHandler
    {
        bool TryHandleInteractionCommand(IUnitInteractableTarget target);
    }

    public interface IUnitBuildPlacementService
    {
        bool TryPlaceDefaultConstructionSite(Vector3 worldPosition, out IUnitInteractableTarget constructionSite);
    }

    public interface IPlayerSelectableTarget
    {
        UnitTeam Team { get; }
        string SelectionName { get; }
        Transform SelectionTransform { get; }
        GameObject SelectionGameObject { get; }
    }

    public readonly struct UnitCommand
    {
        public readonly UnitCommandMode Mode;
        public readonly Vector3 Destination;
        public readonly PrototypeUnitStatus Target;
        public readonly IUnitInteractableTarget InteractableTarget;
        public readonly bool Queue;

        public UnitCommand(UnitCommandMode mode, Vector3 destination, PrototypeUnitStatus target, bool queue)
            : this(mode, destination, target, null, queue)
        {
        }

        public UnitCommand(
            UnitCommandMode mode,
            Vector3 destination,
            PrototypeUnitStatus target,
            IUnitInteractableTarget interactableTarget,
            bool queue)
        {
            Mode = mode;
            Destination = destination;
            Target = target;
            InteractableTarget = interactableTarget;
            Queue = queue;
        }
    }
}
