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
        Patrol
    }

    public readonly struct UnitCommand
    {
        public readonly UnitCommandMode Mode;
        public readonly Vector3 Destination;
        public readonly PrototypeUnitStatus Target;
        public readonly bool Queue;

        public UnitCommand(UnitCommandMode mode, Vector3 destination, PrototypeUnitStatus target, bool queue)
        {
            Mode = mode;
            Destination = destination;
            Target = target;
            Queue = queue;
        }
    }
}
