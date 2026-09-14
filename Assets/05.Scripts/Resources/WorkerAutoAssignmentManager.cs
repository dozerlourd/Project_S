using System.Collections.Generic;
using ProjectS.Buildings;
using ProjectS.Units;
using UnityEngine;

namespace ProjectS.Resources
{
    /// <summary>
    /// Assigns only idle workers to the least-served reachable resource node.
    /// Explicit player or AI commands remain authoritative while active.
    /// </summary>
    public sealed class WorkerAutoAssignmentManager : MonoBehaviour
    {
        private static readonly List<WorkerAutoAssignmentManager> ActiveManagers =
            new List<WorkerAutoAssignmentManager>();

        [SerializeField] private UnitTeam team = UnitTeam.Team1;
        [SerializeField] private ResourceType resourceType = ResourceType.Minerals;
        [SerializeField] private bool automaticAssignmentEnabled;
        [SerializeField, Min(0.1f)] private float reassessmentInterval = 0.5f;
        [SerializeField, Min(0)] private int maximumWorkersPerNode;
        [SerializeField] private bool logAssignments;

        private readonly Dictionary<UnitCommandAgent, ResourceNode> assignments =
            new Dictionary<UnitCommandAgent, ResourceNode>();
        private float nextReassessmentTime;
        private int lastStateSignature = int.MinValue;

