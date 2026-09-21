using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    public enum SelectionOutlineKind
    {
        Friendly,
        Enemy
    }

    public readonly struct PlayerSelectionSnapshot
    {
        public readonly IReadOnlyList<UnitCommandAgent> FriendlyUnits;
        public readonly IPlayerSelectableTarget PrimarySelection;

        public PlayerSelectionSnapshot(IReadOnlyList<UnitCommandAgent> friendlyUnits, IPlayerSelectableTarget primarySelection)
        {
            FriendlyUnits = friendlyUnits;
            PrimarySelection = primarySelection;
        }
    }

    public sealed class SelectionOutlineManager : MonoBehaviour
    {
        [SerializeField] private Color friendlyInnerOutlineColor = new Color(0.25f, 1f, 0.35f, 0.98f);
        [SerializeField] private Color friendlyOuterOutlineColor = new Color(0.02f, 0.45f, 0.08f, 0.9f);
        [SerializeField] private Color enemyInnerOutlineColor = new Color(1f, 0.22f, 0.12f, 0.98f);
        [SerializeField] private Color enemyOuterOutlineColor = new Color(0.55f, 0.03f, 0.02f, 0.9f);
        [SerializeField, Min(1)] private int maxOutlinedFriendlyUnits = 36;

        private readonly Dictionary<GameObject, SelectionOutlineEffect> activeOutlines = new Dictionary<GameObject, SelectionOutlineEffect>();
        private readonly List<GameObject> keepAliveBuffer = new List<GameObject>(48);
        private readonly HashSet<GameObject> keepAliveSet = new HashSet<GameObject>();
        private readonly List<GameObject> removeBuffer = new List<GameObject>(48);
        private PlayerUnitCommandController boundController;

        public static SelectionOutlineManager ActiveInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeManager()
        {
            EnsureRuntimeManager();
        }

        public static SelectionOutlineManager EnsureRuntimeManager()
        {
            if (ActiveInstance != null)
            {
                return ActiveInstance;
            }

            var manager = FindFirstObjectByType<SelectionOutlineManager>();
            if (manager == null)
            {
                manager = new GameObject("SelectionOutlineManager").AddComponent<SelectionOutlineManager>();
            }

            ActiveInstance = manager;
            if (PlayerUnitCommandController.ActiveInstance != null)
            {
                manager.BindController(PlayerUnitCommandController.ActiveInstance);
            }

            return manager;
        }

        private void Awake()
        {
            ActiveInstance = this;
            if (PlayerUnitCommandController.ActiveInstance != null)
            {
                BindController(PlayerUnitCommandController.ActiveInstance);
            }
        }

        private void OnEnable()
        {
            if (PlayerUnitCommandController.ActiveInstance != null)
            {
                BindController(PlayerUnitCommandController.ActiveInstance);
            }
        }

        private void OnDestroy()
        {
            if (boundController != null)
            {
                boundController.SelectionChanged -= HandleSelectionChanged;
                boundController = null;
            }

            ClearOutlines();
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public void BindController(PlayerUnitCommandController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            if (boundController != null)
            {
                boundController.SelectionChanged -= HandleSelectionChanged;
            }

            boundController = controller;
            if (boundController != null)
            {
                boundController.SelectionChanged += HandleSelectionChanged;
                HandleSelectionChanged(boundController.GetSelectionSnapshot());
            }
        }

        private void HandleSelectionChanged(PlayerSelectionSnapshot snapshot)
        {
            keepAliveBuffer.Clear();
            keepAliveSet.Clear();

            var outlinedFriendlyCount = 0;
            var units = snapshot.FriendlyUnits;
            if (units != null)
            {
                for (var i = 0; i < units.Count && outlinedFriendlyCount < maxOutlinedFriendlyUnits; i++)
                {
                    var unit = units[i];
                    if (unit == null || unit.Status == null)
                    {
                        continue;
                    }

                    var status = unit.Status;
                    if (status.SelectionGameObject == null || !status.SelectionGameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    ShowOutline(status, SelectionOutlineKind.Friendly);
                    outlinedFriendlyCount++;
                }
            }

            var primary = snapshot.PrimarySelection;
            if (primary != null
                && primary.SelectionGameObject != null
                && primary.SelectionGameObject.activeInHierarchy
                && !IsAlreadyOutlined(primary.SelectionGameObject))
            {
                var kind = boundController != null && primary.Team == boundController.PlayerTeam
                    ? SelectionOutlineKind.Friendly
                    : SelectionOutlineKind.Enemy;
                ShowOutline(primary, kind);
            }

            removeBuffer.Clear();
            foreach (var key in activeOutlines.Keys)
            {
                if (key == null || !keepAliveSet.Contains(key))
                {
                    removeBuffer.Add(key);
                }
            }

            for (var i = 0; i < removeBuffer.Count; i++)
            {
                HideOutline(removeBuffer[i]);
            }

            keepAliveBuffer.Clear();
            keepAliveSet.Clear();
            removeBuffer.Clear();
        }

        private void ShowOutline(IPlayerSelectableTarget target, SelectionOutlineKind kind)
        {
            var targetObject = target.SelectionGameObject;
            if (targetObject == null)
            {
                return;
            }

            var effect = GetOrCreateEffect(targetObject);
            if (effect == null)
            {
                return;
            }

            var innerColor = kind == SelectionOutlineKind.Enemy ? enemyInnerOutlineColor : friendlyInnerOutlineColor;
            var outerColor = kind == SelectionOutlineKind.Enemy ? enemyOuterOutlineColor : friendlyOuterOutlineColor;
            effect.Configure(innerColor, outerColor, null, -1);
            effect.SetOutlineVisible(true);
            keepAliveBuffer.Add(targetObject);
            keepAliveSet.Add(targetObject);
        }

        private SelectionOutlineEffect GetOrCreateEffect(GameObject targetObject)
        {
            if (activeOutlines.TryGetValue(targetObject, out var effect) && effect != null)
            {
                return effect;
            }

            effect = targetObject.GetComponent<SelectionOutlineEffect>();
            if (effect == null)
            {
                effect = targetObject.AddComponent<SelectionOutlineEffect>();
            }

            effect.Prepare();
            activeOutlines[targetObject] = effect;
            return effect;
        }

        private bool IsAlreadyOutlined(GameObject targetObject)
        {
            return targetObject != null && keepAliveSet.Contains(targetObject);
        }

        private void HideOutline(GameObject targetObject)
        {
            if (ReferenceEquals(targetObject, null))
            {
                return;
            }

            if (!activeOutlines.TryGetValue(targetObject, out var effect))
            {
                activeOutlines.Remove(targetObject);
                return;
            }

            if (effect != null)
            {
                effect.SetOutlineVisible(false);
            }

            activeOutlines.Remove(targetObject);
        }

        private void ClearOutlines()
        {
            foreach (var effect in activeOutlines.Values)
            {
                if (effect != null)
                {
                    effect.SetOutlineVisible(false);
                }
            }

            activeOutlines.Clear();
            keepAliveBuffer.Clear();
            keepAliveSet.Clear();
            removeBuffer.Clear();
        }
    }
}
