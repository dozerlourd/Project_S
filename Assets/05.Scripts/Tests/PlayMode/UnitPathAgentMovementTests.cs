using System;
using System.Collections;
using NUnit.Framework;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class UnitPathAgentMovementTests
    {
        private const float MovementSpeed = 3f;
        private const float MovementTolerance = 0.0025f;
        private static readonly Type PlayerResourceWalletType = GetGameplayType("ProjectS.Resources.PlayerResourceWallet");
        private static readonly Type ResourceAmountType = GetGameplayType("ProjectS.Resources.ResourceAmount");
        private static readonly Type ResourceNodeType = GetGameplayType("ProjectS.Resources.ResourceNode");
        private static readonly Type ResourceTypeType = GetGameplayType("ProjectS.Resources.ResourceType");
        private static readonly Type BuildingStatusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
        private static readonly Type BuildingKindType = GetGameplayType("ProjectS.Buildings.BuildingKind");
        private static readonly Type ConstructionSiteType = GetGameplayType("ProjectS.Buildings.ConstructionSite");
        private static readonly Type BuildingPlacementServiceType = GetGameplayType("ProjectS.Buildings.BuildingPlacementService");
        private static readonly Type ResourceDropOffType = GetGameplayType("ProjectS.Buildings.ResourceDropOff");
        private static readonly Type UnitProductionDefinitionType = GetGameplayType("ProjectS.Buildings.UnitProductionDefinition");
        private static readonly Type UnitProductionQueueType = GetGameplayType("ProjectS.Buildings.UnitProductionQueue");
        private static readonly Type WorkerGatherControllerType = GetGameplayType("ProjectS.Resources.WorkerGatherController");
        private static readonly Type RtsGameHudType = GetGameplayType("ProjectS.UI.RtsGameHud");

        [UnityTest]
        public IEnumerator MoveTo_DoesNotExceedConfiguredSpeedNearDestination()
        {
            var unit = CreateMovableUnit("SmoothSingleUnit", Vector3.zero);
            var agent = unit.GetComponent<UnitPathAgent>();
            var previousPosition = unit.transform.position;
            var previousTime = Time.time;
            var maxSpeed = 0f;

            Assert.That(agent.MoveTo(new Vector3(0.07f, 0f, 0f)), Is.True);

            for (var i = 0; i < 6; i++)
            {
                yield return null;

                var deltaTime = Time.time - previousTime;
                if (deltaTime > 0f)
                {
                    var distance = Vector3.Distance(previousPosition, unit.transform.position);
                    maxSpeed = Mathf.Max(maxSpeed, distance / deltaTime);
                }

                previousPosition = unit.transform.position;
                previousTime = Time.time;
            }

            Object.Destroy(unit);
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(MovementSpeed + MovementTolerance));
        }

        [UnityTest]
        public IEnumerator MoveTo_WithNearbyMovingUnit_DoesNotAddSeparationSpeedOnTopOfMovementSpeed()
        {
            var leader = CreateMovableUnit("SmoothLeader", Vector3.zero);
            var neighbor = CreateMovableUnit("SmoothNeighbor", new Vector3(0.35f, 0.1f, 0f));
            var leaderAgent = leader.GetComponent<UnitPathAgent>();
            var neighborAgent = neighbor.GetComponent<UnitPathAgent>();
            var previousPosition = leader.transform.position;
            var previousTime = Time.time;
            var maxSpeed = 0f;

            Assert.That(leaderAgent.MoveTo(new Vector3(3f, 0f, 0f)), Is.True);
            Assert.That(neighborAgent.MoveTo(new Vector3(3.35f, 0.1f, 0f)), Is.True);

            for (var i = 0; i < 30; i++)
            {
                yield return null;

                var deltaTime = Time.time - previousTime;
                if (deltaTime > 0f)
                {
                    var distance = Vector3.Distance(previousPosition, leader.transform.position);
                    maxSpeed = Mathf.Max(maxSpeed, distance / deltaTime);
                }

                previousPosition = leader.transform.position;
                previousTime = Time.time;
            }

            Object.Destroy(leader);
            Object.Destroy(neighbor);
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(MovementSpeed + MovementTolerance));
        }

        [UnityTest]
        public IEnumerator AttackMove_AcquiresNearbyEnemyAndEntersAttackState()
        {
            var attacker = CreateMovableUnit("AttackMoveAttacker", Vector3.zero, UnitTeam.Team1);
            var enemy = CreateMovableUnit("AttackMoveEnemy", new Vector3(0.75f, 0f, 0f), UnitTeam.Team2);
            var commandAgent = attacker.GetComponent<UnitCommandAgent>();
            var enemyStatus = enemy.GetComponent<PrototypeUnitStatus>();

            commandAgent.Issue(new UnitCommand(UnitCommandMode.AttackMove, new Vector3(4f, 0f, 0f), null, false));
            yield return new WaitForSeconds(0.35f);

            Assert.That(commandAgent.PriorityTarget, Is.EqualTo(enemyStatus));
            Assert.That(commandAgent.ActionState, Is.EqualTo(UnitActionState.AttackingTarget));

            Object.Destroy(attacker);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator UnitRegistry_TracksAgentsByInitializedTeam()
        {
            var friendly = CreateMovableUnit("RegistryFriendly", Vector3.zero, UnitTeam.Team1);
            var enemy = CreateMovableUnit("RegistryEnemy", Vector3.right, UnitTeam.Team2);
            var friendlyAgent = friendly.GetComponent<UnitCommandAgent>();
            var enemyAgent = enemy.GetComponent<UnitCommandAgent>();

            yield return null;

            Assert.That(UnitRegistry.GetAgents(UnitTeam.Team1), Does.Contain(friendlyAgent));
            Assert.That(UnitRegistry.GetAgents(UnitTeam.Team2), Does.Contain(enemyAgent));

            Object.Destroy(friendly);
            Object.Destroy(enemy);
        }

        [UnityTest]
        public IEnumerator ResourceNode_FindNearestAvailableIgnoresDepletedAndDisabledNodes()
        {
            var depletedObject = CreateResourceNode(
                "DepletedMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                5,
                5,
                0f,
                new Vector3(0.1f, 0f, 0f));
            var disabledObject = CreateResourceNode(
                "DisabledMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                new Vector3(0.2f, 0f, 0f));
            var availableObject = CreateResourceNode(
                "AvailableMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                new Vector3(3f, 0f, 0f));
            var depletedNode = depletedObject.GetComponent(ResourceNodeType);
            var availableNode = availableObject.GetComponent(ResourceNodeType);

            Invoke(depletedNode, "TryGather");
            disabledObject.SetActive(false);

            yield return null;

            var found = InvokeStatic(
                ResourceNodeType,
                "FindNearestAvailable",
                Vector3.zero,
                Enum.Parse(ResourceTypeType, "Minerals"));

            Assert.That(found, Is.EqualTo(availableNode));

            Object.Destroy(depletedObject);
            Object.Destroy(disabledObject);
            Object.Destroy(availableObject);
        }

        [UnityTest]
        public IEnumerator PlayerCommandController_DetectsResourceNodeAtWorldPointForInteraction()
        {
            var controllerObject = new GameObject("InteractionDetectionController");
            var controller = controllerObject.AddComponent<PlayerUnitCommandController>();
            var resourceObject = CreateResourceNode(
                "ClickableMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                Vector3.zero);
            var resourceNode = resourceObject.GetComponent(ResourceNodeType);

            yield return null;

            Assert.That(controller.TryGetInteractableAtWorldPoint(Vector3.zero, out var target), Is.True);
            Assert.That(target, Is.EqualTo(resourceNode));

            Object.Destroy(resourceObject);
            Object.Destroy(controllerObject);
        }

        [UnityTest]
        public IEnumerator PlayerCommandController_DetectsResourceNodeAcrossVisualBounds()
        {
            var controllerObject = new GameObject("WideInteractionDetectionController");
            var controller = controllerObject.AddComponent<PlayerUnitCommandController>();
            var resourceObject = new GameObject("WideClickableMinerals");
            var renderer = resourceObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateTestSprite(32, 32, 12f);
            var collider = resourceObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.4f, 0.4f);
            collider.isTrigger = true;
            var resourceNode = resourceObject.AddComponent(ResourceNodeType);
            Invoke(
                resourceNode,
                "Configure",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                1f,
                false);

            yield return null;

            Assert.That(collider.size.x, Is.GreaterThan(2.5f));
            Assert.That(controller.TryGetInteractableAtWorldPoint(new Vector3(1.2f, 0f, 0f), out var target), Is.True);
            Assert.That(target, Is.EqualTo(resourceNode));

            Object.Destroy(resourceObject);
            Object.Destroy(controllerObject);
        }

        [UnityTest]
        public IEnumerator RtsGameHud_RebindsWhenTeamWalletIsReplaced()
        {
            LogAssert.Expect(LogType.Warning, "Replacing existing resource wallet for Team1. Only one active wallet should own a team's resources.");
            var hudObject = new GameObject("ResourceHudRefresh");
            var hud = hudObject.AddComponent(RtsGameHudType);
            var firstWalletObject = new GameObject("HudFirstWallet");
            var firstWallet = firstWalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(firstWallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(10, 0));

            yield return null;

            Assert.That(GetPrivateField<object>(hud, "wallet"), Is.EqualTo(firstWallet));

            var secondWalletObject = new GameObject("HudSecondWallet");
            var secondWallet = secondWalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(secondWallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(25, 0));

            yield return null;

            Assert.That(GetPrivateField<object>(hud, "wallet"), Is.EqualTo(secondWallet));

            Object.Destroy(secondWalletObject);
            Object.Destroy(firstWalletObject);
            Object.Destroy(hudObject);
        }

        [UnityTest]
        public IEnumerator WorkerInteractCommand_WithDetectedResourceNodeStartsGathering()
        {
            LogAssert.Expect(LogType.Warning, "No available resource drop-off found for carried resources.");
            var controllerObject = new GameObject("WorkerInteractionDetectionController");
            var controller = controllerObject.AddComponent<PlayerUnitCommandController>();
            var resourceObject = CreateResourceNode(
                "DetectedGatherMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                new Vector3(0.2f, 0f, 0f));
            var worker = CreateWorkerUnit("DetectedGatherWorker", Vector3.zero);
            var commandAgent = worker.GetComponent<UnitCommandAgent>();
            var gatherController = worker.GetComponent(WorkerGatherControllerType);
            var resourceNode = resourceObject.GetComponent(ResourceNodeType);

            yield return null;

            Assert.That(controller.TryGetInteractableAtWorldPoint(new Vector3(0.2f, 0f, 0f), out var target), Is.True);
            commandAgent.Issue(new UnitCommand(
                UnitCommandMode.Interact,
                target.InteractionPoint,
                null,
                target,
                false));

            for (var i = 0; i < 120 && GetInt(resourceNode, "RemainingAmount") == 20; i++)
            {
                yield return null;
            }

            Assert.That(GetInt(resourceNode, "RemainingAmount"), Is.EqualTo(15));
            Assert.That((string)GetProperty(commandAgent, "LastInteractionFailureReason"), Is.Empty);
            Assert.That((string)GetProperty(gatherController, "LastFailureReason"), Does.Contain("No available resource drop-off"));

            Object.Destroy(worker);
            Object.Destroy(resourceObject);
            Object.Destroy(controllerObject);
        }

        [UnityTest]
        public IEnumerator ResourceDropOff_ReregistersAfterBuildingTeamChangesAndDepositsToCorrectWallet()
        {
            var team1WalletObject = new GameObject("Team1Wallet");
            var team1Wallet = team1WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team1Wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(0, 0));
            var team2WalletObject = new GameObject("Team2Wallet");
            var team2Wallet = team2WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team2Wallet, "Initialize", UnitTeam.Team2, CreateResourceAmount(0, 0));
            var dropOffObject = new GameObject("RetimedDropOff");
            var status = dropOffObject.AddComponent(BuildingStatusType);
            var dropOff = dropOffObject.AddComponent(ResourceDropOffType);

            Invoke(status, "Initialize", UnitTeam.Team2, Enum.Parse(BuildingKindType, "MainBase"), Vector2Int.one, true);

            yield return null;

            var team1DropOff = InvokeStatic(ResourceDropOffType, "FindNearest", UnitTeam.Team1, Vector3.zero);
            var team2DropOff = InvokeStatic(ResourceDropOffType, "FindNearest", UnitTeam.Team2, Vector3.zero);

            Assert.That(team1DropOff, Is.Null);
            Assert.That(team2DropOff, Is.EqualTo(dropOff));
            Assert.That((bool)Invoke(dropOff, "TryDeposit", UnitTeam.Team2, CreateResourceAmount(15, 3)), Is.True);
            Assert.That(GetInt(team1Wallet, "Minerals"), Is.EqualTo(0));
            Assert.That(GetInt(team2Wallet, "Minerals"), Is.EqualTo(15));
            Assert.That(GetInt(team2Wallet, "Gas"), Is.EqualTo(3));

            Object.Destroy(dropOffObject);
            Object.Destroy(team1WalletObject);
            Object.Destroy(team2WalletObject);
        }

        [UnityTest]
        public IEnumerator ConstructionSite_TryCreateSpendsCostAndBuilderContributionCompletes()
        {
            var walletObject = new GameObject("ConstructionWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(200, 25));
            var builder = CreateWorkerUnit("ConstructionWorker", Vector3.left);
            var tryCreate = ConstructionSiteType.GetMethod("TryCreate");
            var tryCreateArguments = new[]
            {
                (object)Vector3.zero,
                UnitTeam.Team1,
                wallet,
                null,
                null,
                null,
                Enum.Parse(BuildingKindType, "MainBase"),
                CreateResourceAmount(75, 10),
                0.2f,
                Vector2Int.one,
                null
            };

            var created = (bool)tryCreate.Invoke(null, tryCreateArguments);
            var site = (Component)tryCreateArguments[10];

            Assert.That(created, Is.True);
            Assert.That(GetInt(wallet, "Minerals"), Is.EqualTo(125));
            Assert.That(GetInt(wallet, "Gas"), Is.EqualTo(15));
            Assert.That(site.GetComponent<BoxCollider2D>().size, Is.EqualTo(Vector2.one));

            var builderAgent = builder.GetComponent<UnitCommandAgent>();
            Assert.That((bool)Invoke(site, "TryContribute", builderAgent, GetFloat(site, "BuildTime")), Is.True);

            yield return null;

            Assert.That(GetBool(site, "Completed"), Is.True);
            Assert.That(site.GetComponent(BuildingStatusType), Is.Not.Null);
            Assert.That(site.GetComponent(ResourceDropOffType), Is.Not.Null);
            Assert.That(site.GetComponent(UnitProductionQueueType), Is.Not.Null);

            Object.Destroy(site.gameObject);
            Object.Destroy(builder);
            Object.Destroy(walletObject);
        }

        [UnityTest]
        public IEnumerator ConstructionSite_TryCreateFailsWithoutWalletForNonFreeCost()
        {
            LogAssert.Expect(LogType.Warning, "Cannot place MainBase construction site: no resource wallet is available for Team1.");
            var tryCreate = ConstructionSiteType.GetMethod("TryCreate");
            var tryCreateArguments = new[]
            {
                (object)Vector3.zero,
                UnitTeam.Team1,
                null,
                null,
                null,
                null,
                Enum.Parse(BuildingKindType, "MainBase"),
                CreateResourceAmount(75, 10),
                0.2f,
                Vector2Int.one,
                null
            };

            var created = (bool)tryCreate.Invoke(null, tryCreateArguments);

            Assert.That(created, Is.False);
            Assert.That(tryCreateArguments[10], Is.Null);
            Assert.That((string)GetProperty(ConstructionSiteType, "LastCreateFailureReason"), Does.Contain("no resource wallet"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConstructionSite_TryCreateRejectsMismatchedWalletTeam()
        {
            LogAssert.Expect(LogType.Warning, "Cannot place MainBase construction site: wallet team mismatch. Expected Team1, received Team2.");
            var walletObject = new GameObject("MismatchedConstructionWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team2, CreateResourceAmount(200, 0));
            var tryCreate = ConstructionSiteType.GetMethod("TryCreate");
            var tryCreateArguments = new[]
            {
                (object)Vector3.zero,
                UnitTeam.Team1,
                wallet,
                null,
                null,
                null,
                Enum.Parse(BuildingKindType, "MainBase"),
                CreateResourceAmount(75, 0),
                0.2f,
                Vector2Int.one,
                null
            };

            var created = (bool)tryCreate.Invoke(null, tryCreateArguments);

            Assert.That(created, Is.False);
            Assert.That(tryCreateArguments[10], Is.Null);
            Assert.That(GetInt(wallet, "Minerals"), Is.EqualTo(200));
            Assert.That((string)GetProperty(ConstructionSiteType, "LastCreateFailureReason"), Does.Contain("wallet team mismatch"));

            Object.Destroy(walletObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingPlacementService_UsesOwnerTeamWalletWhenConfiguredWalletTeamDiffers()
        {
            var team1WalletObject = new GameObject("PlacementTeam1Wallet");
            var team1Wallet = team1WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team1Wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(100, 0));
            var team2WalletObject = new GameObject("PlacementTeam2Wallet");
            var team2Wallet = team2WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team2Wallet, "Initialize", UnitTeam.Team2, CreateResourceAmount(100, 0));
            var serviceObject = new GameObject("PlacementService");
            var service = serviceObject.AddComponent(BuildingPlacementServiceType);

            Invoke(
                service,
                "Configure",
                UnitTeam.Team1,
                team2Wallet,
                null,
                null,
                null,
                Enum.Parse(BuildingKindType, "MainBase"),
                CreateResourceAmount(40, 0),
                0.2f,
                Vector2Int.one);

            var tryPlaceArguments = new[] { (object)Vector3.zero, null };
            var placed = (bool)Invoke(service, "TryPlaceDefaultConstructionSite", tryPlaceArguments);
            var site = (Component)tryPlaceArguments[1];

            Assert.That(placed, Is.True);
            Assert.That(site, Is.Not.Null);
            Assert.That(GetInt(team1Wallet, "Minerals"), Is.EqualTo(60));
            Assert.That(GetInt(team2Wallet, "Minerals"), Is.EqualTo(100));

            Object.Destroy(site.gameObject);
            Object.Destroy(serviceObject);
            Object.Destroy(team1WalletObject);
            Object.Destroy(team2WalletObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConstructionSite_PlacementPreviewRejectsOccupiedBuildingResourceAndUnit()
        {
            var building = CreateProductionBuilding("PlacementBlockedBuilding", UnitTeam.Team1, Vector3.zero);

            var buildingPreviewCells = (IEnumerable)InvokeStatic(
                ConstructionSiteType,
                "GetPlacementPreviewCells",
                null,
                Vector3.zero,
                Vector2Int.one);
            var buildingPreviewCell = GetFirstPreviewCell(buildingPreviewCells);

            Assert.That(GetField<bool>(buildingPreviewCell, "CanPlace"), Is.False);
            Assert.That(GetField<string>(buildingPreviewCell, "FailureReason"), Does.Contain("building"));

            Object.Destroy(building);
            yield return null;

            var resource = CreateResourceNode(
                "PlacementBlockedResource",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                Vector3.zero);

            var resourceReason = (string)InvokeStatic(
                ConstructionSiteType,
                "GetPlacementFailureReason",
                null,
                Vector3.zero,
                Vector2Int.one);

            Assert.That(resourceReason, Does.Contain("resource node"));

            Object.Destroy(resource);
            yield return null;

            var unit = CreateMovableUnit("PlacementBlockedUnit", Vector3.zero, UnitTeam.Team1);

            var unitReason = (string)InvokeStatic(
                ConstructionSiteType,
                "GetPlacementFailureReason",
                null,
                Vector3.zero,
                Vector2Int.one);

            Assert.That(unitReason, Does.Contain("unit"));

            Object.Destroy(unit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingPlacementService_ReportsFailureReasonAndKeepsCostWhenPlacementIsBlocked()
        {
            var walletObject = new GameObject("BlockedPlacementWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(100, 0));
            var resource = CreateResourceNode(
                "BlockedPlacementMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                Vector3.zero);
            var serviceObject = new GameObject("BlockedPlacementService");
            var service = serviceObject.AddComponent(BuildingPlacementServiceType);

            Invoke(
                service,
                "Configure",
                UnitTeam.Team1,
                wallet,
                null,
                null,
                null,
                Enum.Parse(BuildingKindType, "MainBase"),
                CreateResourceAmount(40, 0),
                0.2f,
                Vector2Int.one);

            Assert.That((bool)Invoke(service, "CanPlaceDefaultConstructionSite", Vector3.zero), Is.False);
            Assert.That((string)GetProperty(service, "LastPlacementFailureReason"), Does.Contain("resource node"));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*resource node.*"));
            var tryPlaceArguments = new[] { (object)Vector3.zero, null };
            var placed = (bool)Invoke(service, "TryPlaceDefaultConstructionSite", tryPlaceArguments);

            Assert.That(placed, Is.False);
            Assert.That(tryPlaceArguments[1], Is.Null);
            Assert.That(GetInt(wallet, "Minerals"), Is.EqualTo(100));
            Assert.That((string)GetProperty(service, "LastPlacementFailureReason"), Does.Contain("resource node"));

            Object.Destroy(serviceObject);
            Object.Destroy(resource);
            Object.Destroy(walletObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkerGatherController_RepeatsGatherAndDepositIntoTeamWallet()
        {
            var walletObject = new GameObject("GatherWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(0, 0));
            var dropOffObject = CreateDropOff("GatherDropOff", UnitTeam.Team1, new Vector3(0.35f, 0f, 0f));
            var resourceObject = CreateResourceNode(
                "GatherMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                new Vector3(-0.35f, 0f, 0f));
            var worker = CreateWorkerUnit("GatherWorker", Vector3.zero);
            var commandAgent = worker.GetComponent<UnitCommandAgent>();
            var resourceNode = resourceObject.GetComponent(ResourceNodeType);
            var interactableResource = (IUnitInteractableTarget)resourceNode;

            commandAgent.Issue(new UnitCommand(
                UnitCommandMode.Interact,
                interactableResource.InteractionPoint,
                null,
                interactableResource,
                false));

            for (var i = 0; i < 120 && GetInt(wallet, "Minerals") < 10; i++)
            {
                yield return null;
            }

            Assert.That(GetInt(wallet, "Minerals"), Is.GreaterThanOrEqualTo(10));
            Assert.That(GetInt(resourceNode, "RemainingAmount"), Is.LessThanOrEqualTo(10));
            Assert.That(commandAgent.Mode, Is.EqualTo(UnitCommandMode.Interact));

            Object.Destroy(worker);
            Object.Destroy(resourceObject);
            Object.Destroy(dropOffObject);
            Object.Destroy(walletObject);
        }

        [UnityTest]
        public IEnumerator WorkerGatherController_StopsWithFailureReasonWhenNoDropOffExists()
        {
            LogAssert.Expect(LogType.Warning, "No available resource drop-off found for carried resources.");
            var resourceObject = CreateResourceNode(
                "NoDropOffMinerals",
                Enum.Parse(ResourceTypeType, "Minerals"),
                20,
                5,
                0f,
                new Vector3(-0.2f, 0f, 0f));
            var worker = CreateWorkerUnit("NoDropOffWorker", Vector3.zero);
            var commandAgent = worker.GetComponent<UnitCommandAgent>();
            var gatherController = worker.GetComponent(WorkerGatherControllerType);
            var resourceNode = resourceObject.GetComponent(ResourceNodeType);
            var interactableResource = (IUnitInteractableTarget)resourceNode;

            commandAgent.Issue(new UnitCommand(
                UnitCommandMode.Interact,
                interactableResource.InteractionPoint,
                null,
                interactableResource,
                false));

            for (var i = 0; i < 120 && commandAgent.Mode == UnitCommandMode.Interact; i++)
            {
                yield return null;
            }

            Assert.That(commandAgent.Mode, Is.EqualTo(UnitCommandMode.Idle));
            Assert.That((string)GetProperty(gatherController, "LastFailureReason"), Does.Contain("No available resource drop-off"));

            Object.Destroy(worker);
            Object.Destroy(resourceObject);
        }

        [UnityTest]
        public IEnumerator UnitProductionQueue_EnqueueSpendsCostAndSpawnsProducedUnit()
        {
            var walletObject = new GameObject("ProductionWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(100, 10));
            var productionBuilding = CreateProductionBuilding("ProductionBuilding", UnitTeam.Team1, Vector3.zero);
            var producedPrefab = CreateUnitPrefab("ProducedWorkerPrefab", PrototypeUnitType.Worker);
            var definition = Activator.CreateInstance(UnitProductionDefinitionType);
            Invoke(
                definition,
                "Configure",
                "Test Worker",
                PrototypeUnitType.Worker,
                producedPrefab,
                CreateResourceAmount(40, 5),
                0.1f);
            var definitions = Array.CreateInstance(UnitProductionDefinitionType, 1);
            definitions.SetValue(definition, 0);

            var queue = productionBuilding.AddComponent(UnitProductionQueueType);
            Invoke(
                queue,
                "Configure",
                wallet,
                null,
                definitions,
                2,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1f, 0f, 0f));

            Assert.That((bool)Invoke(queue, "TryEnqueue", PrototypeUnitType.Worker), Is.True);
            Assert.That(GetInt(wallet, "Minerals"), Is.EqualTo(60));
            Assert.That(GetInt(wallet, "Gas"), Is.EqualTo(5));
            Assert.That(GetInt(queue, "QueuedCount"), Is.EqualTo(1));

            for (var i = 0; i < 120 && GetInt(queue, "QueuedCount") > 0; i++)
            {
                yield return null;
            }

            yield return null;

            var producedUnits = Object.FindObjectsByType<PrototypeUnitStatus>(FindObjectsSortMode.None);
            Assert.That(producedUnits, Has.Some.Matches<PrototypeUnitStatus>(
                unit => unit != null
                    && unit.gameObject.scene.IsValid()
                    && unit.gameObject.name.StartsWith(producedPrefab.name)
                    && unit.Team == UnitTeam.Team1
                    && unit.UnitType == PrototypeUnitType.Worker));

            var producedNamePrefix = producedPrefab.name;
            Object.Destroy(producedPrefab);
            Object.Destroy(productionBuilding);
            Object.Destroy(walletObject);
            DestroyObjectsStartingWith(producedNamePrefix);
        }

        [UnityTest]
        public IEnumerator UnitProductionQueue_ReportsFailureReasonWhenResourcesAreInsufficient()
        {
            var walletObject = new GameObject("PoorProductionWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(10, 0));
            var productionBuilding = CreateProductionBuilding("PoorProductionBuilding", UnitTeam.Team1, Vector3.zero);
            var producedPrefab = CreateUnitPrefab("ExpensiveWorkerPrefab", PrototypeUnitType.Worker);
            var definition = Activator.CreateInstance(UnitProductionDefinitionType);
            Invoke(
                definition,
                "Configure",
                "Expensive Worker",
                PrototypeUnitType.Worker,
                producedPrefab,
                CreateResourceAmount(40, 5),
                0.1f);
            var definitions = Array.CreateInstance(UnitProductionDefinitionType, 1);
            definitions.SetValue(definition, 0);
            var queue = productionBuilding.AddComponent(UnitProductionQueueType);
            Invoke(
                queue,
                "Configure",
                wallet,
                null,
                definitions,
                2,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1f, 0f, 0f));

            LogAssert.Expect(
                LogType.Warning,
                "Insufficient resources for cost (Minerals: 40, Gas: 5). Current resources: Minerals: 10, Gas: 0.");
            LogAssert.Expect(
                LogType.Warning,
                "Cannot enqueue Expensive Worker: insufficient resources for cost (Minerals: 40, Gas: 5).");

            Assert.That((bool)Invoke(queue, "TryEnqueue", PrototypeUnitType.Worker), Is.False);
            Assert.That((string)GetProperty(queue, "LastEnqueueFailureReason"), Does.Contain("insufficient resources"));
            Assert.That((string)GetProperty(wallet, "LastFailureReason"), Does.Contain("Insufficient resources"));
            Assert.That(GetInt(wallet, "Minerals"), Is.EqualTo(10));
            Assert.That(GetInt(queue, "QueuedCount"), Is.EqualTo(0));

            Object.Destroy(producedPrefab);
            Object.Destroy(productionBuilding);
            Object.Destroy(walletObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitProductionQueue_TracksPendingProductionAndAdvancesInOrder()
        {
            var walletObject = new GameObject("QueuedProductionWallet");
            var wallet = walletObject.AddComponent(PlayerResourceWalletType);
            Invoke(wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(200, 0));
            var productionBuilding = CreateProductionBuilding("QueuedProductionBuilding", UnitTeam.Team1, Vector3.zero);
            var workerPrefab = CreateUnitPrefab("QueuedWorkerPrefab", PrototypeUnitType.Worker);
            var soldierPrefab = CreateUnitPrefab("QueuedSoldierPrefab", PrototypeUnitType.Soldier);
            var workerDefinition = Activator.CreateInstance(UnitProductionDefinitionType);
            Invoke(
                workerDefinition,
                "Configure",
                "Queued Worker",
                PrototypeUnitType.Worker,
                workerPrefab,
                CreateResourceAmount(10, 0),
                0.05f);
            var soldierDefinition = Activator.CreateInstance(UnitProductionDefinitionType);
            Invoke(
                soldierDefinition,
                "Configure",
                "Queued Soldier",
                PrototypeUnitType.Soldier,
                soldierPrefab,
                CreateResourceAmount(10, 0),
                10f);
            var definitions = Array.CreateInstance(UnitProductionDefinitionType, 2);
            definitions.SetValue(workerDefinition, 0);
            definitions.SetValue(soldierDefinition, 1);
            var queue = productionBuilding.AddComponent(UnitProductionQueueType);
            Invoke(
                queue,
                "Configure",
                wallet,
                null,
                definitions,
                3,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1f, 0f, 0f));

            Assert.That((bool)Invoke(queue, "TryEnqueue", PrototypeUnitType.Worker), Is.True);
            Assert.That((bool)Invoke(queue, "TryEnqueue", PrototypeUnitType.Soldier), Is.True);
            Assert.That(GetProperty(queue, "ActiveProduction"), Is.EqualTo(workerDefinition));
            Assert.That(GetInt(queue, "PendingCount"), Is.EqualTo(1));
            Assert.That(Invoke(queue, "GetPendingProduction", 0), Is.EqualTo(soldierDefinition));

            for (var i = 0; i < 120 && GetProperty(queue, "ActiveProduction") == workerDefinition; i++)
            {
                yield return null;
            }

            Assert.That(GetProperty(queue, "ActiveProduction"), Is.EqualTo(soldierDefinition));
            Assert.That(GetInt(queue, "PendingCount"), Is.EqualTo(0));
            Assert.That(GetInt(queue, "QueuedCount"), Is.EqualTo(1));

            var workerNamePrefix = workerPrefab.name;
            Object.Destroy(workerPrefab);
            Object.Destroy(soldierPrefab);
            Object.Destroy(productionBuilding);
            Object.Destroy(walletObject);
            DestroyObjectsStartingWith(workerNamePrefix);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitProductionQueue_UsesUpdatedTeamWalletAfterBuildingTeamChanges()
        {
            var team1WalletObject = new GameObject("ProductionTeam1Wallet");
            var team1Wallet = team1WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team1Wallet, "Initialize", UnitTeam.Team1, CreateResourceAmount(100, 0));
            var team2WalletObject = new GameObject("ProductionTeam2Wallet");
            var team2Wallet = team2WalletObject.AddComponent(PlayerResourceWalletType);
            Invoke(team2Wallet, "Initialize", UnitTeam.Team2, CreateResourceAmount(100, 0));
            var productionBuilding = CreateProductionBuilding("RetimedProductionBuilding", UnitTeam.Team1, Vector3.zero);
            var queue = productionBuilding.AddComponent(UnitProductionQueueType);
            var producedPrefab = CreateUnitPrefab("RetimedProducedWorkerPrefab", PrototypeUnitType.Worker);
            var definition = Activator.CreateInstance(UnitProductionDefinitionType);
            Invoke(
                definition,
                "Configure",
                "Retimed Worker",
                PrototypeUnitType.Worker,
                producedPrefab,
                CreateResourceAmount(40, 0),
                0.1f);
            var definitions = Array.CreateInstance(UnitProductionDefinitionType, 1);
            definitions.SetValue(definition, 0);
            Invoke(
                queue,
                "Configure",
                team1Wallet,
                null,
                definitions,
                2,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1f, 0f, 0f));
            var status = productionBuilding.GetComponent(BuildingStatusType);

            Invoke(status, "Initialize", UnitTeam.Team2, Enum.Parse(BuildingKindType, "Production"), Vector2Int.one, true);

            Assert.That((bool)Invoke(queue, "TryEnqueue", PrototypeUnitType.Worker), Is.True);
            Assert.That(GetInt(team1Wallet, "Minerals"), Is.EqualTo(100));
            Assert.That(GetInt(team2Wallet, "Minerals"), Is.EqualTo(60));

            Object.Destroy(producedPrefab);
            Object.Destroy(productionBuilding);
            Object.Destroy(team1WalletObject);
            Object.Destroy(team2WalletObject);
            yield return null;
        }

        private static GameObject CreateMovableUnit(string name, Vector3 position)
        {
            return CreateMovableUnit(name, position, UnitTeam.Team1);
        }

        private static GameObject CreateMovableUnit(string name, Vector3 position, UnitTeam team)
        {
            return CreateMovableUnit(
                name,
                position,
                team,
                PrototypeUnitType.Soldier,
                UnitRole.Combat,
                false);
        }

        private static GameObject CreateWorkerUnit(string name, Vector3 position)
        {
            var worker = CreateMovableUnit(
                name,
                position,
                UnitTeam.Team1,
                PrototypeUnitType.Worker,
                UnitRole.Resource | UnitRole.Builder,
                true);
            if (worker.GetComponent(WorkerGatherControllerType) == null)
            {
                worker.AddComponent(WorkerGatherControllerType);
            }

            return worker;
        }

        private static GameObject CreateMovableUnit(
            string name,
            Vector3 position,
            UnitTeam team,
            PrototypeUnitType unitType,
            UnitRole roles,
            bool canGatherResources)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;

            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                unitType,
                MovementDomain.Ground,
                roles,
                AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                100f,
                10f,
                0f,
                1.5f,
                5f,
                1f,
                MovementSpeed,
                1,
                Vector2Int.one,
                canGatherResources,
                false,
                0f);

            if (unit.GetComponent<UnitPathAgent>() == null)
            {
                unit.AddComponent<UnitPathAgent>();
            }

            return unit;
        }

        private static GameObject CreateDropOff(string name, UnitTeam team, Vector3 position)
        {
            var dropOff = new GameObject(name);
            dropOff.transform.position = position;
            var status = dropOff.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", team, Enum.Parse(BuildingKindType, "MainBase"), Vector2Int.one, true);
            dropOff.AddComponent(ResourceDropOffType);
            return dropOff;
        }

        private static GameObject CreateResourceNode(
            string name,
            object type,
            int amount,
            int gatherPerTrip,
            float gatherDuration,
            Vector3 position)
        {
            var resourceObject = new GameObject(name);
            resourceObject.transform.position = position;
            var resourceNode = resourceObject.AddComponent(ResourceNodeType);
            Invoke(resourceNode, "Configure", type, amount, gatherPerTrip, gatherDuration, 1f, false);
            return resourceObject;
        }

        private static Sprite CreateTestSprite(int width, int height, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

        private static GameObject CreateProductionBuilding(string name, UnitTeam team, Vector3 position)
        {
            var building = new GameObject(name);
            building.transform.position = position;
            var status = building.AddComponent(BuildingStatusType);
            Invoke(status, "Initialize", team, Enum.Parse(BuildingKindType, "Production"), Vector2Int.one, true);
            return building;
        }

        private static GameObject CreateUnitPrefab(string name, PrototypeUnitType unitType)
        {
            var prefab = CreateMovableUnit(
                name,
                new Vector3(100f, 100f, 0f),
                UnitTeam.Team2,
                unitType,
                UnitRole.Resource | UnitRole.Builder,
                true);
            prefab.SetActive(false);
            return prefab;
        }

        private static void DestroyObjectsStartingWith(string namePrefix)
        {
            var statuses = Object.FindObjectsByType<PrototypeUnitStatus>(FindObjectsSortMode.None);
            foreach (var status in statuses)
            {
                if (status != null
                    && status.gameObject.scene.IsValid()
                    && status.gameObject.name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    Object.Destroy(status.gameObject);
                }
            }
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

        private static object CreateResourceAmount(int minerals, int gas)
        {
            return Activator.CreateInstance(ResourceAmountType, minerals, gas);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = FindMethod(target.GetType(), methodName, arguments);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(Type targetType, string methodName, params object[] arguments)
        {
            var method = FindMethod(targetType, methodName, arguments);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {targetType.Name}.{methodName}.");
            return method.Invoke(null, arguments);
        }

        private static object GetFirstPreviewCell(IEnumerable previewCells)
        {
            Assert.That(previewCells, Is.Not.Null);
            var enumerator = previewCells.GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True);
            return enumerator.Current;
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

        private static bool GetBool(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"Could not resolve field {target.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(target);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Could not resolve field {target.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(target);
        }

        private static object GetProperty(object target, string propertyName)
        {
            if (target is Type targetType)
            {
                var staticProperty = targetType.GetProperty(
                    propertyName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                Assert.That(staticProperty, Is.Not.Null, $"Could not resolve property {targetType.Name}.{propertyName}.");
                return staticProperty.GetValue(null);
            }

            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not resolve property {target.GetType().Name}.{propertyName}.");
            return property.GetValue(target);
        }
    }
}
