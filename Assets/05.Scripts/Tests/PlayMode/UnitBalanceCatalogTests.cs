using System;
using System.Collections;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitBalanceCatalogTests
    {
        [Test]
        public void FallbackCatalog_AppliesUnitStatusDefaults()
        {
            var unit = new GameObject("FallbackTankStatus");
            var status = unit.AddComponent<PrototypeUnitStatus>();

            status.ConfigurePrototypeDefaults(PrototypeUnitType.Tank);

            Assert.That(status.MaxHealth, Is.EqualTo(260f));
            Assert.That(status.PhysicalAttackPower, Is.EqualTo(26f));
            Assert.That(status.AttackRange, Is.EqualTo(5.5f));
            Assert.That(status.MovementSpeed, Is.EqualTo(2f));
            Assert.That(status.SupplyCost, Is.EqualTo(3));
            Assert.That(status.AdvancedSupplyCost, Is.EqualTo(0));

            Object.DestroyImmediate(unit);
        }

        [Test]
        public void FallbackCatalog_AppliesProductionDefinitionDefaults()
        {
            var productionDefinitionType = GetGameplayType("ProjectS.Buildings.UnitProductionDefinition");
            var resourceAmountType = GetGameplayType("ProjectS.Resources.ResourceAmount");
            var prefab = new GameObject("RangerPrefab");
            var definition = Activator.CreateInstance(productionDefinitionType);
            Invoke(
                definition,
                "Configure",
                "Old Ranger",
                PrototypeUnitType.Ranger,
                prefab,
                Activator.CreateInstance(resourceAmountType, 1, 1),
                1f,
                1,
                1,
                null);

            Invoke(definition, "ApplyBalanceCatalog", null);

            var cost = GetProperty(definition, "Cost");
            Assert.That(GetProperty(definition, "DisplayName"), Is.EqualTo("Ranger"));
            Assert.That(GetProperty(cost, "Minerals"), Is.EqualTo(100));
            Assert.That(GetProperty(cost, "Gas"), Is.EqualTo(25));
            Assert.That(GetProperty(definition, "ProductionTime"), Is.EqualTo(8f));
            Assert.That(GetProperty(definition, "SupplyCost"), Is.EqualTo(2));
            Assert.That(GetProperty(definition, "AdvancedSupplyCost"), Is.EqualTo(0));
            Assert.That(ContainsEnumName(GetProperty(definition, "AllowedProductionBuildings"), "Production"), Is.True);

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CsvCatalog_AppliesStatusAndProductionValues()
        {
            var csv =
                "unitType,displayName,maxHealth,physicalAttackPower,attackRange,detectionRange,attackSpeed,movementSpeed,standardSupplyCost,advancedSupplyCost,minerals,gas,productionTime,areaDamageRadius,maxAreaTargets,allowedProductionBuildings,unlockRequirements\n"
                + "Scout,Sheet Scout,66,7,4.75,7.5,1.4,5.1,0,1,80,15,4.5,1.25,2,SignalRelay,field-engineering";
            var entry = UnitBalanceCsvParser.Parse(csv)[0];
            var unit = new GameObject("CsvScoutStatus");
            var status = unit.AddComponent<PrototypeUnitStatus>();
            var prefab = new GameObject("CsvScoutPrefab");
            var productionDefinitionType = GetGameplayType("ProjectS.Buildings.UnitProductionDefinition");
            var resourceAmountType = GetGameplayType("ProjectS.Resources.ResourceAmount");
            var definition = Activator.CreateInstance(productionDefinitionType);
            var catalog = ScriptableObject.CreateInstance<UnitBalanceCatalog>();
            catalog.Configure(null, new[] { entry });

            entry.ApplyTo(status, UnitTeam.Team3);
            Invoke(
                definition,
                "Configure",
                "Old Scout",
                PrototypeUnitType.Scout,
                prefab,
                Activator.CreateInstance(resourceAmountType, 1, 0),
                1f,
                1,
                1,
                null);
            Invoke(definition, "ApplyBalanceCatalog", catalog);

            Assert.That(status.UnitType, Is.EqualTo(PrototypeUnitType.Scout));
            Assert.That(status.Team, Is.EqualTo(UnitTeam.Team3));
            Assert.That(status.MaxHealth, Is.EqualTo(66f));
            Assert.That(status.AttackRange, Is.EqualTo(4.75f));
            Assert.That(status.MovementSpeed, Is.EqualTo(5.1f));
            Assert.That(status.SupplyCost, Is.EqualTo(0));
            Assert.That(status.AdvancedSupplyCost, Is.EqualTo(1));
            Assert.That(status.AreaDamageRadius, Is.EqualTo(1.25f));
            Assert.That(status.MaxAreaTargets, Is.EqualTo(2));
            var cost = GetProperty(definition, "Cost");
            Assert.That(GetProperty(definition, "DisplayName"), Is.EqualTo("Sheet Scout"));
            Assert.That(GetProperty(cost, "Minerals"), Is.EqualTo(80));
            Assert.That(GetProperty(cost, "Gas"), Is.EqualTo(15));
            Assert.That(GetProperty(definition, "ProductionTime"), Is.EqualTo(4.5f));
            Assert.That(ContainsEnumName(GetProperty(definition, "AllowedProductionBuildings"), "SignalRelay"), Is.True);
            Assert.That(CountItems(GetProperty(definition, "UnlockRequirements")), Is.EqualTo(1));

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(unit);
            Object.DestroyImmediate(prefab);
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
            var methods = target.GetType().GetMethods();
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName || method.GetParameters().Length != arguments.Length)
                {
                    continue;
                }

                return method.Invoke(target, arguments);
            }

            Assert.Fail($"Could not resolve method {target.GetType().Name}.{methodName}.");
            return null;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not resolve property {target.GetType().Name}.{propertyName}.");
            return property.GetValue(target);
        }

        private static bool ContainsEnumName(object values, string expectedName)
        {
            foreach (var value in (IEnumerable)values)
            {
                if (value != null && value.ToString() == expectedName)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountItems(object values)
        {
            var count = 0;
            foreach (var _ in (IEnumerable)values)
            {
                count++;
            }

            return count;
        }
    }
}
