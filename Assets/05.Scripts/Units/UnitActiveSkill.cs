using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectS.Units
{
    public enum UnitActiveSkillEffectType
    {
        SelfMovementSpeedMultiplier
    }

    [Serializable]
    public sealed class UnitActiveSkillDefinition
    {
        [SerializeField] private string displayName = "Active Skill";
        [SerializeField] private UnitActiveSkillEffectType effectType;
        [SerializeField, Min(0.1f)] private float cooldown = 10f;
        [SerializeField, Min(0.1f)] private float duration = 3f;
        [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1.25f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? effectType.ToString() : displayName;
        public UnitActiveSkillEffectType EffectType => effectType;
        public float Cooldown => Mathf.Max(0.1f, cooldown);
        public float Duration => Mathf.Max(0.1f, duration);
        public float MovementSpeedMultiplier => Mathf.Max(1f, movementSpeedMultiplier);

        public void Configure(
            string skillName,
            UnitActiveSkillEffectType skillEffectType,
            float cooldownSeconds,
            float durationSeconds,
            float speedMultiplier)
        {
            displayName = skillName;
            effectType = skillEffectType;
            cooldown = Mathf.Max(0.1f, cooldownSeconds);
            duration = Mathf.Max(0.1f, durationSeconds);
            movementSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitActiveSkillController : MonoBehaviour
    {
        private sealed class SkillRuntimeState
        {
            public float CooldownEndsAt;
            public float EffectEndsAt;
            public bool EffectActive;
        }

        [SerializeField] private UnitActiveSkillDefinition[] skills = new UnitActiveSkillDefinition[0];

        private PrototypeUnitStatus status;
        private SkillRuntimeState[] runtimeStates = new SkillRuntimeState[0];

        public IReadOnlyList<UnitActiveSkillDefinition> Skills => skills;
        public string LastFailureReason { get; private set; }

        private void Awake()
        {
            ResolveStatus();
            EnsureRuntimeStates();
        }

        private void Update()
        {
            EnsureRuntimeStates();
            for (var i = 0; i < runtimeStates.Length; i++)
            {
                var state = runtimeStates[i];
                if (state.EffectActive && Time.time >= state.EffectEndsAt)
                {
                    RemoveEffect(state);
                }
            }
        }

        private void OnDisable()
        {
            ClearActiveEffects();
        }

        public void Configure(UnitActiveSkillDefinition[] definitions)
        {
            ClearActiveEffects();
            skills = definitions ?? new UnitActiveSkillDefinition[0];
            runtimeStates = new SkillRuntimeState[0];
            LastFailureReason = string.Empty;
            EnsureRuntimeStates();
        }

        public bool TryActivate(int skillIndex)
        {
            ResolveStatus();
            EnsureRuntimeStates();
            if (skillIndex < 0 || skillIndex >= skills.Length || skills[skillIndex] == null)
            {
                return Fail($"No active skill is assigned to slot {skillIndex + 1}.");
            }

            if (!isActiveAndEnabled || status == null || !status.IsAlive)
            {
                return Fail($"{skills[skillIndex].DisplayName} cannot be used by an inactive or defeated unit.");
            }

            var definition = skills[skillIndex];
            var state = runtimeStates[skillIndex];
            var cooldownRemaining = Mathf.Max(0f, state.CooldownEndsAt - Time.time);
            if (cooldownRemaining > 0f)
            {
                return Fail($"{definition.DisplayName} is on cooldown ({cooldownRemaining:0.0}s remaining).");
            }

            if (!ApplyEffect(definition, state))
            {
                return Fail($"{definition.DisplayName} has an unsupported effect.");
            }

            state.CooldownEndsAt = Time.time + definition.Cooldown;
            state.EffectEndsAt = Time.time + definition.Duration;
            state.EffectActive = true;
            CombatFeedbackEvents.Publish(
                CombatFeedbackType.ActiveSkill,
                transform.position,
                status.Team,
                status.Team);
            LastFailureReason = string.Empty;
            return true;
        }

        public float GetCooldownRemaining(int skillIndex)
        {
            EnsureRuntimeStates();
            return skillIndex >= 0 && skillIndex < runtimeStates.Length
                ? Mathf.Max(0f, runtimeStates[skillIndex].CooldownEndsAt - Time.time)
                : 0f;
        }

        public float GetActiveDurationRemaining(int skillIndex)
        {
            EnsureRuntimeStates();
            return skillIndex >= 0
                && skillIndex < runtimeStates.Length
                && runtimeStates[skillIndex].EffectActive
                    ? Mathf.Max(0f, runtimeStates[skillIndex].EffectEndsAt - Time.time)
                    : 0f;
        }

        private bool ApplyEffect(UnitActiveSkillDefinition definition, SkillRuntimeState state)
        {
            switch (definition.EffectType)
            {
                case UnitActiveSkillEffectType.SelfMovementSpeedMultiplier:
                    status.SetMovementSpeedModifier(state, definition.MovementSpeedMultiplier);
                    return true;
                default:
                    return false;
            }
        }

        private void RemoveEffect(SkillRuntimeState state)
        {
            status?.RemoveMovementSpeedModifier(state);
            state.EffectActive = false;
            state.EffectEndsAt = 0f;
        }

        private void ClearActiveEffects()
        {
            for (var i = 0; i < runtimeStates.Length; i++)
            {
                if (runtimeStates[i].EffectActive)
                {
                    RemoveEffect(runtimeStates[i]);
                }
            }
        }

        private void ResolveStatus()
        {
            if (status == null)
            {
                status = GetComponent<PrototypeUnitStatus>();
            }
        }

        private void EnsureRuntimeStates()
        {
            if (skills == null)
            {
                skills = new UnitActiveSkillDefinition[0];
            }

            if (runtimeStates.Length == skills.Length)
            {
                return;
            }

            runtimeStates = new SkillRuntimeState[skills.Length];
            for (var i = 0; i < runtimeStates.Length; i++)
            {
                runtimeStates[i] = new SkillRuntimeState();
            }
        }

        private bool Fail(string reason)
        {
            LastFailureReason = reason;
            return false;
        }
    }
}
