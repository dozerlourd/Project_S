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
        private const int SelectionSortingOrder = 18;
        private const int UnitSortingOrder = 20;

        private static Sprite squareSprite;
        private static Sprite workerSprite;
        private static Sprite soldierSprite;
        private static Sprite spliterSprite;
        private static Sprite rangerSprite;
        private static Sprite selectionSprite;

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

        private static void UpgradeExistingSetup(Transform root)
        {
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
                FindOrCreateBuildingTemplate(templates, "Spliter Production Building Template", BuildingKind.SpliterProduction, true, new Vector2(2.5f, 2.5f), new Color(0.45f, 0.2f, 0.66f, 1f)),
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

        private void BuildTestSetup()
        {
            var tilemapWorld = ProjectSTilemapWorld.ActiveInstance ?? FindFirstObjectByType<ProjectSTilemapWorld>();
            var root = new GameObject(SetupRootName);
            var prototypeRoot = CreateChild(root.transform, "Runtime Prototypes");
            prototypeRoot.SetActive(false);

            GetStartPositions(tilemapWorld, out var playerStart, out var aiStart);
            var playerWallet = CreateWallet("Player Wallet", UnitTeam.Team1, new ResourceAmount(700, 100), root.transform);
            var aiWallet = CreateWallet("AI Wallet", UnitTeam.Team2, new ResourceAmount(700, 100), root.transform);

            var workerPrototype = CreateUnitPrototype(
                "Worker Prototype",
                UnitTeam.Team1,
                PrototypeUnitType.Worker,
                workerSprite ??= CreateUnitSprite(PrototypeUnitType.Worker),
                new Color(0.62f, 0.68f, 0.55f, 1f),
                prototypeRoot.transform);
            var soldierPrototype = CreateUnitPrototype(
                "Soldier Prototype",
                UnitTeam.Team1,
                PrototypeUnitType.Soldier,
                soldierSprite ??= CreateUnitSprite(PrototypeUnitType.Soldier),
                new Color(0.58f, 0.48f, 0.38f, 1f),
                prototypeRoot.transform);
            var spliterPrototype = CreateUnitPrototype(
                "Spliter Prototype",
                UnitTeam.Team1,
                PrototypeUnitType.Spliter,
                spliterSprite ??= CreateUnitSprite(PrototypeUnitType.Spliter),
                new Color(0.55f, 0.38f, 0.28f, 1f),
                prototypeRoot.transform);
            var rangerPrototype = CreateUnitPrototype(
                "Ranger Prototype",
                UnitTeam.Team1,
                PrototypeUnitType.Ranger,
                rangerSprite ??= CreateUnitSprite(PrototypeUnitType.Ranger),
                new Color(0.46f, 0.42f, 0.58f, 1f),
                prototypeRoot.transform);

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
                CreateProductionDefinition("Worker", PrototypeUnitType.Worker, workerPrototype, new ResourceAmount(50, 0), 5f)
            };
            var combatDefinitions = new[]
            {
                CreateProductionDefinition("Soldier", PrototypeUnitType.Soldier, soldierPrototype, new ResourceAmount(100, 0), 7f),
                CreateProductionDefinition("Ranger", PrototypeUnitType.Ranger, rangerPrototype, new ResourceAmount(100, 25), 8f)
            };
            var spliterDefinitions = new[] { CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, spliterPrototype, new ResourceAmount(125, 0), 8f) };

            InstantiateBuilding(mainBasePrototype, "Player Main Base", UnitTeam.Team1, BuildingKind.MainBase, playerStart, playerWallet, tilemapWorld, workerDefinitions, new Vector3(2.5f, -1.5f, 0f), new Vector3(5f, -2f, 0f), root.transform);
            InstantiateBuilding(productionPrototype, "Player Production", UnitTeam.Team1, BuildingKind.Production, Snap(tilemapWorld, playerStart + new Vector3(4f, -3f, 0f)), playerWallet, tilemapWorld, combatDefinitions, new Vector3(2.5f, -0.5f, 0f), new Vector3(5f, -1f, 0f), root.transform);
            InstantiateBuilding(spliterProductionPrototype, "Player Spliter Production", UnitTeam.Team1, BuildingKind.SpliterProduction, Snap(tilemapWorld, playerStart + new Vector3(7f, -3f, 0f)), playerWallet, tilemapWorld, spliterDefinitions, new Vector3(2.5f, -0.5f, 0f), new Vector3(5f, -1f, 0f), root.transform);
            InstantiateBuilding(autoTurretPrototype, "Player Auto Turret", UnitTeam.Team1, BuildingKind.AutoTurret, Snap(tilemapWorld, playerStart + new Vector3(3f, 3f, 0f)), playerWallet, tilemapWorld, new UnitProductionDefinition[0], Vector3.zero, Vector3.zero, root.transform);
            InstantiateBuilding(speedAuraPrototype, "Player Speed Aura", UnitTeam.Team1, BuildingKind.SpeedAura, Snap(tilemapWorld, playerStart + new Vector3(-3f, 3f, 0f)), playerWallet, tilemapWorld, new UnitProductionDefinition[0], Vector3.zero, Vector3.zero, root.transform);
            InstantiateBuilding(mainBasePrototype, "AI Main Base", UnitTeam.Team2, BuildingKind.MainBase, aiStart, aiWallet, tilemapWorld, workerDefinitions, new Vector3(-2.5f, 1.5f, 0f), new Vector3(-5f, 2f, 0f), root.transform);
            InstantiateBuilding(productionPrototype, "AI Production", UnitTeam.Team2, BuildingKind.Production, Snap(tilemapWorld, aiStart + new Vector3(-4f, 3f, 0f)), aiWallet, tilemapWorld, combatDefinitions, new Vector3(-2.5f, 0.5f, 0f), new Vector3(-5f, 1f, 0f), root.transform);
            InstantiateBuilding(spliterProductionPrototype, "AI Spliter Production", UnitTeam.Team2, BuildingKind.SpliterProduction, Snap(tilemapWorld, aiStart + new Vector3(-7f, 3f, 0f)), aiWallet, tilemapWorld, spliterDefinitions, new Vector3(-2.5f, 0.5f, 0f), new Vector3(-5f, 1f, 0f), root.transform);
            InstantiateBuilding(autoTurretPrototype, "AI Auto Turret", UnitTeam.Team2, BuildingKind.AutoTurret, Snap(tilemapWorld, aiStart + new Vector3(-3f, -3f, 0f)), aiWallet, tilemapWorld, new UnitProductionDefinition[0], Vector3.zero, Vector3.zero, root.transform);
            InstantiateBuilding(speedAuraPrototype, "AI Speed Aura", UnitTeam.Team2, BuildingKind.SpeedAura, Snap(tilemapWorld, aiStart + new Vector3(3f, -3f, 0f)), aiWallet, tilemapWorld, new UnitProductionDefinition[0], Vector3.zero, Vector3.zero, root.transform);

            CreateResourceCluster(playerStart + new Vector3(-3f, -3f, 0f), root.transform, tilemapWorld);
            CreateResourceCluster(aiStart + new Vector3(3f, 3f, 0f), root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team1, playerStart, workerPrototype, soldierPrototype, spliterPrototype, rangerPrototype, root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team2, aiStart, workerPrototype, soldierPrototype, spliterPrototype, rangerPrototype, root.transform, tilemapWorld);
            CreatePlayerSystems(playerWallet, tilemapWorld, constructionPrototype, productionPrototype, spliterProductionPrototype, autoTurretPrototype, speedAuraPrototype, root.transform);
            CreateAiController(playerStart, root.transform);
        }

        private static void CreatePlayerSystems(
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionPrototype,
            GameObject productionPrototype,
            GameObject spliterProductionPrototype,
            GameObject autoTurretPrototype,
            GameObject speedAuraPrototype,
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
            ai.Configure(UnitTeam.Team2, UnitTeam.Team1, 4, 4, fallbackAttackPoint);
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

        private static GameObject CreateUnitPrototype(
            string name,
            UnitTeam team,
            PrototypeUnitType unitType,
            Sprite sprite,
            Color bodyColor,
            Transform parent)
        {
            var root = CreateChild(parent, name);
            root.SetActive(false);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.77f, 1f);
            collider.offset = new Vector2(0f, 0.1f);
            collider.isTrigger = true;

            var rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;

            var visual = CreateChild(root.transform, "Visual");
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = bodyColor;
            renderer.sortingOrder = UnitSortingOrder;

            var selectionRing = CreateChild(root.transform, "SelectionRing");
            var selectionRenderer = selectionRing.AddComponent<SpriteRenderer>();
            selectionRenderer.sprite = selectionSprite ??= CreateSelectionSprite();
            selectionRenderer.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            selectionRenderer.sortingOrder = SelectionSortingOrder;
            selectionRing.SetActive(false);

            var status = root.AddComponent<PrototypeUnitStatus>();
            ApplyStatus(status, team, unitType);
            root.AddComponent<UnitPathAgent>();
            root.AddComponent<UnitCommandAgent>();
            root.AddComponent<TemporaryAttackEffect>();
            root.AddComponent<UnitTeamIndicator>();
            root.AddComponent<UnitHealth>();
            root.AddComponent<UnitCombat>();
            root.AddComponent<UnitHealthBar>();
            if (unitType == PrototypeUnitType.Worker)
            {
                root.AddComponent<WorkerGatherController>();
                root.AddComponent<WorkerConstructionController>();
            }

            return root;
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
            GameObject soldierPrototype,
            GameObject spliterPrototype,
            GameObject rangerPrototype,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var ySign = team == UnitTeam.Team1 ? -1f : 1f;
            var xSign = team == UnitTeam.Team1 ? 1f : -1f;
            InstantiateUnit(workerPrototype, $"{team} Worker 1", team, start + new Vector3(-1.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrototype, $"{team} Worker 2", team, start + new Vector3(-0.5f * xSign, -2.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrototype, $"{team} Worker 3", team, start + new Vector3(0.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(soldierPrototype, $"{team} Soldier 1", team, start + new Vector3(3f * xSign, 1f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(soldierPrototype, $"{team} Soldier 2", team, start + new Vector3(4f * xSign, 0f, 0f), parent, tilemapWorld);
            InstantiateUnit(spliterPrototype, $"{team} Spliter", team, start + new Vector3(3f * xSign, -1f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(rangerPrototype, $"{team} Ranger", team, start + new Vector3(4f * xSign, -2f * ySign, 0f), parent, tilemapWorld);
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
            float duration)
        {
            var definition = new UnitProductionDefinition();
            definition.Configure(displayName, unitType, prefab, cost, duration);
            return definition;
        }

        private static void ApplyStatus(PrototypeUnitStatus status, UnitTeam team, PrototypeUnitType unitType)
        {
            switch (unitType)
            {
                case PrototypeUnitType.Worker:
                    status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Resource | UnitRole.Builder, AttackDistanceType.Melee, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 60f, 3f, 0f, 1.2f, 4f, 1f, 3f, 1, Vector2Int.one, true, false, 0f);
                    break;
                case PrototypeUnitType.Soldier:
                    status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 100f, 10f, 0f, 1.5f, 5f, 1f, 3.2f, 1, Vector2Int.one, false, false, 0f);
                    break;
                case PrototypeUnitType.Spliter:
                    status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Combat, AttackDistanceType.Melee, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common, AttackTargetType.AreaAttack, 90f, 8f, 0f, 1.4f, 5f, 0.9f, 3f, 3, Vector2Int.one, false, true, 2f);
                    break;
                case PrototypeUnitType.Ranger:
                    status.Initialize(UnitTrial.Human, team, unitType, MovementDomain.Ground, UnitRole.Combat, AttackDistanceType.Ranged, AttackPowerType.Physical, PlacementType.Movable, UnitGrade.Common, AttackTargetType.SingleTarget, 70f, 8f, 0f, 6f, 8f, 0.8f, 2.8f, 1, Vector2Int.one, false, false, 0f);
                    break;
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

        private static Sprite CreateUnitSprite(PrototypeUnitType unitType)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var center = new Vector2(15.5f, 15.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    var inside = unitType == PrototypeUnitType.Ranger
                        ? Mathf.Abs(delta.x) + Mathf.Abs(delta.y) < 17f
                        : delta.magnitude < 13f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateSelectionSprite()
        {
            var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var center = new Vector2(23.5f, 23.5f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var radius = (new Vector2(x, y) - center).magnitude;
                    texture.SetPixel(x, y, radius > 18f && radius < 22f ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
