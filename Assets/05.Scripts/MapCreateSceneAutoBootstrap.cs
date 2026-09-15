using System.Collections;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.UI;
using ProjectS.Units;
using ProjectS.Unlocks;
using ProjectS.Upgrades;
using ProjectS.Visibility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectS
{
    // Scene-local smoke test bootstrap for the first playable RTS loop.
    public sealed class MapCreateSceneAutoBootstrap : MonoBehaviour
    {
        private const string TargetSceneName = "MapCreate_Scene";
        private const string SetupRootName = "ProjectS Match Test Setup";
        private const string ResourceLayerName = "Resource";
        private const string ResourceNodesSpriteResourcePath = "ResourceNodes";
        private const string MineralsSpriteName = "ResourceNodes_Minerals";
        private const string GasSpriteName = "ResourceNodes_Gas";
        private const int ResourceSortingOrder = 12;
        private const int UnitSortingOrder = 20;
        private const int MainBaseSupplyProvided = 20;

        private static Sprite squareSprite;
        private static Sprite mineralsResourceSprite;
        private static Sprite gasResourceSprite;
        private static bool resourceSpritesLoaded;

        [Header("Unit Prefabs")]
        [SerializeField] private GameObject workerPrefab;
        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private GameObject spliterPrefab;
        [SerializeField] private GameObject rangerPrefab;
        [SerializeField] private GameObject tankPrefab;
        [SerializeField] private GameObject strikerPrefab;
        [SerializeField] private GameObject swarmPrefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (SceneManager.GetActiveScene().name != TargetSceneName
                || FindFirstObjectByType<MapCreateSceneAutoBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("MapCreate Scene Auto Bootstrap");
            bootstrapObject.AddComponent<MapCreateSceneAutoBootstrap>();
        }

        private IEnumerator Start()
        {
            yield return null;

            EnsureGameplayPresentation();

            var buildingPrefabs = BuildingPrefabCatalog.Load();
            var catalogFailureReason = string.Empty;
            if (buildingPrefabs == null || !buildingPrefabs.TryValidate(out catalogFailureReason))
            {
                Debug.LogError(
                    $"MapCreate runtime setup requires Resources/{BuildingPrefabCatalog.ResourcesPath}.asset. "
                    + (buildingPrefabs == null ? "The catalog asset was not found." : catalogFailureReason));
                Destroy(gameObject);
                yield break;
            }

            var existingRoot = GameObject.Find(SetupRootName);
            if (existingRoot != null)
            {
                UpgradeExistingSetup(existingRoot.transform, buildingPrefabs);
                Destroy(gameObject);
                yield break;
            }

            BuildTestSetup(buildingPrefabs);
            Destroy(gameObject);
        }

        private static void EnsureGameplayPresentation()
        {
            var tilemapWorld = ProjectSTilemapWorld.ActiveInstance
                ?? FindFirstObjectByType<ProjectSTilemapWorld>();

            var fog = FindFirstObjectByType<FogOfWarManager>();
            if (fog == null)
            {
                fog = new GameObject("Fog Of War").AddComponent<FogOfWarManager>();
            }

            if (tilemapWorld != null)
            {
                fog.Configure(tilemapWorld, UnitTeam.Team1);
            }

            if (FindFirstObjectByType<RtsMinimap>() == null)
            {
                new GameObject("RtsMinimap").AddComponent<RtsMinimap>();
            }
        }

        private void UpgradeExistingSetup(Transform root, BuildingPrefabCatalog buildingPrefabs)
        {
            EnsureSupplyManagers(root);
            EnsureTeamUpgradeResearch(root, UnitTeam.Team1);
            EnsureTeamUpgradeResearch(root, UnitTeam.Team2);
            EnsureTeamUnlockState(root, UnitTeam.Team1);
            EnsureTeamUnlockState(root, UnitTeam.Team2);
            GetStartPositions(ProjectSTilemapWorld.ActiveInstance, out var playerStart, out var aiStart);
            if (!ResourceTilemapNodeSynchronizer.SceneHasResourceTiles())
            {
                CreateExpansionResourceClusters(playerStart, aiStart, root, ProjectSTilemapWorld.ActiveInstance);
            }
            var templates = root.Find("Runtime Building Templates");
            if (templates == null)
            {
                var templatesObject = CreateChild(root, "Runtime Building Templates");
                templatesObject.SetActive(false);
                templates = templatesObject.transform;
            }

            var placementService = FindFirstObjectByType<BuildingPlacementService>();
            if (placementService == null)
            {
                return;
            }

            var spliterProductionTemplate = CreateBuildingTemplate(
                templates,
                "Spliter Production Building Template",
                buildingPrefabs.GetPrefab(BuildingKind.SpliterProduction));
            var autoTurretTemplate = CreateBuildingTemplate(
                templates,
                "Auto Turret Building Template",
                buildingPrefabs.GetPrefab(BuildingKind.AutoTurret));
            var speedAuraTemplate = CreateBuildingTemplate(
                templates,
                "Speed Aura Building Template",
                buildingPrefabs.GetPrefab(BuildingKind.SpeedAura));
            var mainBaseTemplate = CreateBuildingTemplate(
                templates,
                "Main Base Building Template",
                buildingPrefabs.GetPrefab(BuildingKind.MainBase));

            placementService.ConfigureBuildOptions(
                ConfigureProductionTemplate(
                    spliterProductionTemplate,
                    GetRequiredUnitPrefab(PrototypeUnitType.Spliter),
                    new[] { CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, GetRequiredUnitPrefab(PrototypeUnitType.Spliter), new ResourceAmount(125, 0), 8f, 3, allowedProductionBuildings: new[] { BuildingKind.SpliterProduction }) },
                    new Vector3(2.5f, -0.5f, 0f),
                    new Vector3(5f, -1f, 0f)),
                autoTurretTemplate,
                speedAuraTemplate,
                mainBasePrefab: ConfigureProductionTemplate(
                    mainBaseTemplate,
                    GetRequiredUnitPrefab(PrototypeUnitType.Worker),
                    new[] { CreateProductionDefinition("Worker", PrototypeUnitType.Worker, GetRequiredUnitPrefab(PrototypeUnitType.Worker), new ResourceAmount(50, 0), 5f, 1, allowedProductionBuildings: new[] { BuildingKind.MainBase }) },
                    new Vector3(2.5f, -1.5f, 0f),
                    new Vector3(5f, -2f, 0f)));
        }

        private static GameObject CreateBuildingTemplate(Transform parent, string name, GameObject prefab)
        {
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Runtime building prefab is missing for template {name}.");
            }

            var existing = parent.Find(name);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var template = Instantiate(prefab, parent);
            template.name = name;
            template.SetActive(false);
            return template;
        }

        private static GameObject ConfigureProductionTemplate(
            GameObject template,
            GameObject requiredPrefab,
            UnitProductionDefinition[] definitions,
            Vector3 spawnOffset,
            Vector3 rallyOffset)
        {
            if (template == null || requiredPrefab == null)
            {
                return template;
            }

            var queue = template.GetComponent<UnitProductionQueue>();
            if (queue != null)
            {
                queue.Configure(null, ProjectSTilemapWorld.ActiveInstance, definitions, 5, spawnOffset, rallyOffset);
            }

            return template;
        }

        private void BuildTestSetup(BuildingPrefabCatalog buildingPrefabs)
        {
            var tilemapWorld = ProjectSTilemapWorld.ActiveInstance ?? FindFirstObjectByType<ProjectSTilemapWorld>();
            var root = new GameObject(SetupRootName);
            var prototypeRoot = CreateChild(root.transform, "Runtime Prototypes");
            prototypeRoot.SetActive(false);

            GetStartPositions(tilemapWorld, out var playerStart, out var aiStart);
            var playerWallet = CreateWallet("Player Wallet", UnitTeam.Team1, new ResourceAmount(100, 0), root.transform);
            var aiWallet = CreateWallet("AI Wallet", UnitTeam.Team2, new ResourceAmount(100, 0), root.transform);
            CreateTeamUpgradeResearch("Player Unit Upgrades", UnitTeam.Team1, playerWallet, root.transform);
            CreateTeamUpgradeResearch("AI Unit Upgrades", UnitTeam.Team2, aiWallet, root.transform);
            CreateTeamUnlockState("Player Unlocks", UnitTeam.Team1, root.transform);
            CreateTeamUnlockState("AI Unlocks", UnitTeam.Team2, root.transform);
            EnsureSupplyManagers(root.transform);

            var workerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Worker);
            var soldierUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Soldier);
            var spliterUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Spliter);
            var rangerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Ranger);
            var tankUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Tank);
            var strikerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Striker);
            var swarmUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Swarm);

            var mainBasePrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Main Base Prototype",
                buildingPrefabs.GetPrefab(BuildingKind.MainBase));
            var productionPrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Production Building Prototype",
                buildingPrefabs.GetPrefab(BuildingKind.Production));
            var spliterProductionPrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Spliter Production Building Prototype",
                buildingPrefabs.GetPrefab(BuildingKind.SpliterProduction));
            var autoTurretPrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Auto Turret Building Prototype",
                buildingPrefabs.GetPrefab(BuildingKind.AutoTurret));
            var speedAuraPrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Speed Aura Building Prototype",
                buildingPrefabs.GetPrefab(BuildingKind.SpeedAura));
            var constructionPrototype = CreateBuildingTemplate(
                prototypeRoot.transform,
                "Construction Site Prototype",
                buildingPrefabs.ConstructionSitePrefab);

            var workerDefinitions = new[]
            {
                CreateProductionDefinition("Worker", PrototypeUnitType.Worker, workerUnitPrefab, new ResourceAmount(50, 0), 5f, 1, allowedProductionBuildings: new[] { BuildingKind.MainBase })
            };
            var combatDefinitions = new[]
            {
                CreateProductionDefinition("Soldier", PrototypeUnitType.Soldier, soldierUnitPrefab, new ResourceAmount(100, 0), 7f, 2, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Ranger", PrototypeUnitType.Ranger, rangerUnitPrefab, new ResourceAmount(100, 25), 8f, 2, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Tank", PrototypeUnitType.Tank, tankUnitPrefab, new ResourceAmount(150, 0), 10f, 3, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Striker", PrototypeUnitType.Striker, strikerUnitPrefab, new ResourceAmount(75, 0), 6f, 1, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Swarm x3", PrototypeUnitType.Swarm, swarmUnitPrefab, new ResourceAmount(120, 0), 8f, 1, 3, allowedProductionBuildings: new[] { BuildingKind.Production })
            };
            var spliterDefinitions = new[] { CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, spliterUnitPrefab, new ResourceAmount(125, 0), 8f, 3, allowedProductionBuildings: new[] { BuildingKind.SpliterProduction }) };

            ConfigureProductionTemplate(mainBasePrototype, workerUnitPrefab, workerDefinitions, new Vector3(2.5f, -1.5f, 0f), new Vector3(5f, -2f, 0f));

            InstantiateBuilding(mainBasePrototype, "Player Main Base", UnitTeam.Team1, BuildingKind.MainBase, playerStart, playerWallet, tilemapWorld, workerDefinitions, new Vector3(2.5f, -1.5f, 0f), new Vector3(5f, -2f, 0f), root.transform);
            InstantiateBuilding(mainBasePrototype, "AI Main Base", UnitTeam.Team2, BuildingKind.MainBase, aiStart, aiWallet, tilemapWorld, workerDefinitions, new Vector3(-2.5f, 1.5f, 0f), new Vector3(-5f, 2f, 0f), root.transform);

            if (!ResourceTilemapNodeSynchronizer.SceneHasResourceTiles())
            {
                CreateResourceCluster(playerStart + new Vector3(-3f, -3f, 0f), root.transform, tilemapWorld);
                CreateResourceCluster(aiStart + new Vector3(3f, 3f, 0f), root.transform, tilemapWorld);
                CreateExpansionResourceClusters(playerStart, aiStart, root.transform, tilemapWorld);
            }
            CreateStartingUnits(UnitTeam.Team1, playerStart, workerUnitPrefab, root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team2, aiStart, workerUnitPrefab, root.transform, tilemapWorld);
            CreatePlayerSystems(
                playerWallet,
                tilemapWorld,
                constructionPrototype,
                productionPrototype,
                spliterProductionPrototype,
                autoTurretPrototype,
                speedAuraPrototype,
                mainBasePrototype,
                combatDefinitions,
                spliterDefinitions,
                root.transform);
            CreateAiController(
                playerStart,
                aiWallet,
                tilemapWorld,
                constructionPrototype,
                productionPrototype,
                spliterProductionPrototype,
                autoTurretPrototype,
                speedAuraPrototype,
                mainBasePrototype,
                root.transform);
        }

        private GameObject GetRequiredUnitPrefab(PrototypeUnitType unitType)
        {
            GameObject prefab;
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    prefab = workerPrefab;
                    break;
                case PrototypeUnitType.Soldier:
                    prefab = soldierPrefab;
                    break;
                case PrototypeUnitType.Spliter:
                    prefab = spliterPrefab;
                    break;
                case PrototypeUnitType.Ranger:
                    prefab = rangerPrefab;
                    break;
                case PrototypeUnitType.Tank:
                    prefab = tankPrefab;
                    break;
                case PrototypeUnitType.Striker:
                    prefab = strikerPrefab;
                    break;
                case PrototypeUnitType.Swarm:
                    prefab = swarmPrefab;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(unitType), unitType, null);
            }

            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Missing B_{unitType} prefab reference on {name}.");
            }

            return prefab;
        }

        private static void CreatePlayerSystems(
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionPrototype,
            GameObject productionPrototype,
            GameObject spliterProductionPrototype,
            GameObject autoTurretPrototype,
            GameObject speedAuraPrototype,
            GameObject mainBasePrototype,
            UnitProductionDefinition[] combatDefinitions,
            UnitProductionDefinition[] spliterDefinitions,
            Transform parent)
        {
            var commandController = PlayerUnitCommandController.ActiveInstance ?? FindFirstObjectByType<PlayerUnitCommandController>();
            if (commandController == null)
            {
                commandController = CreateChild(parent, "Player Runtime Systems").AddComponent<PlayerUnitCommandController>();
            }

            var workerAutoAssignment = commandController.GetComponent<WorkerAutoAssignmentManager>();
            if (workerAutoAssignment == null)
            {
                workerAutoAssignment = commandController.gameObject.AddComponent<WorkerAutoAssignmentManager>();
            }

            workerAutoAssignment.Configure(UnitTeam.Team1, false);

            var placementService = commandController.GetComponent<BuildingPlacementService>();
            if (placementService == null)
            {
                placementService = commandController.gameObject.AddComponent<BuildingPlacementService>();
            }

            placementService.Configure(
                UnitTeam.Team1,
                wallet,
                tilemapWorld,
                constructionPrototype,
                productionPrototype,
                BuildingKind.Production,
                new ResourceAmount(150, 0),
                8f,
                new Vector2Int(2, 2));

            ConfigureProductionTemplate(
                productionPrototype,
                combatDefinitions != null && combatDefinitions.Length > 0 ? combatDefinitions[0]?.UnitPrefab : null,
                combatDefinitions,
                new Vector3(2.5f, -0.5f, 0f),
                new Vector3(5f, -1f, 0f));
            ConfigureProductionTemplate(
                spliterProductionPrototype,
                spliterDefinitions != null && spliterDefinitions.Length > 0 ? spliterDefinitions[0]?.UnitPrefab : null,
                spliterDefinitions,
                new Vector3(2.5f, -0.5f, 0f),
                new Vector3(5f, -1f, 0f));
            placementService.ConfigureBuildOptions(
                spliterProductionPrototype,
                autoTurretPrototype,
                speedAuraPrototype,
                mainBasePrefab: mainBasePrototype);

            var hud = FindFirstObjectByType<RtsGameHud>();
            if (hud == null)
            {
                hud = commandController.gameObject.AddComponent<RtsGameHud>();
            }

            hud.Configure(UnitTeam.Team1, placementService);
            EnsureMatchController(parent);
        }

        private static void CreateAiController(
            Vector3 fallbackAttackPoint,
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionPrototype,
            GameObject productionPrototype,
            GameObject spliterProductionPrototype,
            GameObject autoTurretPrototype,
            GameObject speedAuraPrototype,
            GameObject mainBasePrototype,
            Transform parent)
        {
            var aiObject = CreateChild(parent, "Simple Skirmish AI");
            var ai = aiObject.AddComponent<SimpleSkirmishAI>();
            ai.Configure(UnitTeam.Team2, UnitTeam.Team1, 3, 7, fallbackAttackPoint);
            ai.ConfigureTempo(1.5f, 18f);

            var templates = aiObject.AddComponent<AiBuildingTemplateRegistry>();
            templates.Configure(UnitTeam.Team2, wallet, tilemapWorld, constructionPrototype);
            templates.RegisterTemplate(BuildingKind.Production, productionPrototype, new ResourceAmount(150, 0), 8f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.SpliterProduction, spliterProductionPrototype, new ResourceAmount(175, 0), 9f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.AutoTurret, autoTurretPrototype, new ResourceAmount(125, 0), 7f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.SpeedAura, speedAuraPrototype, new ResourceAmount(125, 25), 7f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.MainBase, mainBasePrototype, new ResourceAmount(350, 75), 12f, new Vector2Int(3, 3));
        }

        private static void EnsureMatchController(Transform parent)
        {
            var matchController = FindFirstObjectByType<RtsMatchController>();
            if (matchController == null)
            {
                matchController = CreateChild(parent, "RTS Match Controller").AddComponent<RtsMatchController>();
            }

            matchController.Configure(UnitTeam.Team1, UnitTeam.Team2);
        }

        private static PlayerResourceWallet CreateWallet(string name, UnitTeam team, ResourceAmount resources, Transform parent)
        {
            var walletObject = CreateChild(parent, name);
            var wallet = walletObject.AddComponent<PlayerResourceWallet>();
            wallet.Initialize(team, resources);
            return wallet;
        }

        private static void CreateTeamUpgradeResearch(
            string name,
            UnitTeam team,
            PlayerResourceWallet wallet,
            Transform parent)
        {
            if (TeamUpgradeResearch.FindForTeam(team) != null)
            {
                return;
            }

            var researchObject = CreateChild(parent, name);
            var research = researchObject.AddComponent<TeamUpgradeResearch>();
            research.Configure(team, wallet, CreateMvpUnitUpgradeDefinitions());
        }

        private static void EnsureTeamUpgradeResearch(Transform parent, UnitTeam team)
        {
            if (TeamUpgradeResearch.FindForTeam(team) == null)
            {
                var wallet = PlayerResourceWallet.FindForTeam(team);
                if (wallet != null)
                {
                    CreateTeamUpgradeResearch($"{team} Unit Upgrades", team, wallet, parent);
                }
            }
        }

        private static void CreateTeamUnlockState(string name, UnitTeam team, Transform parent)
        {
            if (TeamUnlockState.FindForTeam(team) != null)
            {
                return;
            }

            var state = CreateChild(parent, name).AddComponent<TeamUnlockState>();
            state.Configure(team);
        }

        private static void EnsureTeamUnlockState(Transform parent, UnitTeam team)
        {
            if (TeamUnlockState.FindForTeam(team) == null)
            {
                CreateTeamUnlockState($"{team} Unlocks", team, parent);
            }
        }

        private static UnitUpgradeDefinition[] CreateMvpUnitUpgradeDefinitions()
        {
            var weaponUpgrade = ScriptableObject.CreateInstance<UnitUpgradeDefinition>();
            weaponUpgrade.name = "Weapon Calibration";
            weaponUpgrade.Configure(
                "Weapon Calibration (+3 ATK)",
                UnitUpgradeKind.AttackDamage,
                new ResourceAmount(100, 25),
                10f,
                3f);

            var mobilityUpgrade = ScriptableObject.CreateInstance<UnitUpgradeDefinition>();
            mobilityUpgrade.name = "Mobility Tuning";
            mobilityUpgrade.Configure(
                "Mobility Tuning (+15% Move)",
                UnitUpgradeKind.MovementSpeed,
                new ResourceAmount(75, 25),
                8f,
                0.15f);

            return new[] { weaponUpgrade, mobilityUpgrade };
        }

        private static void EnsureSupplyManagers(Transform parent)
        {
            EnsureSupplyManager(parent, "Player Supply", UnitTeam.Team1);
            EnsureSupplyManager(parent, "AI Supply", UnitTeam.Team2);

            var buildings = FindObjectsByType<BuildingStatus>(FindObjectsSortMode.None);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Kind == BuildingKind.MainBase)
                {
                    buildings[i].ConfigureSupplyProvided(MainBaseSupplyProvided);
                }
            }

            var units = FindObjectsByType<PrototypeUnitStatus>(FindObjectsSortMode.None);
            for (var i = 0; i < units.Length; i++)
            {
                units[i].ConfigureSupplyCost(GetSupplyCost(units[i].UnitType));
            }
        }

        private static void EnsureSupplyManager(Transform parent, string name, UnitTeam team)
        {
            var supplyManager = SupplyManager.FindForTeam(team);
            if (supplyManager == null)
            {
                var managerObject = CreateChild(parent, name);
                supplyManager = managerObject.AddComponent<SupplyManager>();
            }

            supplyManager.Initialize(team);
        }

        private static void InstantiateBuilding(
            GameObject prototype,
            string name,
            UnitTeam team,
            BuildingKind kind,
            Vector3 position,
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            UnitProductionDefinition[] definitions,
            Vector3 spawnOffset,
            Vector3 rallyOffset,
            Transform parent)
        {
            var building = Instantiate(prototype, position, Quaternion.identity, parent);
            building.name = name;
            building.SetActive(true);

            var status = building.GetComponent<BuildingStatus>();
            if (status != null)
            {
                status.Initialize(team, kind, status.Footprint, true);
            }

            var productionQueue = building.GetComponent<UnitProductionQueue>();
            if (productionQueue != null)
            {
                productionQueue.Configure(wallet, tilemapWorld, definitions, 5, spawnOffset, rallyOffset);
            }
        }

        private static void CreateResourceCluster(Vector3 center, Transform parent, ProjectSTilemapWorld tilemapWorld)
        {
            CreateResourceCluster("Home", center, parent, tilemapWorld);
        }

        private static void CreateExpansionResourceClusters(
            Vector3 playerStart,
            Vector3 aiStart,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var center = Vector3.Lerp(playerStart, aiStart, 0.5f);
            var direction = aiStart - playerStart;
            var lateral = new Vector3(-direction.y, direction.x, 0f).normalized * 6f;
            CreateExpansionResourceClusterIfMissing("Expansion Alpha", center + lateral, parent, tilemapWorld);
            CreateExpansionResourceClusterIfMissing("Expansion Beta", center - lateral, parent, tilemapWorld);
        }

        private static void CreateExpansionResourceClusterIfMissing(
            string label,
            Vector3 center,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            if (parent.Find($"{label} Mineral Field 1") != null)
            {
                return;
            }

            CreateResourceCluster(label, Snap(tilemapWorld, center), parent, tilemapWorld);
        }

        private static void CreateResourceCluster(
            string label,
            Vector3 center,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var offsets = new[]
            {
                new Vector3(-1.5f, 0f, 0f),
                new Vector3(0f, 0.8f, 0f),
                new Vector3(1.5f, 0f, 0f),
                new Vector3(0f, -0.8f, 0f)
            };

            for (var i = 0; i < offsets.Length; i++)
            {
                CreateResourceNode($"{label} Mineral Field {i + 1}", ResourceType.Minerals, center + offsets[i], parent, tilemapWorld);
            }

            CreateResourceNode($"{label} Vespene Geyser", ResourceType.Gas, center + new Vector3(3f, 0f, 0f), parent, tilemapWorld);
        }

        private static void CreateResourceNode(
            string name,
            ResourceType type,
            Vector3 position,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var nodeObject = CreateChild(parent, name);
            nodeObject.transform.position = Snap(tilemapWorld, position);
            SetLayerIfExists(nodeObject, ResourceLayerName);
            var renderer = nodeObject.AddComponent<SpriteRenderer>();
            var resourceSprite = GetResourceNodeSprite(type);
            renderer.sprite = resourceSprite ?? (squareSprite ??= CreateSquareSprite());
            renderer.color = resourceSprite != null
                ? Color.white
                : type == ResourceType.Minerals
                    ? new Color(0.25f, 0.85f, 0.95f, 1f)
                    : new Color(0.4f, 0.9f, 0.35f, 1f);
            renderer.sortingOrder = ResourceSortingOrder;
            ScaleResourceNodeVisual(nodeObject.transform, resourceSprite, type);

            var collider = nodeObject.AddComponent<BoxCollider2D>();
            collider.size = type == ResourceType.Minerals ? new Vector2(1f, 0.8f) : new Vector2(1.2f, 1.2f);
            collider.isTrigger = true;

            var node = nodeObject.AddComponent<ResourceNode>();
            node.Configure(
                type,
                type == ResourceType.Minerals ? 1500 : 2500,
                type == ResourceType.Minerals ? 8 : 6,
                type == ResourceType.Minerals ? 1.2f : 1.8f,
                type == ResourceType.Minerals ? 0.95f : 1.05f,
                true);
        }

        private static Sprite GetResourceNodeSprite(ResourceType type)
        {
            if (!resourceSpritesLoaded)
            {
                var sprites = UnityEngine.Resources.LoadAll<Sprite>(ResourceNodesSpriteResourcePath);
                for (var i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i].name == MineralsSpriteName)
                    {
                        mineralsResourceSprite = sprites[i];
                    }
                    else if (sprites[i].name == GasSpriteName)
                    {
                        gasResourceSprite = sprites[i];
                    }
                }

                resourceSpritesLoaded = true;
            }

            return type == ResourceType.Gas ? gasResourceSprite : mineralsResourceSprite;
        }

        private static void ScaleResourceNodeVisual(Transform nodeTransform, Sprite sprite, ResourceType type)
        {
            if (nodeTransform == null || sprite == null)
            {
                return;
            }

            var largestDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (largestDimension <= 0f)
            {
                return;
            }

            var desiredSize = type == ResourceType.Gas ? 1.35f : 1.2f;
            nodeTransform.localScale = Vector3.one * (desiredSize / largestDimension);
        }

        private static void SetLayerIfExists(GameObject target, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (target != null && layer >= 0)
            {
                target.layer = layer;
            }
        }

        private static void CreateStartingUnits(
            UnitTeam team,
            Vector3 start,
            GameObject workerPrototype,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var ySign = team == UnitTeam.Team1 ? -1f : 1f;
            var xSign = team == UnitTeam.Team1 ? 1f : -1f;
            InstantiateUnit(workerPrototype, $"{team} Worker 1", team, start + new Vector3(-1.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrototype, $"{team} Worker 2", team, start + new Vector3(-0.5f * xSign, -2.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrototype, $"{team} Worker 3", team, start + new Vector3(0.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrototype, $"{team} Worker 4", team, start + new Vector3(1.5f * xSign, -2.5f * ySign, 0f), parent, tilemapWorld);
        }

        private static void InstantiateUnit(
            GameObject prototype,
            string name,
            UnitTeam team,
            Vector3 position,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var unit = Instantiate(prototype, Snap(tilemapWorld, position), Quaternion.identity, parent);
            unit.name = name;
            var status = unit.GetComponent<PrototypeUnitStatus>();
            if (status != null)
            {
                status.SetTeam(team);
            }

            unit.SetActive(true);
        }

        private static UnitProductionDefinition CreateProductionDefinition(
            string displayName,
            PrototypeUnitType unitType,
            GameObject prefab,
            ResourceAmount cost,
            float duration,
            int supplyCost,
            int outputCount = 1,
            BuildingKind[] allowedProductionBuildings = null)
        {
            var definition = new UnitProductionDefinition();
            definition.Configure(displayName, unitType, prefab, cost, duration, supplyCost, outputCount);
            definition.ConfigureAllowedProductionBuildings(allowedProductionBuildings);
            return definition;
        }

        private static int GetSupplyCost(PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker: return 1;
                case PrototypeUnitType.Soldier: return 2;
                case PrototypeUnitType.Spliter: return 3;
                case PrototypeUnitType.Ranger: return 2;
                case PrototypeUnitType.Tank: return 3;
                case PrototypeUnitType.Striker:
                case PrototypeUnitType.Swarm: return 1;
                default: return 0;
            }
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Vector3 Snap(ProjectSTilemapWorld tilemapWorld, Vector3 position)
        {
            return tilemapWorld != null
                ? tilemapWorld.GetCellCenterWorld(tilemapWorld.WorldToCell(ClampToBounds(tilemapWorld, position)))
                : position;
        }

        private static void GetStartPositions(
            ProjectSTilemapWorld tilemapWorld,
            out Vector3 playerStart,
            out Vector3 aiStart)
        {
            if (tilemapWorld == null)
            {
                playerStart = new Vector3(-28f, 13f, 0f);
                aiStart = new Vector3(22f, -37f, 0f);
                return;
            }

            var bounds = tilemapWorld.CellBounds;
            var padding = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.y) * 0.12f, 5f, 8f);
            playerStart = Snap(tilemapWorld, new Vector3(bounds.xMin + padding, bounds.yMax - padding, 0f));
            aiStart = Snap(tilemapWorld, new Vector3(bounds.xMax - padding, bounds.yMin + padding, 0f));
        }

        private static Vector3 ClampToBounds(ProjectSTilemapWorld tilemapWorld, Vector3 position)
        {
            var bounds = tilemapWorld.CellBounds;
            var minX = bounds.xMin + 1f;
            var maxX = bounds.xMax - 1f;
            var minY = bounds.yMin + 1f;
            var maxY = bounds.yMax - 1f;
            return new Vector3(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY),
                position.z);
        }

        private static Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

    }
}
