using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Tilemaps;
using ProjectS.Units;
using ProjectS.Visibility;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class CombatFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator CombatAndSkillActions_PublishFeedbackWithoutChangingDamageRules()
        {
            var events = new List<CombatFeedbackEvent>();
            Action<CombatFeedbackEvent> handler = feedbackEvent => events.Add(feedbackEvent);
            CombatFeedbackEvents.Published += handler;
            var attacker = CreateUnit("Feedback Attacker", Vector3.zero, UnitTeam.Team1, 10f);
            var target = CreateUnit("Feedback Target", Vector3.right, UnitTeam.Team2, 2f);
            var targetStatus = target.GetComponent<PrototypeUnitStatus>();
            var targetHealth = target.GetComponent<UnitHealth>();
            var combat = attacker.GetComponent<UnitCombat>();
            var startingHealth = targetHealth.CurrentHealth;

            InvokePrivate(combat, "ApplyAttack", targetStatus);

            Assert.That(targetHealth.CurrentHealth, Is.EqualTo(startingHealth - 10f).Within(0.001f));
            Assert.That(events.Exists(item => item.Type == CombatFeedbackType.AttackHit), Is.True);
            Assert.That(events.Exists(item => item.Type == CombatFeedbackType.Damaged), Is.True);

            var skillController = attacker.AddComponent<UnitActiveSkillController>();
            var skill = new UnitActiveSkillDefinition();
            skill.Configure("Feedback Skill", UnitActiveSkillEffectType.SelfMovementSpeedMultiplier, 1f, 0.1f, 1.2f);
            skillController.Configure(new[] { skill });
            Assert.That(skillController.TryActivate(0), Is.True);
            Assert.That(events.Exists(item => item.Type == CombatFeedbackType.ActiveSkill), Is.True);

            targetStatus.TakeDamage(1000f, attacker.GetComponent<PrototypeUnitStatus>());
            Assert.That(events.Exists(item => item.Type == CombatFeedbackType.Death), Is.True);

            CombatFeedbackEvents.Published -= handler;
            Object.Destroy(attacker);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FeedbackService_UsesBoundedPoolAndSuppressesHiddenEnemyEffects()
        {
            var worldRoot = CreateWorld(8, out var groundTile);
            var world = worldRoot.GetComponent<ProjectSTilemapWorld>();
            var provider = CreateUnit("Feedback Vision Provider", new Vector3(0.5f, 0.5f), UnitTeam.Team1, 0f);
            provider.GetComponent<PrototypeUnitStatus>().ConfigureVisionRadius(1.1f);
            var fog = FogOfWarManager.ActiveInstance != null
                ? FogOfWarManager.ActiveInstance
                : new GameObject("Feedback Fog").AddComponent<FogOfWarManager>();
            fog.Configure(world, UnitTeam.Team1);
            var service = CombatFeedbackService.ActiveInstance != null
                ? CombatFeedbackService.ActiveInstance
                : new GameObject("Feedback Service").AddComponent<CombatFeedbackService>();
            var playedBefore = service.PlayedEffectCount;
            var suppressedBefore = service.SuppressedEffectCount;

            CombatFeedbackEvents.Publish(
                CombatFeedbackType.Damaged,
                new Vector3(6.5f, 0.5f),
                UnitTeam.Team2);
            Assert.That(service.SuppressedEffectCount, Is.EqualTo(suppressedBefore + 1));
            Assert.That(service.PlayedEffectCount, Is.EqualTo(playedBefore));

            CombatFeedbackEvents.Publish(
                CombatFeedbackType.Damaged,
                new Vector3(6.5f, 0.5f),
                UnitTeam.Team1);
            for (var i = 0; i < service.MaximumPoolSize + 12; i++)
            {
                CombatFeedbackEvents.Publish(
                    CombatFeedbackType.AttackHit,
                    new Vector3(0.5f, 0.5f),
                    UnitTeam.Team1);
            }

            Assert.That(service.CreatedVisualCount, Is.LessThanOrEqualTo(service.MaximumPoolSize));
            Assert.That(service.ActiveVisualCount, Is.LessThanOrEqualTo(service.MaximumPoolSize));
            Assert.That(service.PlayedEffectCount, Is.EqualTo(playedBefore + service.MaximumPoolSize + 13));

            Object.Destroy(provider);
            Object.Destroy(worldRoot);
            Object.Destroy(groundTile);
            yield return null;
        }

        private static GameObject CreateWorld(int width, out Tile groundTile)
        {
            var root = new GameObject("Feedback Test Grid");
            root.AddComponent<Grid>();
            var tilemapObject = new GameObject("Ground");
            tilemapObject.transform.SetParent(root.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            groundTile = ScriptableObject.CreateInstance<Tile>();
            for (var x = 0; x < width; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0), groundTile);
            }

            var world = root.AddComponent<ProjectSTilemapWorld>();
            world.RebuildNavigationCache();
            return root;
        }

        private static GameObject CreateUnit(
            string name,
            Vector3 position,
            UnitTeam team,
            float attackPower)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                PrototypeUnitType.Soldier,
                MovementDomain.Ground,
                UnitRole.Combat,
                AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                100f,
                attackPower,
                0f,
                1.5f,
                5f,
                1f,
                3f,
                1,
                Vector2Int.one,
                false,
                false,
                0f);
            return unit;
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method was not found: {methodName}");
            return method.Invoke(target, arguments);
        }
    }
}
