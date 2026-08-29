using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Buildings
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    [RequireComponent(typeof(UnitPathAgent))]
    public sealed class WorkerConstructionController : MonoBehaviour, IUnitInteractionHandler
    {
        [SerializeField, Min(0.1f)] private float buildPower = 1f;

        private PrototypeUnitStatus status;
        private UnitCommandAgent commandAgent;
        private UnitPathAgent pathAgent;
        private ConstructionSite targetSite;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            commandAgent = GetComponent<UnitCommandAgent>();
            pathAgent = GetComponent<UnitPathAgent>();
        }

        private void Update()
        {
            if (targetSite == null)
            {
                return;
            }

            if (commandAgent.Mode != UnitCommandMode.Interact || targetSite.Completed)
            {
                targetSite = null;
                if (commandAgent.Mode == UnitCommandMode.Interact)
                {
                    commandAgent.Stop();
                }

                return;
            }

            if (!IsInRange(targetSite.InteractionPoint, targetSite.InteractionRange))
            {
                if (!pathAgent.HasPath)
                {
                    pathAgent.MoveTo(targetSite.InteractionPoint);
                }

                return;
            }

            pathAgent.ClearPath();
            targetSite.TryContribute(commandAgent, buildPower * Time.deltaTime);
            if (targetSite != null && targetSite.Completed)
            {
                targetSite = null;
                commandAgent.Stop();
            }
        }

        public bool TryHandleInteractionCommand(IUnitInteractableTarget target)
        {
            if (!(target is ConstructionSite site) || status == null || !status.Roles.HasFlag(UnitRole.Builder))
            {
                return false;
            }

            targetSite = site;
            pathAgent.MoveTo(site.InteractionPoint);
            return true;
        }

        private bool IsInRange(Vector3 point, float range)
        {
            return Vector3.Distance(transform.position, point) <= Mathf.Max(0.1f, range);
        }
    }
}
