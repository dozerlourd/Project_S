using System;
using System.Collections;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class MatchOutcomePlayModeTests
    {
        private const float MovementSpeed = 3f;
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type BuildingHealthType = GetGameplayType("ProjectS.Buildings.BuildingHealth");
        private static readonly Type UnitProductionQueueType = GetGameplayType("ProjectS.Buildings.UnitProductionQueue");
        private static readonly Type SimpleSkirmishAIType = GetGameplayType("ProjectS.AI.SimpleSkirmishAI");
        private static readonly Type RtsMatchControllerType = GetGameplayType("ProjectS.RtsMatchController");

        [UnityTest]
        public IEnumerator UnitCombat_FocusAttackDamagesEnemyMainBase()
        {
            var attacker = CreateCombatUnit("BuildingAttacker", Vector3.zero, UnitTeam.Team1);
            var enemyBase = CreateBuilding("EnemyAttackableBase", UnitTeam.Team2, "MainBase", new Vector3(0.75f, 0f, 0f));
            var target = (IUnitAttackTarget)enemyBase.GetComponent(BuildingStatusType);
            var health = enemyBase.GetComponent(BuildingHealthType);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var maxHealth = GetFloat(health, "MaxHealth");

            commandAgent.Issue(new UnitCommand(UnitCommandMode.FocusAttack, target.SelectionTransform.position, target, false));

            for (var i = 0; i < 20 && GetFloat(health, "CurrentHealth") >= maxHealth; i++)
            {
                yield return null;
            }

            Assert.That(GetFloat(health, "CurrentHealth"), Is.LessThan(maxHealth));

            Object.Destroy(attacker);
            Object.Destroy(enemyBase);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyBuildingsDestroyed_EndsWithVictoryOnceAndStopsAiActivity()
        {
            var controllerObject = CreateMatchController(out var controller);
            var playerBase = CreateBuilding("VictoryPlayerBase", UnitTeam.Team1, "MainBase", Vector3.left);
            var enemyBase = CreateBuilding("VictoryEnemyBase", UnitTeam.Team2, "MainBase", Vector3.right);
            var playerUnit = CreateCombatUnit("VictoryPlayerUnit", Vector3.left + Vector3.down, UnitTeam.Team1);
            var enemyUnit = CreateCombatUnit("VictoryEnemyUnit", Vector3.right + Vector3.up, UnitTeam.Team2);
            var aiObject = new GameObject("VictoryAI");
            var ai = (Behaviour)aiObject.AddComponent(SimpleSkirmishAIType);
            var productionBuilding = CreateBuilding("VictoryProduction", UnitTeam.Team2, "Production", new Vector3(2f, 0f, 0f));
            var productionQueue = (Behaviour)productionBuilding.AddComponent(UnitProductionQueueType);

            yield return null;
            Invoke(controller, "ForceEvaluate");
            Assert.That(GetProperty(controller, "Result").ToString(), Is.EqualTo("InProgress"));

            var enemyBaseTarget = (IUnitAttackTarget)enemyBase.GetComponent(BuildingStatusType);
            var productionTarget = (IUnitAttackTarget)productionBuilding.GetComponent(BuildingStatusType);
            enemyBaseTarget.TakeDamage(10000f);
            productionTarget.TakeDamage(10000f);

            yield return null;
            Invoke(controller, "ForceEvaluate");
            Invoke(controller, "ForceEvaluate");

            Assert.That(GetProperty(controller, "Result").ToString(), Is.EqualTo("Victory"));
            Assert.That(GetProperty(controller, "EndReason").ToString(), Is.EqualTo("EnemyBuildingsDestroyed"));
            Assert.That(GetInt(controller, "ResolutionCount"), Is.EqualTo(1));
            Assert.That(ai.enabled, Is.False);
            Assert.That(productionQueue.enabled, Is.False);

            Object.Destroy(controllerObject);
            Object.Destroy(playerBase);
            Object.Destroy(enemyBase);
            Object.Destroy(playerUnit);
            Object.Destroy(enemyUnit);
            Object.Destroy(aiObject);
            Object.Destroy(productionBuilding);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyUnitsEliminated_DoesNotEndWhileEnemyBuildingsRemain()
        {
            var controllerObject = CreateMatchController(out var controller);
            var playerBase = CreateBuilding("EliminatePlayerBase", UnitTeam.Team1, "MainBase", Vector3.left);
            var enemyBase = CreateBuilding("EliminateEnemyBase", UnitTeam.Team2, "MainBase", Vector3.right);
            var playerUnit = CreateCombatUnit("EliminatePlayerUnit", Vector3.left + Vector3.down, UnitTeam.Team1);
            var enemyUnit = CreateCombatUnit("EliminateEnemyUnit", Vector3.right + Vector3.up, UnitTeam.Team2);
            var enemyTarget = enemyUnit.GetComponent<PrototypeUnitStatus>() as IUnitAttackTarget;

            yield return null;
            enemyTarget.TakeDamage(10000f);

            yield return null;
            Invoke(controller, "ForceEvaluate");

            Assert.That(GetProperty(controller, "Result").ToString(), Is.EqualTo("InProgress"));
            Assert.That(GetProperty(controller, "EndReason").ToString(), Is.EqualTo("None"));

            Object.Destroy(controllerObject);
            Object.Destroy(playerBase);
            Object.Destroy(enemyBase);
            Object.Destroy(playerUnit);
            Object.Destroy(enemyUnit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerBuildingsDestroyed_EndsWithDefeat()
        {
            var controllerObject = CreateMatchController(out var controller);
            var playerBase = CreateBuilding("DefeatPlayerBase", UnitTeam.Team1, "MainBase", Vector3.left);
            var enemyBase = CreateBuilding("DefeatEnemyBase", UnitTeam.Team2, "MainBase", Vector3.right);
            var playerUnit = CreateCombatUnit("DefeatPlayerUnit", Vector3.left + Vector3.down, UnitTeam.Team1);
            var enemyUnit = CreateCombatUnit("DefeatEnemyUnit", Vector3.right + Vector3.up, UnitTeam.Team2);
            var target = (IUnitAttackTarget)playerBase.GetComponent(BuildingStatusType);

            yield return null;
            target.TakeDamage(10000f);

            yield return null;
            Invoke(controller, "ForceEvaluate");

            Assert.That(GetProperty(controller, "Result").ToString(), Is.EqualTo("Defeat"));
            Assert.That(GetProperty(controller, "EndReason").ToString(), Is.EqualTo("PlayerBuildingsDestroyed"));

            Object.Destroy(controllerObject);
            Object.Destroy(playerBase);
            Object.Destroy(enemyBase);
            Object.Destroy(playerUnit);
            Object.Destroy(enemyUnit);
            yield return null;
        }

        private static GameObject CreateMatchController(out Component controller)
        {
            var controllerObject = new GameObject("MatchController");
            controller = controllerObject.AddComponent(RtsMatchControllerType);
            Invoke(controller, "Configure", UnitTeam.Team1, UnitTeam.Team2);
            return controllerObject;
        }

        private static GameObject CreateBuilding(string name, UnitTeam team, string buildingKind, Vector3 position)
        {
            var building = new GameObject(name);
            building.transform.position = position;
            var collider = building.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2f, 2f);
            collider.isTrigger = true;

            var status = building.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", team, Enum.Parse(BuildingKindType, buildingKind), Vector2Int.one, true);
            return building;
        }

        private static GameObject CreateCombatUnit(string name, Vector3 position, UnitTeam team)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            var collider = unit.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.75f, 0.75f);
            collider.isTrigger = true;

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
                75f,
                0f,
                2f,
                6f,
                8f,
                MovementSpeed,
                1,
                Vector2Int.one,
                false,
                false,
                0f);

            if (unit.GetComponent<UnitPathAgent>() == null)
            {
                unit.AddComponent<UnitPathAgent>();
            }

            return unit;
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.That(type, Is.Not.Null, $"Could not resolve gameplay type {typeName}.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = FindMethod(target.GetType(), methodName, arguments);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static System.Reflection.MethodInfo FindMethod(Type targetType, string methodName, object[] arguments)
        {
            var methods = targetType.GetMethods();
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != arguments.Length)
                {
                    continue;
                }

                var matches = true;
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    var argument = arguments[parameterIndex];
                    if (argument == null)
                    {
                        continue;
                    }

                    if (!parameters[parameterIndex].ParameterType.IsInstanceOfType(argument))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method;
                }
            }

            return null;
        }

        private static int GetInt(object target, string propertyName)
        {
            return (int)GetProperty(target, propertyName);
        }

        private static float GetFloat(object target, string propertyName)
        {
            return (float)GetProperty(target, propertyName);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not resolve property {target.GetType().Name}.{propertyName}.");
            return property.GetValue(target);
        }
    }
}
