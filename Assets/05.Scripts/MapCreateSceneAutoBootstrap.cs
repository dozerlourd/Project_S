using System.Collections;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.UI;
using ProjectS.Units;
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
        private const int ResourceSortingOrder = 12;
        private const int UnitSortingOrder = 20;
        private const int MainBaseSupplyProvided = 20;

        private static Sprite squareSprite;

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

            var existingRoot = GameObject.Find(SetupRootName);
            if (existingRoot != null)
            {
                UpgradeExistingSetup(existingRoot.transform);
                Destroy(gameObject);
                yield break;
            }

            BuildTestSetup();
            Destroy(gameObject);
        }

        private void UpgradeExistingSetup(Transform root)
        {
            EnsureSupplyManagers(root);
            var placementService = FindFirstObjectByType<BuildingPlacementService>();
            if (placementService == null)
            {
                return;
            }

            var templates = root.Find("Runtime Building Templates");
            if (templates == null)
            {
                var templatesObject = CreateChild(root, "Runtime Building Templates");
                templatesObject.SetActive(false);
                templates = templatesObject.transform;
            }

            placementService.ConfigureBuildOptions(
                ConfigureProductionTemplate(
                    FindOrCreateBuildingTemplate(templates, "Spliter Production Building Template", BuildingKind.SpliterProduction, true, new Vector2(2.5f, 2.5f), new Color(0.45f, 0.2f, 0.66f, 1f)),
                    GetRequiredUnitPrefab(PrototypeUnitType.Spliter),
                    new[] { CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, GetRequiredUnitPrefab(PrototypeUnitType.Spliter), new ResourceAmount(125, 0), 8f, 3) },
                    new Vector3(2.5f, -0.5f, 0f),
                    new Vector3(5f, -1f, 0f)),
                FindOrCreateBuildingTemplate(templates, "Auto Turret Building Template", BuildingKind.AutoTurret, false, new Vector2(2.3f, 2.3f), new Color(0.38f, 0.34f, 0.34f, 1f)),
                FindOrCreateBuildingTemplate(templates, "Speed Aura Building Template", BuildingKind.SpeedAura, false, new Vector2(2.6f, 2.6f), new Color(0.18f, 0.66f, 0.75f, 1f)));
        }

        private static GameObject FindOrCreateBuildingTemplate(Transform parent, string name, BuildingKind kind, bool hasProductionQueue, Vector2 size, Color color)
        {
            var existing = parent.Find(name);
            return existing != null
                ? existing.gameObject
                : CreateBuildingPrototype(name, kind, false, hasProductionQueue, size, color, parent);
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

        private void BuildTestSetup()
        {
            var tilemapWorld = ProjectSTilemapWorld.ActiveInstance ?? FindFirstObjectByType<ProjectSTilemapWorld>();
            var root = new GameObject(SetupRootName);
            var prototypeRoot = CreateChild(root.transform, "Runtime Prototypes");
            prototypeRoot.SetActive(false);

            GetStartPositions(tilemapWorld, out var playerStart, out var aiStart);
            var playerWallet = CreateWallet("Player Wallet", UnitTeam.Team1, new ResourceAmount(700, 100), root.transform);
            var aiWallet = CreateWallet("AI Wallet", UnitTeam.Team2, new ResourceAmount(700, 100), root.transform);
            EnsureSupplyManagers(root.transform);

            var workerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Worker);
            var soldierUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Soldier);
            var spliterUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Spliter);
            var rangerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Ranger);
            var tankUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Tank);
            var strikerUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Striker);
            var swarmUnitPrefab = GetRequiredUnitPrefab(PrototypeUnitType.Swarm);

            var mainBasePrototype = CreateBuildingPrototype(
                "Main Base Prototype",
                BuildingKind.MainBase,
                true,
                true,
                new Vector2(2.6f, 2.2f),
                new Color(0.28f, 0.52f, 0.76f, 1f),
                prototypeRoot.transform);
            var productionPrototype = CreateBuildingPrototype(
                "Production Building Prototype",
                BuildingKind.Production,
                false,
                true,
                new Vector2(2.4f, 2f),
                new Color(0.48f, 0.36f, 0.68f, 1f),
                prototypeRoot.transform);
            var spliterProductionPrototype = CreateBuildingPrototype("Spliter Production Building Prototype", BuildingKind.SpliterProduction, false, true, new Vector2(2.5f, 2.5f), new Color(0.45f, 0.2f, 0.66f, 1f), prototypeRoot.transform);
            var autoTurretPrototype = CreateBuildingPrototype("Auto Turret Building Prototype", BuildingKind.AutoTurret, false, false, new Vector2(2.3f, 2.3f), new Color(0.38f, 0.34f, 0.34f, 1f), prototypeRoot.transform);
            var speedAuraPrototype = CreateBuildingPrototype("Speed Aura Building Prototype", BuildingKind.SpeedAura, false, false, new Vector2(2.6f, 2.6f), new Color(0.18f, 0.66f, 0.75f, 1f), prototypeRoot.transform);
            var constructionPrototype = CreateConstructionSitePrototype(prototypeRoot.transform);

            var workerDefinitions = new[]
            {
                CreateProductionDefinition("Worker", PrototypeUnitType.Worker, workerUnitPrefab, new ResourceAmount(50, 0), 5f, 1)
            };
            var combatDefinitions = new[]
            {
                CreateProductionDefinition("Soldier", PrototypeUnitType.Soldier, soldierUnitPrefab, new ResourceAmount(100, 0), 7f, 2),
                CreateProductionDefinition("Ranger", PrototypeUnitType.Ranger, rangerUnitPrefab, new ResourceAmount(100, 25), 8f, 2),
                CreateProductionDefinition("Tank", PrototypeUnitType.Tank, tankUnitPrefab, new ResourceAmount(150, 0), 10f, 3),
                CreateProductionDefinition("Striker", PrototypeUnitType.Striker, strikerUnitPrefab, new ResourceAmount(75, 0), 6f, 1),
                CreateProductionDefinition("Swarm x3", PrototypeUnitType.Swarm, swarmUnitPrefab, new ResourceAmount(120, 0), 8f, 1, 3)
            };
            var spliterDefinitions = new[] { CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, spliterUnitPrefab, new ResourceAmount(125, 0), 8f, 3) };

            InstantiateBuilding(mainBasePrototype, "Player Main Base", UnitTeam.Team1, BuildingKind.MainBase, playerStart, playerWallet, tilemapWorld, workerDefinitions, new Vector3(2.5f, -1.5f, 0f), new Vector3(5f, -2f, 0f), root.transform);
            InstantiateBuilding(mainBasePrototype, "AI Main Base", UnitTeam.Team2, BuildingKind.MainBase, aiStart, aiWallet, tilemapWorld, workerDefinitions, new Vector3(-2.5f, 1.5f, 0f), new Vector3(-5f, 2f, 0f), root.transform);

            CreateResourceCluster(playerStart + new Vector3(-3f, -3f, 0f), root.transform, tilemapWorld);
            CreateResourceCluster(aiStart + new Vector3(3f, 3f, 0f), root.transform, tilemapWorld);
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
                combatDefinitions,
                spliterDefinitions,
                root.transform);
            CreateAiController(playerStart, root.transform);
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
            UnitProductionDefinition[] combatDefinitions,
            UnitProductionDefinition[] spliterDefinitions,
            Transform parent)
        {
            var commandController = PlayerUnitCommandController.ActiveInstance ?? FindFirstObjectByType<PlayerUnitCommandController>();
            if (commandController == null)
            {
                commandController = CreateChild(parent, "Player Runtime Systems").AddComponent<PlayerUnitCommandController>();
            }

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
            placementService.ConfigureBuildOptions(spliterProductionPrototype, autoTurretPrototype, speedAuraPrototype);

            var hud = FindFirstObjectByType<RtsGameHud>();
            if (hud == null)
            {
                hud = commandController.gameObject.AddComponent<RtsGameHud>();
            }

            hud.Configure(UnitTeam.Team1, placementService);
            EnsureMatchController(parent);
        }

        private static void CreateAiController(Vector3 fallbackAttackPoint, Transform parent)
        {
            var aiObject = CreateChild(parent, "Simple Skirmish AI");
            var ai = aiObject.AddComponent<SimpleSkirmishAI>();
            ai.Configure(UnitTeam.Team2, UnitTeam.Team1, 3, 7, fallbackAttackPoint);
            ai.ConfigureTempo(1.5f, 18f);
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

        private static GameObject CreateBuildingPrototype(
            string name,
            BuildingKind kind,
            bool dropOff,
            bool production,
            Vector2 size,
            Color color,
            Transform parent)
        {
            var root = CreateChild(parent, name);
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            var visual = root.AddComponent<PrototypeBuildingVisual>();
            visual.Configure(color, new Color(0.08f, 0.13f, 0.18f, 1f), size, GetBuildingSpriteResourcePath(kind));

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;

            var status = root.AddComponent<BuildingStatus>();
            status.Initialize(UnitTeam.Team1, kind, Vector2Int.CeilToInt(size), true);
            if (dropOff)
            {
                root.AddComponent<ResourceDropOff>();
            }

            if (production)
            {
                root.AddComponent<UnitProductionQueue>();
            }

            if (kind == BuildingKind.AutoTurret)
            {
                root.AddComponent<BuildingAutoTurret>();
            }
            else if (kind == BuildingKind.SpeedAura)
            {
                root.AddComponent<BuildingSpeedAura>();
            }

            return root;
        }

        private static string GetBuildingSpriteResourcePath(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.SpliterProduction: return "Temp/Buildings/SpliterProductionBuilding";
                case BuildingKind.AutoTurret: return "Temp/Buildings/AutoTurretBuilding";
                case BuildingKind.SpeedAura: return "Temp/Buildings/SpeedAuraBuilding";
                default: return string.Empty;
            }
        }

        private static GameObject CreateConstructionSitePrototype(Transform parent)
        {
            var root = CreateChild(parent, "Construction Site Prototype");
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            var visual = root.AddComponent<PrototypeBuildingVisual>();
            visual.Configure(new Color(0.52f, 0.48f, 0.4f, 0.85f), new Color(0.95f, 0.82f, 0.38f, 1f), new Vector2(2.2f, 2f));

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.2f, 2f);
            collider.isTrigger = true;
            root.AddComponent<ConstructionSite>();
            return root;
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
                status.Initialize(team, kind, kind == BuildingKind.MainBase ? new Vector2Int(3, 3) : new Vector2Int(2, 2), true);
                status.ConfigureSupplyProvided(kind == BuildingKind.MainBase ? MainBaseSupplyProvided : 0);
            }

            var productionQueue = building.GetComponent<UnitProductionQueue>();
            if (productionQueue != null)
            {
                productionQueue.Configure(wallet, tilemapWorld, definitions, 5, spawnOffset, rallyOffset);
            }
        }

        private static void CreateResourceCluster(Vector3 center, Transform parent, ProjectSTilemapWorld tilemapWorld)
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
                CreateResourceNode($"Mineral Field {i + 1}", ResourceType.Minerals, center + offsets[i], parent, tilemapWorld);
            }

            CreateResourceNode("Vespene Geyser", ResourceType.Gas, center + new Vector3(3f, 0f, 0f), parent, tilemapWorld);
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
            renderer.sprite = squareSprite ??= CreateSquareSprite();
            renderer.color = type == ResourceType.Minerals
                ? new Color(0.25f, 0.85f, 0.95f, 1f)
                : new Color(0.4f, 0.9f, 0.35f, 1f);
            renderer.sortingOrder = ResourceSortingOrder;

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
            int outputCount = 1)
        {
            var definition = new UnitProductionDefinition();
            definition.Configure(displayName, unitType, prefab, cost, duration, supplyCost, outputCount);
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
