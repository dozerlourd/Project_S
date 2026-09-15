using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Units;
using ProjectS.Visibility;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class StructurePlayModeTests
    {
        private const UnitTeam FirstTeam = UnitTeam.Team7;
        private const UnitTeam SecondTeam = UnitTeam.Team8;

        private static readonly string[] Kinds =
        {
            "MainBase", "Production", "ResourceDropOff", "SupplyDepot",
            "SpliterProduction", "AutoTurret", "SpeedAura", "Other",
            "ResearchLab", "DefenseControlCenter", "VehicleFactory", "SignalRelay",
            "TacticalCommandCenter", "MaintenanceBay", "ForwardSupplyPost"
        };

        [Test]
        public void Structure_IsAbstract_AndBuildingStatusPreservesLegacyContract()
        {
            var structureType = BuildingType("Structure");
            var statusType = BuildingType("BuildingStatus");
            Assert.That(structureType.IsAbstract, Is.True);
            Assert.That(statusType.IsAbstract, Is.False);
            Assert.That(statusType.BaseType, Is.EqualTo(structureType));
            Assert.That(typeof(IUnitAttackTarget).IsAssignableFrom(statusType), Is.True);
            Assert.That(typeof(IFogVisionProvider).IsAssignableFrom(statusType), Is.True);
            Assert.That(typeof(IAttackTargetPriorityProvider).IsAssignableFrom(statusType), Is.True);

            var registryMethod = BuildingType("BuildingRegistry").GetMethod("GetBuildings");
            Assert.That(registryMethod, Is.Not.Null);
            Assert.That(registryMethod.ReturnType,
                Is.EqualTo(typeof(IReadOnlyList<>).MakeGenericType(statusType)));
        }

        [UnityTest]
        public IEnumerator RuntimeBuildingCatalog_UsesCorePrefabsWithDistinctPersistentSprites()
        {
            var catalog = Resources.Load<ScriptableObject>("Buildings/BuildingPrefabCatalog");
            Assert.That(catalog, Is.Not.Null);
            var sprites = new HashSet<Sprite>();
            var instances = new List<GameObject>();

            try
            {
                foreach (var kind in new[] { "MainBase", "Production", "SpliterProduction", "AutoTurret", "SpeedAura" })
                {
                    var prefab = (GameObject)Invoke(catalog, "GetPrefab", ParseKind(kind));
                    Assert.That(prefab, Is.Not.Null, kind + " catalog prefab");
                    var status = prefab.GetComponent(BuildingType("BuildingStatus"));
                    Assert.That(status, Is.Not.Null);
                    Assert.That(Property(status, "Kind").ToString(), Is.EqualTo(kind));

                    var expectedSprite = prefab.GetComponent<SpriteRenderer>()?.sprite;
                    Assert.That(expectedSprite, Is.Not.Null, kind + " sprite");
                    Assert.That(sprites.Add(expectedSprite), Is.True, kind + " must use a unique Sprite asset");

                    var instance = Object.Instantiate(prefab);
                    instances.Add(instance);
                    yield return null;
                    Assert.That(instance.GetComponent<SpriteRenderer>()?.sprite, Is.SameAs(expectedSprite),
                        kind + " runtime visual must preserve the prefab Sprite");
                }

                var constructionPrefab = (GameObject)Property(catalog, "ConstructionSitePrefab");
                Assert.That(constructionPrefab, Is.Not.Null);
                var constructionSprite = constructionPrefab.GetComponent<SpriteRenderer>()?.sprite;
                Assert.That(constructionSprite, Is.Not.Null);
                Assert.That(sprites.Add(constructionSprite), Is.True,
                    "ConstructionSite must use a Sprite distinct from completed buildings");
                Assert.That(catalog.GetType().GetMethod("GetPrefab")?.Invoke(catalog, new[] { ParseKind("SupplyDepot") }), Is.Null);
                Assert.That(catalog.GetType().GetMethod("GetPrefab")?.Invoke(catalog, new[] { ParseKind("ResourceDropOff") }), Is.Null);
            }
            finally
            {
                for (var i = instances.Count - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(instances[i]);
                }
            }
        }

        [TestCase("VehicleFactory", 3, 3, 1250f)]
        [TestCase("MaintenanceBay", 3, 2, 850f)]
        [TestCase("SignalRelay", 2, 2, 550f)]
        public void SpecializedProductionCatalog_PrefabsExposeDedicatedEmptyQueues(
            string kind,
            int footprintWidth,
            int footprintHeight,
            float maxHealth)
        {
            var catalog = Resources.Load<ScriptableObject>("Buildings/BuildingPrefabCatalog");
            Assert.That(catalog, Is.Not.Null);
            var prefab = (GameObject)Invoke(catalog, "GetPrefab", ParseKind(kind));
            Assert.That(prefab, Is.Not.Null, kind + " catalog prefab");

            var status = prefab.GetComponent(BuildingType("BuildingStatus"));
            Assert.That(status, Is.Not.Null);
            Assert.That(status.GetType(), Is.EqualTo(BuildingType(kind + "Structure")));
            Assert.That(Property(status, "Kind").ToString(), Is.EqualTo(kind));
            Assert.That(Property(status, "Footprint"), Is.EqualTo(new Vector2Int(footprintWidth, footprintHeight)));

            var queue = prefab.GetComponent(BuildingType("UnitProductionQueue"));
            Assert.That(queue, Is.Not.Null);
            Assert.That((IEnumerable)Property(queue, "ProducibleUnits"), Is.Empty,
                kind + " must not expose a production entry before its unit prefab exists");
            Assert.That(prefab.GetComponents(BuildingType("UnitProductionQueue")), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<BoxCollider2D>(), Is.Not.Null);

            var health = prefab.GetComponent(BuildingType("BuildingHealth"));
            Assert.That(health, Is.Not.Null);
            Assert.That(Property(health, "MaxHealth"), Is.EqualTo(maxHealth));
        }

        [TestCaseSource(nameof(Kinds))]
        public void Factory_AllKinds_PreserveLegacyLookupsAndRoleComponents(string kind)
        {
            var root = new GameObject("Structure factory " + kind);
            try
            {
                var structure = AddStructure(root, kind);
                var expectedType = BuildingType(kind == "Other" ? "BuildingStatus" : kind + "Structure");
                Assert.That(structure.GetType(), Is.EqualTo(expectedType));
                if (kind != "Other")
                {
                    Assert.That(expectedType.BaseType, Is.EqualTo(BuildingType("BuildingStatus")));
                }

                Assert.That(root.GetComponent(BuildingType("BuildingStatus")), Is.SameAs(structure));
                Assert.That(root.GetComponent(BuildingType("Structure")), Is.SameAs(structure));
                Assert.That(structure, Is.InstanceOf<IUnitAttackTarget>());
                Assert.That(structure, Is.InstanceOf<IFogVisionProvider>());
                Assert.That(Property(structure, "Kind").ToString(), Is.EqualTo(kind));
                Assert.That(Property(structure, "Completed"), Is.EqualTo(true));
                Assert.That(Property(structure, "IsAlive"), Is.EqualTo(true));
                Assert.That((Vector2Int)Property(structure, "Footprint"),
                    Is.EqualTo(DefaultFootprint(kind)));
                Assert.That(Property(structure, "VisionRadius"), Is.EqualTo(kind == "SignalRelay" ? 12f : 9f));
                Assert.That(Property(structure, "SupplyProvided"),
                    Is.EqualTo(kind == "SupplyDepot" ? 10 : kind == "ForwardSupplyPost" ? 5 : 0));

                var health = root.GetComponent(BuildingType("BuildingHealth"));
                Assert.That(health, Is.Not.Null);
                Assert.That((float)Property(health, "MaxHealth"), Is.GreaterThan(0f));
                Assert.That(Property(health, "CurrentHealth"), Is.EqualTo(Property(health, "MaxHealth")));
                AssertSingleComponents(root, kind);
                AssertRegistrations(structure, UnitTeam.Team1, 1);

                var footprint = new Vector2Int(4, 1);
                Initialize(structure, FirstTeam, kind, footprint, true);
                Invoke(structure, "ConfigureVisionRadius", 4.5f);
                Invoke(structure, "ConfigureSupplyProvided", 2);
                Invoke(health, "ConfigureMaxHealth", 1234f);
                ((IUnitAttackTarget)structure).TakeDamage(34f);

                Assert.That(AddStructure(root, kind), Is.SameAs(structure));
                root.SetActive(false);
                root.SetActive(true);
                Assert.That(AddStructure(root, kind), Is.SameAs(structure));
                Assert.That(Property(structure, "Team"), Is.EqualTo(FirstTeam));
                Assert.That(Property(structure, "Footprint"), Is.EqualTo(footprint));
                Assert.That(Property(structure, "VisionRadius"), Is.EqualTo(4.5f));
                Assert.That(Property(structure, "SupplyProvided"), Is.EqualTo(2));
                Assert.That(Property(health, "MaxHealth"), Is.EqualTo(1234f));
                Assert.That(Property(health, "CurrentHealth"), Is.EqualTo(1200f));
                AssertSingleComponents(root, kind);
                AssertRegistrations(structure, FirstTeam, 1);
                AssertTeamMembership(structure, UnitTeam.Team1, 0);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("ResearchLab", 800f, 2, 2)]
        [TestCase("DefenseControlCenter", 950f, 2, 2)]
        [TestCase("VehicleFactory", 1250f, 3, 3)]
        [TestCase("SignalRelay", 550f, 2, 2)]
        [TestCase("TacticalCommandCenter", 1050f, 3, 2)]
        [TestCase("MaintenanceBay", 850f, 3, 2)]
        [TestCase("ForwardSupplyPost", 700f, 2, 2)]
        public void NewRole_DefaultsApplyBeforeActivation_AndInitializeKeepsFixedKind(
            string kind, float maxHealth, int width, int height)
        {
            var root = new GameObject("Inactive structure " + kind);
            try
            {
                root.SetActive(false);
                var structure = AddStructure(root, kind);
                Assert.That(Property(structure, "Footprint"), Is.EqualTo(new Vector2Int(width, height)));
                var health = root.GetComponent(BuildingType("BuildingHealth"));
                Assert.That(health, Is.Not.Null);
                Assert.That(Property(health, "MaxHealth"), Is.EqualTo(maxHealth));
                Assert.That(Property(health, "CurrentHealth"), Is.EqualTo(maxHealth));
                AssertRegistrations(structure, UnitTeam.Team1, 0);

                Initialize(structure, FirstTeam, "Other", new Vector2Int(5, 1), false);
                Assert.That(Property(structure, "Kind").ToString(), Is.EqualTo(kind));
                root.SetActive(true);
                Assert.That(Property(structure, "Completed"), Is.EqualTo(false));
                Assert.That(Property(structure, "Footprint"), Is.EqualTo(new Vector2Int(5, 1)));
                Assert.That(Property(health, "MaxHealth"), Is.EqualTo(maxHealth));
                Invoke(structure, "MarkCompleted");
                Assert.That(Property(structure, "IsAlive"), Is.EqualTo(true));
                AssertSingleComponents(root, kind);
                AssertRegistrations(structure, FirstTeam, 1);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void Factory_ExistingLegacyStatus_IsReusedWithoutResettingAuthoredValues(string kind)
        {
            var root = new GameObject("Legacy structure " + kind);
            try
            {
                var legacy = root.AddComponent(BuildingType("BuildingStatus"));
                Initialize(legacy, FirstTeam, kind, new Vector2Int(4, 3), false);
                Invoke(legacy, "ConfigureVisionRadius", 3.5f);
                Invoke(legacy, "ConfigureSupplyProvided", 6);
                var health = root.GetComponent(BuildingType("BuildingHealth"));
                Invoke(health, "ConfigureMaxHealth", 432f);

                Assert.That(AddStructure(root, kind), Is.SameAs(legacy));
                Assert.That(AddStructure(root, kind), Is.SameAs(legacy));
                Assert.That(legacy.GetType(), Is.EqualTo(BuildingType("BuildingStatus")));
                Assert.That(Property(legacy, "Team"), Is.EqualTo(FirstTeam));
                Assert.That(Property(legacy, "Kind").ToString(), Is.EqualTo(kind));
                Assert.That(Property(legacy, "Footprint"), Is.EqualTo(new Vector2Int(4, 3)));
                Assert.That(Property(legacy, "Completed"), Is.EqualTo(false));
                Assert.That(Property(legacy, "VisionRadius"), Is.EqualTo(3.5f));
                Assert.That(Property(legacy, "SupplyProvided"), Is.EqualTo(6));
                Assert.That(Property(health, "MaxHealth"), Is.EqualTo(432f));
                Assert.That(root.GetComponents(BuildingType("Structure")), Has.Length.EqualTo(1));
                AssertRegistrations(legacy, FirstTeam, 1);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Other")]
        [TestCase("ResearchLab")]
        [TestCase("ForwardSupplyPost")]
        public void CommonLifecycle_CompletionTeamChangesDisableAndDeath_KeepRegistriesUnique(string kind)
        {
            var root = new GameObject("Structure lifecycle " + kind);
            Component structure = null;
            try
            {
                structure = AddStructure(root, kind);
                var target = (IUnitAttackTarget)structure;
                var health = root.GetComponent(BuildingType("BuildingHealth"));
                Initialize(structure, FirstTeam, kind, new Vector2Int(0, -1), false);
                Assert.That(Property(structure, "Footprint"), Is.EqualTo(Vector2Int.one));
                Assert.That(target.IsAlive, Is.False);
                var maximum = (float)Property(health, "MaxHealth");
                target.TakeDamage(10000f);
                Assert.That(Property(health, "CurrentHealth"), Is.EqualTo(maximum));
                AssertRegistrations(structure, FirstTeam, 1);

                Invoke(structure, "MarkCompleted");
                Invoke(structure, "MarkCompleted");
                Assert.That(target.IsAlive, Is.True);
                AssertRegistrations(structure, FirstTeam, 1);
                target.TakeDamage(25f);
                Assert.That(Property(health, "CurrentHealth"), Is.EqualTo(maximum - 25f));

                Initialize(structure, SecondTeam, kind, Vector2Int.one, true);
                Initialize(structure, SecondTeam, kind, Vector2Int.one, true);
                AssertRegistrations(structure, SecondTeam, 1);
                AssertTeamMembership(structure, FirstTeam, 0);
                ((Behaviour)structure).enabled = false;
                AssertRegistrations(structure, SecondTeam, 0);
                Invoke(structure, "ConfigureVisionRadius", 7f);
                Invoke(structure, "ConfigureSupplyProvided", 4);
                AssertRegistrations(structure, SecondTeam, 0);
                ((Behaviour)structure).enabled = true;
                AssertRegistrations(structure, SecondTeam, 1);

                target.TakeDamage(10000f);
                Assert.That(target.IsAlive, Is.False);
                Assert.That(Property(health, "IsDestroyed"), Is.EqualTo(true));
                Assert.That(root.activeSelf, Is.False);
                AssertRegistrations(structure, SecondTeam, 0);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssertRegistrations(structure, SecondTeam, 0);
        }

        [Test]
        public void SignalRelay_CompletionRadiusTeamAndDisable_RefreshFogProvider()
        {
            var root = new GameObject("Signal relay vision");
            try
            {
                var structure = AddStructure(root, "SignalRelay");
                Initialize(structure, FirstTeam, "SignalRelay", new Vector2Int(2, 2), false);
                var provider = (IFogVisionProvider)structure;
                Assert.That(provider.VisionTransform, Is.SameAs(root.transform));
                Assert.That(provider.VisionRadius, Is.EqualTo(12f));
                Assert.That(provider.IsVisionActive, Is.False);
                var version = FogOfWarRegistry.GetVersion(FirstTeam);

                Invoke(structure, "MarkCompleted");
                Assert.That(provider.IsVisionActive, Is.True);
                Assert.That(FogOfWarRegistry.GetVersion(FirstTeam), Is.GreaterThan(version));
                version = FogOfWarRegistry.GetVersion(FirstTeam);
                Invoke(structure, "ConfigureVisionRadius", 15f);
                Assert.That(provider.VisionRadius, Is.EqualTo(15f));
                Assert.That(FogOfWarRegistry.GetVersion(FirstTeam), Is.GreaterThan(version));
                Invoke(structure, "ConfigureVisionRadius", -1f);
                Assert.That(provider.VisionRadius, Is.Zero);

                var oldTeamVersion = FogOfWarRegistry.GetVersion(FirstTeam);
                var newTeamVersion = FogOfWarRegistry.GetVersion(SecondTeam);
                Initialize(structure, SecondTeam, "SignalRelay", new Vector2Int(2, 2), true);
                Assert.That(provider.Team, Is.EqualTo(SecondTeam));
                Assert.That(FogOfWarRegistry.GetVersion(FirstTeam), Is.GreaterThan(oldTeamVersion));
                Assert.That(FogOfWarRegistry.GetVersion(SecondTeam), Is.GreaterThan(newTeamVersion));
                AssertTeamMembership(structure, FirstTeam, 0);
                AssertRegistrations(structure, SecondTeam, 1);
                root.SetActive(false);
                Assert.That(provider.IsVisionActive, Is.False);
                AssertRegistrations(structure, SecondTeam, 0);
                root.SetActive(true);
                Assert.That(provider.IsVisionActive, Is.True);
                AssertRegistrations(structure, SecondTeam, 1);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ForwardSupplyPost_CompletionTeamChangeDisableAndDeath_UpdateSupplyAndDropOff()
        {
            var ownedObjects = new List<GameObject>();
            try
            {
                var firstSupply = GetOrCreateTeamService(ownedObjects, "SupplyManager", FirstTeam);
                var secondSupply = GetOrCreateTeamService(ownedObjects, "SupplyManager", SecondTeam);
                GetOrCreateTeamService(ownedObjects, "PlayerResourceWallet", FirstTeam);
                GetOrCreateTeamService(ownedObjects, "PlayerResourceWallet", SecondTeam);
                var firstBaseline = (int)Property(firstSupply, "MaxSupply");
                var secondBaseline = (int)Property(secondSupply, "MaxSupply");
                var root = CreateOwnedObject(ownedObjects, "Forward supply lifecycle");
                root.transform.position = new Vector3(50000f, 50000f, 0f);
                root.SetActive(false);
                var structure = AddStructure(root, "ForwardSupplyPost");
                Initialize(structure, FirstTeam, "ForwardSupplyPost", new Vector2Int(2, 2), false);
                root.SetActive(true);
                var dropOff = root.GetComponent(BuildingType("ResourceDropOff"));
                Assert.That(dropOff, Is.Not.Null);
                Assert.That(Property(structure, "SupplyProvided"), Is.EqualTo(5));
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                Assert.That(Property(dropOff, "CanAcceptDeposits"), Is.EqualTo(false));
                Assert.That(FindNearestDropOff(FirstTeam, root.transform.position), Is.Not.SameAs(dropOff));

                Invoke(structure, "MarkCompleted");
                Invoke(structure, "MarkCompleted");
                AssertSupply(firstSupply, firstBaseline + 5, secondSupply, secondBaseline);
                Assert.That(FindNearestDropOff(FirstTeam, root.transform.position), Is.SameAs(dropOff));
                Assert.That(Property(dropOff, "CanAcceptDeposits"), Is.EqualTo(true));
                AssertDropOffCount(dropOff, 1);

                Initialize(structure, SecondTeam, "ForwardSupplyPost", new Vector2Int(2, 2), false);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                Assert.That(FindNearestDropOff(FirstTeam, root.transform.position), Is.Not.SameAs(dropOff));
                Assert.That(FindNearestDropOff(SecondTeam, root.transform.position), Is.Not.SameAs(dropOff));
                Assert.That(Property(dropOff, "Team"), Is.EqualTo(SecondTeam));
                Assert.That(Property(dropOff, "CanAcceptDeposits"), Is.EqualTo(false));
                Invoke(structure, "MarkCompleted");
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline + 5);
                Assert.That(FindNearestDropOff(SecondTeam, root.transform.position), Is.SameAs(dropOff));
                Assert.That(Property(dropOff, "CanAcceptDeposits"), Is.EqualTo(true));
                Invoke(structure, "ConfigureSupplyProvided", 8);
                Invoke(structure, "ConfigureSupplyProvided", 8);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline + 8);

                root.SetActive(false);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                Assert.That(FindNearestDropOff(SecondTeam, root.transform.position), Is.Not.SameAs(dropOff));
                AssertDropOffCount(dropOff, 0);
                AssertRegistrations(structure, SecondTeam, 0);
                Invoke(structure, "ConfigureSupplyProvided", 11);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                root.SetActive(true);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline + 11);
                Assert.That(FindNearestDropOff(SecondTeam, root.transform.position), Is.SameAs(dropOff));
                AssertDropOffCount(dropOff, 1);
                AssertRegistrations(structure, SecondTeam, 1);

                ((Behaviour)structure).enabled = false;
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                ((Behaviour)structure).enabled = true;
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline + 11);
                ((IUnitAttackTarget)structure).TakeDamage(10000f);
                AssertSupply(firstSupply, firstBaseline, secondSupply, secondBaseline);
                Assert.That(Property(dropOff, "CanAcceptDeposits"), Is.EqualTo(false));
                Assert.That(FindNearestDropOff(SecondTeam, root.transform.position), Is.Not.SameAs(dropOff));
                AssertDropOffCount(dropOff, 0);
                AssertRegistrations(structure, SecondTeam, 0);
            }
            finally
            {
                for (var i = ownedObjects.Count - 1; i >= 0; i--)
                {
                    if (ownedObjects[i] != null)
                    {
                        Object.DestroyImmediate(ownedObjects[i]);
                    }
                }
            }
        }

        private static Vector2Int DefaultFootprint(string kind)
        {
            switch (kind)
            {
                case "MainBase":
                case "VehicleFactory":
                    return new Vector2Int(3, 3);
                case "TacticalCommandCenter":
                case "MaintenanceBay":
                    return new Vector2Int(3, 2);
                default:
                    return new Vector2Int(2, 2);
            }
        }

        private static void AssertSingleComponents(GameObject root, string kind)
        {
            Assert.That(root.GetComponents(BuildingType("Structure")), Has.Length.EqualTo(1));
            Assert.That(root.GetComponents(BuildingType("BuildingStatus")), Has.Length.EqualTo(1));
            Assert.That(root.GetComponents(BuildingType("BuildingHealth")), Has.Length.EqualTo(1));
            if (kind == "MainBase"
                || kind == "Production"
                || kind == "SpliterProduction"
                || kind == "VehicleFactory"
                || kind == "MaintenanceBay"
                || kind == "SignalRelay")
            {
                Assert.That(root.GetComponents(BuildingType("UnitProductionQueue")), Has.Length.EqualTo(1));
            }
            if (kind == "MainBase" || kind == "ResourceDropOff" || kind == "ForwardSupplyPost")
            {
                Assert.That(root.GetComponents(BuildingType("ResourceDropOff")), Has.Length.EqualTo(1));
            }
            if (kind == "AutoTurret" || kind == "SpeedAura")
            {
                Assert.That(root.GetComponents(BuildingType("Building" + kind)), Has.Length.EqualTo(1));
            }
        }

        private static void AssertRegistrations(Component structure, UnitTeam team, int count)
        {
            Assert.That(CountReferences((IEnumerable)Property(BuildingType("BuildingRegistry"), "All"), structure),
                Is.EqualTo(count), "Building registry membership");
            Assert.That(CountReferences(UnitAttackTargetRegistry.All, structure),
                Is.EqualTo(count), "Attack target registry membership");
            Assert.That(CountReferences(FogOfWarRegistry.All, structure),
                Is.EqualTo(count), "Fog provider registry membership");
            AssertTeamMembership(structure, team, count);
        }

        private static void AssertTeamMembership(Component structure, UnitTeam team, int count)
        {
            var buildings = (IEnumerable)Invoke(BuildingType("BuildingRegistry"), "GetBuildings", team);
            Assert.That(CountReferences(buildings, structure), Is.EqualTo(count), "Team building membership");
            Assert.That(CountReferences(UnitAttackTargetRegistry.GetTargets(team), structure),
                Is.EqualTo(count), "Team attack target membership");
            Assert.That(CountReferences(FogOfWarRegistry.GetProviders(team), structure),
                Is.EqualTo(count), "Team fog provider membership");
        }

        private static int CountReferences(IEnumerable values, object expected)
        {
            var count = 0;
            foreach (var value in values)
            {
                if (ReferenceEquals(value, expected))
                {
                    count++;
                }
            }
            return count;
        }

        private static void AssertSupply(Component first, int firstExpected, Component second, int secondExpected)
        {
            Assert.That(Property(first, "MaxSupply"), Is.EqualTo(firstExpected), "First team supply");
            Assert.That(Property(second, "MaxSupply"), Is.EqualTo(secondExpected), "Second team supply");
        }

        private static void AssertDropOffCount(Component dropOff, int count)
        {
            Assert.That(CountReferences((IEnumerable)Property(BuildingType("ResourceDropOff"), "All"), dropOff),
                Is.EqualTo(count), "Drop-off registry membership");
        }

        private static object FindNearestDropOff(UnitTeam team, Vector3 position)
        {
            return Invoke(BuildingType("ResourceDropOff"), "FindNearest", team, position);
        }

        private static Component GetOrCreateTeamService(List<GameObject> owned, string typeName, UnitTeam team)
        {
            var type = GetGameplayType("ProjectS.Resources." + typeName);
            var existing = (Component)Invoke(type, "FindForTeam", team);
            if (existing != null)
            {
                return existing;
            }

            var root = CreateOwnedObject(owned, typeName + " " + team);
            root.SetActive(false);
            var service = root.AddComponent(type);
            if (typeName == "PlayerResourceWallet")
            {
                var resources = Activator.CreateInstance(GetGameplayType("ProjectS.Resources.ResourceAmount"), 0, 0);
                Invoke(service, "Initialize", team, resources);
            }
            else
            {
                Invoke(service, "Initialize", team);
            }
            root.SetActive(true);
            return service;
        }

        private static GameObject CreateOwnedObject(List<GameObject> owned, string name)
        {
            var root = new GameObject(name);
            owned.Add(root);
            return root;
        }

        private static Component AddStructure(GameObject root, string kind)
        {
            var result = (Component)Invoke(BuildingType("StructureFactory"), "AddTo", root, ParseKind(kind));
            Assert.That(result, Is.Not.Null, "Factory must return the structure component.");
            return result;
        }

        private static void Initialize(Component structure, UnitTeam team, string kind, Vector2Int footprint, bool completed)
        {
            Invoke(structure, "Initialize", team, ParseKind(kind), footprint, completed);
        }

        private static object ParseKind(string kind)
        {
            return Enum.Parse(BuildingType("BuildingKind"), kind);
        }

        private static Type BuildingType(string name)
        {
            return GetGameplayType("ProjectS.Buildings." + name);
        }

        private static Type GetGameplayType(string name)
        {
            var type = Type.GetType(name + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "Could not resolve gameplay type " + name);
            return type;
        }

        private static object Property(object target, string name)
        {
            var type = target as Type ?? target.GetType();
            var flags = BindingFlags.Public | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            var property = type.GetProperty(name, flags);
            Assert.That(property, Is.Not.Null, type.FullName + "." + name);
            return property.GetValue(target is Type ? null : target);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            var type = target as Type ?? target.GetType();
            var flags = BindingFlags.Public | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            var argumentTypes = Array.ConvertAll(arguments, argument => argument.GetType());
            var method = type.GetMethod(name, flags, null, argumentTypes, null);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            return method.Invoke(target is Type ? null : target, arguments);
        }
    }
}
