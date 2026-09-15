using System;
using System.Collections.Generic;
using ProjectS.Visibility;
using UnityEngine;

namespace ProjectS.Units
{
    public enum CombatFeedbackType
    {
        AttackHit,
        Damaged,
        Death,
        ActiveSkill
    }

    public readonly struct CombatFeedbackEvent
    {
        public readonly CombatFeedbackType Type;
        public readonly Vector3 WorldPosition;
        public readonly UnitTeam SubjectTeam;
        public readonly UnitTeam SourceTeam;

        public CombatFeedbackEvent(
            CombatFeedbackType type,
            Vector3 worldPosition,
            UnitTeam subjectTeam,
            UnitTeam sourceTeam)
        {
            Type = type;
            WorldPosition = worldPosition;
            SubjectTeam = subjectTeam;
            SourceTeam = sourceTeam;
        }
    }

    public static class CombatFeedbackEvents
    {
        public static event Action<CombatFeedbackEvent> Published;

        public static void Publish(
            CombatFeedbackType type,
            Vector3 worldPosition,
            UnitTeam subjectTeam,
            UnitTeam sourceTeam = UnitTeam.Team1)
        {
            Published?.Invoke(new CombatFeedbackEvent(type, worldPosition, subjectTeam, sourceTeam));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            Published = null;
        }
    }

    public sealed class CombatFeedbackService : MonoBehaviour
    {
        private const int CircleSegmentCount = 20;

        private sealed class PooledVisual
        {
            public LineRenderer Line;
            public CombatFeedbackEvent FeedbackEvent;
            public Color Color;
            public float StartRadius;
            public float EndRadius;
            public float StartTime;
            public float EndTime;
            public bool Active;
        }

        [SerializeField, Min(1)] private int prewarmCount = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 48;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 45;
        [SerializeField, Min(0.01f)] private float attackHitDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float damagedDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float deathDuration = 0.75f;
        [SerializeField, Min(0.01f)] private float activeSkillDuration = 0.65f;

        private readonly List<PooledVisual> visuals = new List<PooledVisual>();
        private Material sharedMaterial;

        public static CombatFeedbackService ActiveInstance { get; private set; }
        public int CreatedVisualCount => visuals.Count;
        public int MaximumPoolSize => Mathf.Max(1, maximumPoolSize);
        public int ActiveVisualCount { get; private set; }
        public int PlayedEffectCount { get; private set; }
        public int SuppressedEffectCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeService()
        {
            if (FindFirstObjectByType<CombatFeedbackService>() == null)
            {
                new GameObject("Combat Feedback").AddComponent<CombatFeedbackService>();
            }
        }

        private void Awake()
        {
            if (ActiveInstance != null && ActiveInstance != this)
            {
                enabled = false;
                return;
            }

            ActiveInstance = this;
            EnsureMaterial();
            var initialCount = Mathf.Min(Mathf.Max(1, prewarmCount), Mathf.Max(1, maximumPoolSize));
            for (var i = 0; i < initialCount; i++)
            {
                visuals.Add(CreateVisual(i));
            }
        }

        private void OnEnable()
        {
            if (ActiveInstance == this)
            {
                CombatFeedbackEvents.Published += HandleFeedback;
            }
        }

        private void OnDisable()
        {
            CombatFeedbackEvents.Published -= HandleFeedback;
            for (var i = 0; i < visuals.Count; i++)
            {
                Deactivate(visuals[i]);
            }

            ActiveVisualCount = 0;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (sharedMaterial != null)
            {
                Destroy(sharedMaterial);
            }
        }

        private void Update()
        {
            ActiveVisualCount = 0;
            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                if (!visual.Active)
                {
                    continue;
                }

                if (Time.time >= visual.EndTime || !ShouldDisplay(visual.FeedbackEvent))
                {
                    Deactivate(visual);
                    continue;
                }

                ActiveVisualCount++;
                UpdateVisual(visual);
            }
        }

