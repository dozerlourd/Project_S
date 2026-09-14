using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitActiveSkillPlayModeTests
    {
        private static readonly Type RtsGameHudType = GetGameplayType("ProjectS.UI.RtsGameHud");

        [UnityTest]
        public IEnumerator SelfBuff_PreservesLatestCommandAndRestoresMovementSpeedAfterDuration()
        {
            var unit = CreateStriker("Active Skill Striker");
            var status = unit.GetComponent<PrototypeUnitStatus>();
            var commandAgent = unit.GetComponent<UnitCommandAgent>();
            var skillController = unit.AddComponent<UnitActiveSkillController>();
            skillController.Configure(new[] { CreateOverdrive(0.2f, 0.05f, 1.5f) });
            commandAgent.HoldPosition();
            var commandId = commandAgent.LatestCommandId;
            var commandMode = commandAgent.Mode;
            var actionState = commandAgent.ActionState;
            var baseMovementSpeed = status.MovementSpeed;

            Assert.That(skillController.TryActivate(0), Is.True);
            Assert.That(status.MovementSpeed, Is.EqualTo(baseMovementSpeed * 1.5f).Within(0.001f));
            Assert.That(commandAgent.LatestCommandId, Is.EqualTo(commandId));
            Assert.That(commandAgent.Mode, Is.EqualTo(commandMode));
            Assert.That(commandAgent.ActionState, Is.EqualTo(actionState));
            Assert.That(skillController.TryActivate(0), Is.False);
            Assert.That(skillController.LastFailureReason, Does.Contain("cooldown"));

            yield return new WaitForSeconds(0.08f);

            Assert.That(status.MovementSpeed, Is.EqualTo(baseMovementSpeed).Within(0.001f));
            Assert.That(skillController.GetCooldownRemaining(0), Is.GreaterThan(0f));
            Assert.That(commandAgent.LatestCommandId, Is.EqualTo(commandId));

            Object.Destroy(unit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudActivation_ExecutesSelectedUnitSkillAndReportsCooldownFailure()
        {
            var unit = CreateStriker("HUD Skill Striker");
            var status = unit.GetComponent<PrototypeUnitStatus>();
            var commandAgent = unit.GetComponent<UnitCommandAgent>();
            var skillController = unit.AddComponent<UnitActiveSkillController>();
            skillController.Configure(new[] { CreateOverdrive(0.25f, 0.1f, 1.4f) });
            commandAgent.HoldPosition();
            var commandId = commandAgent.LatestCommandId;
            var baseMovementSpeed = status.MovementSpeed;

            var commandObject = new GameObject("Skill Command Controller");
            var commandController = commandObject.AddComponent<PlayerUnitCommandController>();
            Invoke(commandController, "AddSelection", commandAgent);
            var hudObject = new GameObject("Skill HUD");
            var hud = hudObject.AddComponent(RtsGameHudType);

            Assert.That((bool)Invoke(hud, "TryActivateSelectedSkill", 0), Is.True);
            Assert.That(status.MovementSpeed, Is.EqualTo(baseMovementSpeed * 1.4f).Within(0.001f));
            Assert.That(commandAgent.LatestCommandId, Is.EqualTo(commandId));
            Assert.That((bool)Invoke(hud, "TryActivateSelectedSkill", 0), Is.False);
            Assert.That(GetField<string>(hud, "skillFeedback"), Does.Contain("cooldown"));

            Object.Destroy(hudObject);
            Object.Destroy(commandObject);
            Object.Destroy(unit);
            yield return null;
        }

        private static UnitActiveSkillDefinition CreateOverdrive(
            float cooldown,
            float duration,
            float multiplier)
        {
            var definition = new UnitActiveSkillDefinition();
            definition.Configure(
                "Overdrive",
                UnitActiveSkillEffectType.SelfMovementSpeedMultiplier,
                cooldown,
                duration,
                multiplier);
            return definition;
        }

        private static GameObject CreateStriker(string name)
        {
            var unit = new GameObject(name);
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                UnitTeam.Team1,
                PrototypeUnitType.Striker,
                MovementDomain.Ground,
                UnitRole.Combat,
                AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                55f,
                6f,
                0f,
                0.8f,
                4f,
                3f,
                4.2f,
                1,
                Vector2Int.one,
                false,
                false,
                0f);
            return unit;
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Gameplay type was not found: {typeName}");
            return type;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field was not found: {fieldName}");
            return (T)field.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != methodName)
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();
                    if (parameters.Length != arguments.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] != null && !parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                });
            Assert.That(method, Is.Not.Null, $"Method was not found: {methodName}");
            return method.Invoke(target, arguments);
        }
    }
}
