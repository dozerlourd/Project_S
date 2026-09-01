using System.Collections.Generic;
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

    public interface IUnitCommandInterruptHandler
    {
        void OnUnitCommandInterrupted();
    }

    public interface IUnitBuildPlacementService
    {
        Vector2Int DefaultFootprint { get; }
        string LastPlacementFailureReason { get; }
        IReadOnlyList<UnitBuildPlacementPreviewCell> GetDefaultConstructionSitePreviewCells(Vector3 worldPosition);
        bool CanPlaceDefaultConstructionSite(Vector3 worldPosition);
        bool TryPlaceDefaultConstructionSite(Vector3 worldPosition, out IUnitInteractableTarget constructionSite);
    }

    public readonly struct UnitBuildPlacementPreviewCell
    {
        public readonly Vector3 WorldCenter;
        public readonly bool CanPlace;
        public readonly string FailureReason;

        public UnitBuildPlacementPreviewCell(Vector3 worldCenter, bool canPlace, string failureReason)
        {
            WorldCenter = worldCenter;
            CanPlace = canPlace;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public interface IUnitRallyPointService
    {
        void SetRallyPoint(Vector3 point);
    }

    public interface IPlayerSelectableTarget
    {
        UnitTeam Team { get; }
        string SelectionName { get; }
        Transform SelectionTransform { get; }
        GameObject SelectionGameObject { get; }
    }

    public interface IUnitAttackTarget : IPlayerSelectableTarget
    {
        bool IsAlive { get; }
        Collider2D AttackCollider { get; }
        void TakeDamage(float amount);
        void TakeDamage(float amount, IUnitAttackTarget attacker);
    }

    public readonly struct UnitCommand
    {
        public readonly UnitCommandMode Mode;
        public readonly Vector3 Destination;
        public readonly IUnitAttackTarget Target;
        public readonly IUnitInteractableTarget InteractableTarget;
        public readonly bool Queue;

        public UnitCommand(UnitCommandMode mode, Vector3 destination, IUnitAttackTarget target, bool queue)
            : this(mode, destination, target, null, queue)
        {
        }

        public UnitCommand(
            UnitCommandMode mode,
            Vector3 destination,
            IUnitAttackTarget target,
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