        private void HandleFeedback(CombatFeedbackEvent feedbackEvent)
        {
            if (!ShouldDisplay(feedbackEvent))
            {
                SuppressedEffectCount++;
                return;
            }

            var visual = AcquireVisual();
            ConfigureVisual(visual, feedbackEvent);
            PlayedEffectCount++;
            ActiveVisualCount = CountActiveVisuals();
        }

        private bool ShouldDisplay(CombatFeedbackEvent feedbackEvent)
        {
            var fog = FogOfWarManager.ActiveInstance;
            return fog == null
                || feedbackEvent.SubjectTeam == fog.PlayerTeam
                || fog.IsWorldPositionVisible(feedbackEvent.WorldPosition);
        }

        private PooledVisual AcquireVisual()
        {
            for (var i = 0; i < visuals.Count; i++)
            {
                if (!visuals[i].Active)
                {
                    return visuals[i];
                }
            }

            if (visuals.Count < Mathf.Max(1, maximumPoolSize))
            {
                var visual = CreateVisual(visuals.Count);
                visuals.Add(visual);
                return visual;
            }

            var oldest = visuals[0];
            for (var i = 1; i < visuals.Count; i++)
            {
                if (visuals[i].StartTime < oldest.StartTime)
                {
                    oldest = visuals[i];
                }
            }

            return oldest;
        }

        private PooledVisual CreateVisual(int index)
        {
            var visualObject = new GameObject($"Pooled Combat Feedback {index + 1}");
            visualObject.transform.SetParent(transform, false);
            var line = visualObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = CircleSegmentCount;
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            line.sharedMaterial = sharedMaterial;
            line.sortingLayerName = sortingLayerName;
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return new PooledVisual { Line = line };
        }

        private void ConfigureVisual(PooledVisual visual, CombatFeedbackEvent feedbackEvent)
        {
            visual.FeedbackEvent = feedbackEvent;
            visual.StartTime = Time.time;
            visual.Active = true;
            switch (feedbackEvent.Type)
            {
                case CombatFeedbackType.AttackHit:
                    SetStyle(visual, new Color(1f, 0.85f, 0.25f, 0.9f), attackHitDuration, 0.12f, 0.38f);
                    break;
                case CombatFeedbackType.Damaged:
                    SetStyle(visual, new Color(1f, 0.2f, 0.12f, 0.78f), damagedDuration, 0.16f, 0.5f);
                    break;
                case CombatFeedbackType.Death:
                    SetStyle(visual, new Color(1f, 0.32f, 0.2f, 0.82f), deathDuration, 0.24f, 1.05f);
                    break;
                case CombatFeedbackType.ActiveSkill:
                    SetStyle(visual, new Color(0.2f, 0.95f, 0.82f, 0.86f), activeSkillDuration, 0.28f, 0.9f);
                    break;
            }

            visual.Line.enabled = true;
            UpdateVisual(visual);
        }

        private static void SetStyle(PooledVisual visual, Color color, float duration, float startRadius, float endRadius)
        {
            visual.Color = color;
            visual.EndTime = visual.StartTime + duration;
            visual.StartRadius = startRadius;
            visual.EndRadius = endRadius;
        }

        private static void UpdateVisual(PooledVisual visual)
        {
            var progress = Mathf.InverseLerp(visual.StartTime, visual.EndTime, Time.time);
            var radius = Mathf.Lerp(visual.StartRadius, visual.EndRadius, progress);
            var center = visual.FeedbackEvent.WorldPosition;
            for (var i = 0; i < CircleSegmentCount; i++)
            {
                var angle = i * Mathf.PI * 2f / CircleSegmentCount;
                visual.Line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            var color = visual.Color;
            color.a *= 1f - progress;
            visual.Line.startColor = color;
            visual.Line.endColor = color;
        }

        private static void Deactivate(PooledVisual visual)
        {
            visual.Active = false;
            if (visual.Line != null)
            {
                visual.Line.enabled = false;
            }
        }

        private int CountActiveVisuals()
        {
            var count = 0;
            for (var i = 0; i < visuals.Count; i++)
            {
                if (visuals[i].Active)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureMaterial()
        {
            if (sharedMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                sharedMaterial = new Material(shader) { name = "Combat Feedback Shared Material" };
            }
        }
    }
}