        public UnitTeam Team => team;
        public bool AutomaticAssignmentEnabled => automaticAssignmentEnabled;
        public int AssignmentCount => AssignedWorkerCount;
        public int AssignedWorkerCount
        {
            get
            {
                var count = 0;
                foreach (var assignment in assignments)
                {
                    var agent = assignment.Key;
                    var node = assignment.Value;
                    if (agent != null
                        && agent.isActiveAndEnabled
                        && agent.Mode == UnitCommandMode.Interact
                        && ReferenceEquals(agent.LatestCommand.InteractableTarget, node)
                        && node != null
                        && node.isActiveAndEnabled
                        && node.CanGather())
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static WorkerAutoAssignmentManager FindForTeam(UnitTeam ownerTeam)
        {
            for (var i = ActiveManagers.Count - 1; i >= 0; i--)
            {
                var manager = ActiveManagers[i];
                if (manager == null)
                {
                    ActiveManagers.RemoveAt(i);
                    continue;
                }

                if (manager.isActiveAndEnabled && manager.team == ownerTeam)
                {
                    return manager;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            if (!ActiveManagers.Contains(this))
            {
                ActiveManagers.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveManagers.Remove(this);
            assignments.Clear();
        }

        private void Update()
        {
            if (!automaticAssignmentEnabled || Time.time < nextReassessmentTime)
            {
                return;
            }

            nextReassessmentTime = Time.time + Mathf.Max(0.1f, reassessmentInterval);
            var stateSignature = CalculateStateSignature();
            if (stateSignature != lastStateSignature)
            {
                lastStateSignature = stateSignature;
                ReassessAssignments();
            }
        }

        public void Configure(UnitTeam ownerTeam, bool enabled = true)
        {
            team = ownerTeam;
            automaticAssignmentEnabled = enabled;
            nextReassessmentTime = 0f;
            lastStateSignature = int.MinValue;
            if (!enabled)
            {
                assignments.Clear();
            }
        }

        public void SetAutomaticAssignmentEnabled(bool enabled)
        {
            automaticAssignmentEnabled = enabled;
            nextReassessmentTime = 0f;
            lastStateSignature = int.MinValue;
            if (!enabled)
            {
                assignments.Clear();
            }
        }

        public bool ToggleAutomaticAssignment()
        {
            SetAutomaticAssignmentEnabled(!automaticAssignmentEnabled);
            return automaticAssignmentEnabled;
        }

        public void ReassessAssignments()
        {
            RemoveInvalidAssignments();

            var workersPerNode = new Dictionary<ResourceNode, int>();
            foreach (var assignment in assignments)
            {
                if (assignment.Value != null)
                {
                    workersPerNode.TryGetValue(assignment.Value, out var count);
                    workersPerNode[assignment.Value] = count + 1;
                }
            }

            var agents = UnitRegistry.GetAgents(team);
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (!IsEligibleIdleWorker(agent) || assignments.ContainsKey(agent))
                {
                    continue;
                }

                var node = FindBestNode(agent.transform.position, workersPerNode);
                if (node == null)
                {
                    continue;
                }

                agent.Issue(new UnitCommand(
                    UnitCommandMode.Interact,
                    node.InteractionPoint,
                    null,
                    node,
                    false));

                if (agent.Mode == UnitCommandMode.Interact)
                {
                    assignments[agent] = node;
                    workersPerNode.TryGetValue(node, out var count);
                    workersPerNode[node] = count + 1;
                    if (logAssignments)
                    {
                        Debug.Log($"[WorkerAutoAssign] {agent.name} -> {node.name} ({resourceType}).", this);
                    }
                }
            }
        }

        private void RemoveInvalidAssignments()
        {
            var toRemove = new List<UnitCommandAgent>();
            foreach (var assignment in assignments)
            {
                var agent = assignment.Key;
                var node = assignment.Value;
                if (agent == null
                    || !agent.isActiveAndEnabled
                    || agent.Status == null
                    || agent.Status.Team != team
                    || !agent.Status.CanGatherResources
                    || agent.Mode != UnitCommandMode.Interact
                    || !ReferenceEquals(agent.LatestCommand.InteractableTarget, node)
                    || node == null
                    || !node.isActiveAndEnabled
                    || node.ResourceType != resourceType
                    || !node.CanGather()
                    || ResourceDropOff.FindNearest(team, node.transform.position) == null)
                {
                    toRemove.Add(agent);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                assignments.Remove(toRemove[i]);
            }
        }

        private bool IsEligibleIdleWorker(UnitCommandAgent agent)
        {
            var status = agent != null ? agent.Status : null;
            return agent != null
                && agent.isActiveAndEnabled
                && status != null
                && status.Team == team
                && status.CanGatherResources
                && status.Roles.HasFlag(UnitRole.Resource)
                && agent.Mode == UnitCommandMode.Idle;
        }

        private ResourceNode FindBestNode(Vector3 position, Dictionary<ResourceNode, int> workersPerNode)
        {
            ResourceNode best = null;
            var bestWorkerCount = int.MaxValue;
            var bestDistance = float.PositiveInfinity;
            var nodes = ResourceNode.AllNodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null
                    || !node.isActiveAndEnabled
                    || node.ResourceType != resourceType
                    || !node.CanGather()
                    || ResourceDropOff.FindNearest(team, node.transform.position) == null)
                {
                    continue;
                }

                workersPerNode.TryGetValue(node, out var workerCount);
                if (maximumWorkersPerNode > 0 && workerCount >= maximumWorkersPerNode)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(node.transform.position - position);
                if (workerCount > bestWorkerCount
                    || (workerCount == bestWorkerCount && distance >= bestDistance))
                {
                    continue;
                }

                best = node;
                bestWorkerCount = workerCount;
                bestDistance = distance;
            }

            return best;
        }

        private int CalculateStateSignature()
        {
            unchecked
            {
                var signature = 17;
                var agents = UnitRegistry.GetAgents(team);
                for (var i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    if (agent == null)
                    {
                        continue;
                    }

                    signature = signature * 31 + agent.GetInstanceID();
                    signature = signature * 31 + (int)agent.Mode;
                    signature = signature * 31 + agent.LatestCommandId;
                }

                var nodes = ResourceNode.AllNodes;
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    signature = signature * 31 + node.GetInstanceID();
                    signature = signature * 31 + node.RemainingAmount;
                    signature = signature * 31 + (node.isActiveAndEnabled ? 1 : 0);
                }

                var dropOffs = ResourceDropOff.All;
                for (var i = 0; i < dropOffs.Count; i++)
                {
                    var dropOff = dropOffs[i];
                    if (dropOff == null)
                    {
                        continue;
                    }

                    signature = signature * 31 + dropOff.GetInstanceID();
                    signature = signature * 31 + (dropOff.CanAcceptDeposits ? 1 : 0);
                    signature = signature * 31 + (dropOff.gameObject.activeInHierarchy ? 1 : 0);
                }

                return signature;
            }
        }
    }
}
