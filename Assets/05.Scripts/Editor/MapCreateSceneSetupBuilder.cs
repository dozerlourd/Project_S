using System.IO;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.UI;
using ProjectS.Units;
using ProjectS.Unlocks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ProjectS.Editor
{
    public static class MapCreateSceneSetupBuilder
    {
        private const string ScenePath = "Assets/06.Scenes/MapCreate_Scene.unity";
        private const string BuildingPrefabFolder = "Assets/03.Prefabs/Buildings";
        private const string CoreBuildingPrefabFolder = BuildingPrefabFolder + "/Core";
        private const string ConstructionPrefabFolder = BuildingPrefabFolder + "/Construction";
        private const string FutureTechPrefabFolder = BuildingPrefabFolder + "/FutureTech";
        private const string UniqueBuildingTextureFolder = "Assets/01.Textures/Buildings/Unique";
        private const string SetupRootName = "ProjectS Match Test Setup";
        private const string MainBasePrefabPath = CoreBuildingPrefabFolder + "/PrototypeMainBase.prefab";
        private const string ProductionPrefabPath = CoreBuildingPrefabFolder + "/PrototypeProductionBuilding.prefab";
        private const string SpliterProductionPrefabPath = CoreBuildingPrefabFolder + "/PrototypeSpliterProductionBuilding.prefab";
        private const string SupplyDepotPrefabPath = CoreBuildingPrefabFolder + "/PrototypeSupplyDepotBuilding.prefab";
        private const string AdvancedSupplyDepotPrefabPath = CoreBuildingPrefabFolder + "/PrototypeAdvancedSupplyDepotBuilding.prefab";
        private const string AutoTurretPrefabPath = CoreBuildingPrefabFolder + "/PrototypeAutoTurretBuilding.prefab";
        private const string SpeedAuraPrefabPath = CoreBuildingPrefabFolder + "/PrototypeSpeedAuraBuilding.prefab";
        private const string ConstructionSitePrefabPath = ConstructionPrefabFolder + "/PrototypeConstructionSite.prefab";
        private const string VehicleFactoryPrefabPath = FutureTechPrefabFolder + "/PrototypeVehicleFactoryBuilding.prefab";
        private const string MaintenanceBayPrefabPath = FutureTechPrefabFolder + "/PrototypeMaintenanceBayBuilding.prefab";
        private const string SignalRelayPrefabPath = FutureTechPrefabFolder + "/PrototypeSignalRelayBuilding.prefab";
        private const string MineralsResourceTilePath = "Assets/Assets/Tilemaps/Resource Tiles/ResourceNodes_Minerals.asset";
        private const string GasResourceTilePath = "Assets/Assets/Tilemaps/Resource Tiles/ResourceNodes_Gas.asset";
        private const string ObstacleTilePath = "Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.asset";
        private const string WorkerPrefabPath = "Assets/03.Prefabs/Units/B_Worker.prefab";
        private const string SoldierPrefabPath = "Assets/03.Prefabs/Units/B_Soldier.prefab";
        private const string SpliterPrefabPath = "Assets/03.Prefabs/Units/B_Spliter.prefab";
        private const string RangerPrefabPath = "Assets/03.Prefabs/Units/B_Ranger.prefab";
        private const string TankPrefabPath = "Assets/03.Prefabs/Units/B_Tank.prefab";
        private const string StrikerPrefabPath = "Assets/03.Prefabs/Units/B_Striker.prefab";
        private const string SwarmPrefabPath = "Assets/03.Prefabs/Units/B_Swarm.prefab";
        private const string MedicPrefabPath = "Assets/03.Prefabs/Units/B_Medic.prefab";
        private const string SiegePrefabPath = "Assets/03.Prefabs/Units/B_Siege.prefab";
        private const string ScoutPrefabPath = "Assets/03.Prefabs/Units/B_Scout.prefab";
        private const string MineralPrefabPath = "Assets/03.Prefabs/Resources/MineralField.prefab";
        private const string GasPrefabPath = "Assets/03.Prefabs/Resources/VespeneGeyser.prefab";
        private const int ResourceSortingOrder = 12;
        private const int BuildingSortingOrder = 20;
        private const int UnitSortingOrder = 20;

        [MenuItem("Tools/Project S/Setup MapCreate Combat Test Scene")]
        public static void SetupMapCreateScene()
        {
            CreatePrototypeBuildingPrefabs();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var tilemapWorld = Object.FindFirstObjectByType<ProjectSTilemapWorld>();
            RemoveExistingSetupRoot();

            var root = new GameObject(SetupRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            GetStartPositions(tilemapWorld, out var playerStart, out var aiStart);
            EnsureFormalMatchLayout(tilemapWorld, playerStart, aiStart);
            var playerWallet = CreateWallet("Player Wallet", UnitTeam.Team1, new ResourceAmount(100, 0), root.transform);
            var aiWallet = CreateWallet("AI Wallet", UnitTeam.Team2, new ResourceAmount(100, 0), root.transform);
            CreateSupplyManager("Player Supply", UnitTeam.Team1, root.transform);
            CreateSupplyManager("AI Supply", UnitTeam.Team2, root.transform);
            CreateTeamUnlockState("Player Unlocks", UnitTeam.Team1, root.transform);
            CreateTeamUnlockState("AI Unlocks", UnitTeam.Team2, root.transform);

            var workerPrefab = LoadRequired<GameObject>(WorkerPrefabPath);
            var soldierPrefab = LoadRequired<GameObject>(SoldierPrefabPath);
            var spliterPrefab = LoadRequired<GameObject>(SpliterPrefabPath);
            var rangerPrefab = LoadRequired<GameObject>(RangerPrefabPath);
            var tankPrefab = LoadRequired<GameObject>(TankPrefabPath);
            var strikerPrefab = LoadRequired<GameObject>(StrikerPrefabPath);
            var swarmPrefab = LoadRequired<GameObject>(SwarmPrefabPath);
            var medicPrefab = LoadRequired<GameObject>(MedicPrefabPath);
            var siegePrefab = LoadRequired<GameObject>(SiegePrefabPath);
            var scoutPrefab = LoadRequired<GameObject>(ScoutPrefabPath);
            var mainBasePrefab = LoadRequired<GameObject>(MainBasePrefabPath);
            var productionPrefab = LoadRequired<GameObject>(ProductionPrefabPath);
            var spliterProductionPrefab = LoadRequired<GameObject>(SpliterProductionPrefabPath);
            var supplyDepotPrefab = LoadRequired<GameObject>(SupplyDepotPrefabPath);
            var advancedSupplyDepotPrefab = LoadRequired<GameObject>(AdvancedSupplyDepotPrefabPath);
            var autoTurretPrefab = LoadRequired<GameObject>(AutoTurretPrefabPath);
            var speedAuraPrefab = LoadRequired<GameObject>(SpeedAuraPrefabPath);
            var vehicleFactoryPrefab = LoadRequired<GameObject>(VehicleFactoryPrefabPath);
            var maintenanceBayPrefab = LoadRequired<GameObject>(MaintenanceBayPrefabPath);
            var signalRelayPrefab = LoadRequired<GameObject>(SignalRelayPrefabPath);
            var constructionSitePrefab = LoadRequired<GameObject>(ConstructionSitePrefabPath);

            var workerDefinitions = new[]
            {
                CreateProductionDefinition("Worker", PrototypeUnitType.Worker, workerPrefab, new ResourceAmount(50, 0), 5f, allowedProductionBuildings: new[] { BuildingKind.MainBase })
            };
            var combatDefinitions = new[]
            {
                CreateProductionDefinition("Soldier", PrototypeUnitType.Soldier, soldierPrefab, new ResourceAmount(100, 0), 7f, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Ranger", PrototypeUnitType.Ranger, rangerPrefab, new ResourceAmount(100, 25), 8f, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Tank", PrototypeUnitType.Tank, tankPrefab, new ResourceAmount(150, 0), 10f, 3, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Striker", PrototypeUnitType.Striker, strikerPrefab, new ResourceAmount(75, 0), 6f, allowedProductionBuildings: new[] { BuildingKind.Production }),
                CreateProductionDefinition("Swarm x3", PrototypeUnitType.Swarm, swarmPrefab, new ResourceAmount(120, 0), 8f, 1, 3, allowedProductionBuildings: new[] { BuildingKind.Production })
            };
            var spliterDefinitions = new[]
            {
                CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, spliterPrefab, new ResourceAmount(125, 0), 8f, allowedProductionBuildings: new[] { BuildingKind.SpliterProduction })
            };
            var medicDefinitions = new[]
            {
                CreateSpecializedProductionDefinition("Medic", PrototypeUnitType.Medic, medicPrefab, 2, BuildingKind.MaintenanceBay)
            };
            var siegeDefinitions = new[]
            {
                CreateSpecializedProductionDefinition("Siege", PrototypeUnitType.Siege, siegePrefab, 4, BuildingKind.VehicleFactory)
            };
            var scoutDefinitions = new[]
            {
                CreateSpecializedProductionDefinition("Scout", PrototypeUnitType.Scout, scoutPrefab, 1, BuildingKind.SignalRelay)
            };

            ConfigureProductionPrefab(vehicleFactoryPrefab, playerWallet, tilemapWorld, siegeDefinitions, new Vector3(3.5f, -0.5f, 0f), new Vector3(6f, -1f, 0f));
            ConfigureProductionPrefab(maintenanceBayPrefab, playerWallet, tilemapWorld, medicDefinitions, new Vector3(3.5f, -0.5f, 0f), new Vector3(6f, -1f, 0f));
            ConfigureProductionPrefab(signalRelayPrefab, playerWallet, tilemapWorld, scoutDefinitions, new Vector3(2.5f, -0.5f, 0f), new Vector3(5f, -1f, 0f));

            var bootstrap = Object.FindFirstObjectByType<MapCreateSceneAutoBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.ConfigureUnitPrefabs(
                    workerPrefab,
                    soldierPrefab,
                    spliterPrefab,
                    rangerPrefab,
                    tankPrefab,
                    strikerPrefab,
                    swarmPrefab,
                    medicPrefab,
                    siegePrefab,
                    scoutPrefab);
                EditorUtility.SetDirty(bootstrap);
            }

            if (!ResourceTilemapNodeSynchronizer.SceneHasResourceTiles())
            {
                CreateResourceCluster(playerStart + new Vector3(-3f, -3f, 0f), root.transform, tilemapWorld);
                CreateResourceCluster(aiStart + new Vector3(3f, 3f, 0f), root.transform, tilemapWorld);
            }

            var mainBaseStatus = mainBasePrefab.GetComponent<BuildingStatus>();
            var mainBaseFootprint = mainBaseStatus != null ? mainBaseStatus.Footprint : new Vector2Int(3, 3);
            if (!ConstructionSite.TryFindNearestValidPlacement(tilemapWorld, playerStart, mainBaseFootprint, 12, out playerStart)
                || !ConstructionSite.TryFindNearestValidPlacement(tilemapWorld, aiStart, mainBaseFootprint, 12, out aiStart))
            {
                throw new System.InvalidOperationException("Could not find valid separated starting main base positions.");
            }

            InstantiateBuilding(
                mainBasePrefab,
                "Player Main Base",
                UnitTeam.Team1,
                BuildingKind.MainBase,
                playerStart,
                playerWallet,
                tilemapWorld,
                workerDefinitions,
                new Vector3(2.5f, -1.5f, 0f),
                new Vector3(5f, -2f, 0f),
                root.transform);

            InstantiateBuilding(
                mainBasePrefab,
                "AI Main Base",
                UnitTeam.Team2,
                BuildingKind.MainBase,
                aiStart,
                aiWallet,
                tilemapWorld,
                workerDefinitions,
                new Vector3(-2.5f, 1.5f, 0f),
                new Vector3(-5f, 2f, 0f),
                root.transform);

            CreateStartingUnits(UnitTeam.Team1, playerStart, workerPrefab, root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team2, aiStart, workerPrefab, root.transform, tilemapWorld);
            CreatePlayerRuntimeSystems(
                playerWallet,
                tilemapWorld,
                constructionSitePrefab,
                productionPrefab,
                spliterProductionPrefab,
                supplyDepotPrefab,
                advancedSupplyDepotPrefab,
                autoTurretPrefab,
                speedAuraPrefab,
                vehicleFactoryPrefab,
                maintenanceBayPrefab,
                signalRelayPrefab,
                combatDefinitions,
                spliterDefinitions,
                root.transform);
            CreateAiController(
                playerStart,
                aiWallet,
                tilemapWorld,
                constructionSitePrefab,
                productionPrefab,
                spliterProductionPrefab,
                autoTurretPrefab,
                speedAuraPrefab,
                supplyDepotPrefab,
                advancedSupplyDepotPrefab,
                vehicleFactoryPrefab,
                maintenanceBayPrefab,
                signalRelayPrefab,
                mainBasePrefab,
                root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Project S/Create Missing Building Prefabs")]
        public static void CreatePrototypeBuildingPrefabs()
        {
            EnsureFolder("Assets/03.Prefabs", "Buildings");
            EnsureFolder(BuildingPrefabFolder, "Core");
            EnsureFolder(BuildingPrefabFolder, "Construction");
            EnsureFolder(BuildingPrefabFolder, "FutureTech");
            CreateBuildingPrefab(MainBasePrefabPath, "PrototypeMainBase", BuildingKind.MainBase, new Vector2(2.6f, 2.2f));
            CreateBuildingPrefab(ProductionPrefabPath, "PrototypeProductionBuilding", BuildingKind.Production, new Vector2(2.4f, 2f));
            CreateBuildingPrefab(SpliterProductionPrefabPath, "PrototypeSpliterProductionBuilding", BuildingKind.SpliterProduction, new Vector2(2.5f, 2.5f));
            CreateBuildingPrefab(SupplyDepotPrefabPath, "PrototypeSupplyDepotBuilding", BuildingKind.SupplyDepot, new Vector2(2f, 1f));
            CreateBuildingPrefab(AdvancedSupplyDepotPrefabPath, "PrototypeAdvancedSupplyDepotBuilding", BuildingKind.AdvancedSupplyDepot, new Vector2(2f, 1f));
            CreateBuildingPrefab(AutoTurretPrefabPath, "PrototypeAutoTurretBuilding", BuildingKind.AutoTurret, new Vector2(2.3f, 2.3f));
            CreateBuildingPrefab(SpeedAuraPrefabPath, "PrototypeSpeedAuraBuilding", BuildingKind.SpeedAura, new Vector2(2.6f, 2.6f));
            CreateBuildingPrefab(VehicleFactoryPrefabPath, "PrototypeVehicleFactoryBuilding", BuildingKind.VehicleFactory, new Vector2(3f, 3f));
            CreateBuildingPrefab(MaintenanceBayPrefabPath, "PrototypeMaintenanceBayBuilding", BuildingKind.MaintenanceBay, new Vector2(3f, 2f));
            CreateBuildingPrefab(SignalRelayPrefabPath, "PrototypeSignalRelayBuilding", BuildingKind.SignalRelay, new Vector2(2f, 2f));
            CreateConstructionSitePrefab();
            ApplyUniqueCoreBuildingSprites();
        }

        [MenuItem("Tools/Project S/Apply Unique Core Building Sprites")]
        public static void ApplyUniqueCoreBuildingSprites()
        {
            ApplyUniqueBuildingSprite(MainBasePrefabPath, "MainBase_Unique", new Vector2(2.6f, 2.2f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(ProductionPrefabPath, "Production_Unique", new Vector2(2.4f, 2f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(SpliterProductionPrefabPath, "SpliterProduction_Unique", new Vector2(2.5f, 2.5f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(AutoTurretPrefabPath, "AutoTurret_Unique", new Vector2(2.3f, 2.3f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(SpeedAuraPrefabPath, "SpeedAura_Unique", new Vector2(2.6f, 2.6f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(VehicleFactoryPrefabPath, "VehicleFactory_Unique", new Vector2(3f, 3f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(MaintenanceBayPrefabPath, "MaintenanceBay_Unique", new Vector2(3f, 2f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(SignalRelayPrefabPath, "SignalRelay_Unique", new Vector2(2f, 2f), BuildingSortingOrder);
            ApplyUniqueBuildingSprite(ConstructionSitePrefabPath, "ConstructionSite_Unique", new Vector2(2.2f, 2f), BuildingSortingOrder - 1);
            AssetDatabase.SaveAssets();
        }

        private static void CreateBuildingPrefab(
            string path,
            string name,
            BuildingKind kind,
            Vector2 size)
        {
            // Existing assets retain their component file IDs and customized settings.
            if (File.Exists(path))
            {
                return;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Structures";
                renderer.sortingOrder = BuildingSortingOrder;
                renderer.sprite = LoadBuildingSprite(kind);
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = size;
                var visual = root.AddComponent<PrototypeBuildingVisual>();
                var body = kind == BuildingKind.MainBase
                    ? new Color(0.28f, 0.52f, 0.76f, 1f)
                    : new Color(0.48f, 0.36f, 0.68f, 1f);
                visual.Configure(body, new Color(0.08f, 0.13f, 0.18f, 1f), size);

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = kind == BuildingKind.MainBase
                    ? new Vector2(2f, 1.6f)
                    : size;
                collider.isTrigger = true;

                var status = StructureFactory.AddTo(root, kind);
                status.Initialize(UnitTeam.Team1, kind, StructureFactory.GetDefaultFootprint(kind), true);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static Sprite LoadBuildingSprite(BuildingKind kind)
        {
            var uniqueAssetPath = GetUniqueBuildingSpriteAssetPath(kind);
            var uniqueSprite = LoadLargestSprite(uniqueAssetPath);
            if (uniqueSprite != null)
            {
                return uniqueSprite;
            }

            return null;
        }

        private static string GetUniqueBuildingSpriteAssetPath(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.MainBase: return UniqueBuildingTextureFolder + "/MainBase_Unique.png";
                case BuildingKind.Production: return UniqueBuildingTextureFolder + "/Production_Unique.png";
                case BuildingKind.SpliterProduction: return UniqueBuildingTextureFolder + "/SpliterProduction_Unique.png";
                case BuildingKind.AutoTurret: return UniqueBuildingTextureFolder + "/AutoTurret_Unique.png";
                case BuildingKind.SpeedAura: return UniqueBuildingTextureFolder + "/SpeedAura_Unique.png";
                case BuildingKind.SupplyDepot: return "Assets/01.Textures/Buildings/SupplyDepotBuilding.png";
                case BuildingKind.AdvancedSupplyDepot: return "Assets/01.Textures/Buildings/AdvancedSupplyDepotBuilding.png";
                case BuildingKind.VehicleFactory: return UniqueBuildingTextureFolder + "/VehicleFactory_Unique.png";
                case BuildingKind.MaintenanceBay: return UniqueBuildingTextureFolder + "/MaintenanceBay_Unique.png";
                case BuildingKind.SignalRelay: return UniqueBuildingTextureFolder + "/SignalRelay_Unique.png";
                default: return string.Empty;
            }
        }

        private static Sprite LoadLargestSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Sprite largest = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite && (largest == null
                    || sprite.rect.width * sprite.rect.height > largest.rect.width * largest.rect.height))
                {
                    largest = sprite;
                }
            }
            return largest;
        }

        private static void ApplyUniqueBuildingSprite(
            string prefabPath,
            string textureName,
            Vector2 size,
            int sortingOrder)
        {
            var sprite = LoadLargestSprite(UniqueBuildingTextureFolder + "/" + textureName + ".png");
            if (sprite == null || !File.Exists(prefabPath))
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>() ?? root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = size;
                renderer.sortingLayerName = "Structures";
                renderer.sortingOrder = sortingOrder;

                var collider = root.GetComponent<BoxCollider2D>() ?? root.AddComponent<BoxCollider2D>();
                collider.size = prefabPath == MainBasePrefabPath
                    ? new Vector2(2f, 1.6f)
                    : size;
                collider.isTrigger = true;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateConstructionSitePrefab()
        {
            if (File.Exists(ConstructionSitePrefabPath))
            {
                return;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("PrototypeConstructionSite");
            SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadLargestSprite(UniqueBuildingTextureFolder + "/ConstructionSite_Unique.png");
                renderer.sortingLayerName = "Structures";
                renderer.sortingOrder = BuildingSortingOrder - 1;
                var visual = root.AddComponent<PrototypeBuildingVisual>();
                visual.Configure(
                    new Color(0.52f, 0.48f, 0.4f, 0.85f),
                    new Color(0.95f, 0.82f, 0.38f, 1f),
                    new Vector2(2.2f, 2f),
                    renderOrder: BuildingSortingOrder - 1);

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(2.2f, 2f);
                collider.isTrigger = true;
                root.AddComponent<ConstructionSite>();

                PrefabUtility.SaveAsPrefabAsset(root, ConstructionSitePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void CreatePlayerRuntimeSystems(
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionSitePrefab,
            GameObject productionPrefab,
            GameObject spliterProductionPrefab,
            GameObject supplyDepotPrefab,
            GameObject advancedSupplyDepotPrefab,
            GameObject autoTurretPrefab,
            GameObject speedAuraPrefab,
            GameObject vehicleFactoryPrefab,
            GameObject maintenanceBayPrefab,
            GameObject signalRelayPrefab,
            UnitProductionDefinition[] combatDefinitions,
            UnitProductionDefinition[] spliterDefinitions,
            Transform parent)
        {
            var runtime = new GameObject("Player Runtime Systems");
            runtime.transform.SetParent(parent, false);

            var workerAutoAssignment = runtime.AddComponent<WorkerAutoAssignmentManager>();
            workerAutoAssignment.Configure(UnitTeam.Team1, false);
            var commandController = runtime.AddComponent<PlayerUnitCommandController>();
            var placementService = runtime.AddComponent<BuildingPlacementService>();
            placementService.Configure(
                UnitTeam.Team1,
                wallet,
                tilemapWorld,
                constructionSitePrefab,
                productionPrefab,
                BuildingKind.Production,
                new ResourceAmount(150, 0),
                8f,
                new Vector2Int(2, 2));
            ConfigureProductionPrefab(productionPrefab, wallet, tilemapWorld, combatDefinitions, new Vector3(2.5f, -0.5f, 0f), new Vector3(5f, -1f, 0f));
            ConfigureProductionPrefab(spliterProductionPrefab, wallet, tilemapWorld, spliterDefinitions, new Vector3(2.5f, -0.5f, 0f), new Vector3(5f, -1f, 0f));
            placementService.ConfigureBuildOptions(
                spliterProductionPrefab,
                autoTurretPrefab,
                speedAuraPrefab,
                supplyDepotPrefab: supplyDepotPrefab,
                advancedSupplyDepotPrefab: advancedSupplyDepotPrefab,
                vehicleFactoryPrefab: vehicleFactoryPrefab,
                maintenanceBayPrefab: maintenanceBayPrefab,
                signalRelayPrefab: signalRelayPrefab);

            var hud = runtime.AddComponent<RtsGameHud>();
            hud.Configure(UnitTeam.Team1, placementService);

            var matchController = runtime.AddComponent<ProjectS.RtsMatchController>();
            matchController.Configure(UnitTeam.Team1, UnitTeam.Team2);
        }

        private static void ConfigureProductionPrefab(
            GameObject prefab,
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            UnitProductionDefinition[] definitions,
            Vector3 spawnOffset,
            Vector3 rallyOffset)
        {
            var queue = prefab != null ? prefab.GetComponent<UnitProductionQueue>() : null;
            if (queue != null)
            {
                queue.Configure(wallet, tilemapWorld, definitions, 5, spawnOffset, rallyOffset);
                EditorUtility.SetDirty(queue);
            }
        }

        private static void CreateAiController(
            Vector3 fallbackAttackPoint,
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionSitePrefab,
            GameObject productionPrefab,
            GameObject spliterProductionPrefab,
            GameObject autoTurretPrefab,
            GameObject speedAuraPrefab,
            GameObject supplyDepotPrefab,
            GameObject advancedSupplyDepotPrefab,
            GameObject vehicleFactoryPrefab,
            GameObject maintenanceBayPrefab,
            GameObject signalRelayPrefab,
            GameObject mainBasePrefab,
            Transform parent)
        {
            var aiObject = new GameObject("Simple Skirmish AI");
            aiObject.transform.SetParent(parent, false);
            var ai = aiObject.AddComponent<SimpleSkirmishAI>();
            ai.Configure(UnitTeam.Team2, UnitTeam.Team1, 3, 7, fallbackAttackPoint);
            ai.ConfigureTempo(1.5f, 18f);

            var templates = aiObject.AddComponent<AiBuildingTemplateRegistry>();
            templates.Configure(UnitTeam.Team2, wallet, tilemapWorld, constructionSitePrefab);
            templates.RegisterTemplate(BuildingKind.Production, productionPrefab, new ResourceAmount(150, 0), 8f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.SpliterProduction, spliterProductionPrefab, new ResourceAmount(175, 0), 9f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.AutoTurret, autoTurretPrefab, new ResourceAmount(125, 0), 7f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.SpeedAura, speedAuraPrefab, new ResourceAmount(125, 25), 7f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.SupplyDepot, supplyDepotPrefab, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 1));
            templates.RegisterTemplate(BuildingKind.AdvancedSupplyDepot, advancedSupplyDepotPrefab, new ResourceAmount(100, 0), 6f, new Vector2Int(2, 1));
            templates.RegisterTemplate(BuildingKind.VehicleFactory, vehicleFactoryPrefab, new ResourceAmount(250, 75), 10f, new Vector2Int(3, 3));
            templates.RegisterTemplate(BuildingKind.MaintenanceBay, maintenanceBayPrefab, new ResourceAmount(200, 50), 9f, new Vector2Int(3, 2));
            templates.RegisterTemplate(BuildingKind.SignalRelay, signalRelayPrefab, new ResourceAmount(150, 50), 8f, new Vector2Int(2, 2));
            templates.RegisterTemplate(BuildingKind.MainBase, mainBasePrefab, new ResourceAmount(350, 75), 12f, new Vector2Int(3, 3));
        }

        private static PlayerResourceWallet CreateWallet(
            string name,
            UnitTeam team,
            ResourceAmount resources,
            Transform parent)
        {
            var walletObject = new GameObject(name);
            walletObject.transform.SetParent(parent, false);
            var wallet = walletObject.AddComponent<PlayerResourceWallet>();
            wallet.Initialize(team, resources);
            return wallet;
        }

        private static SupplyManager CreateSupplyManager(string name, UnitTeam team, Transform parent)
        {
            var supplyObject = new GameObject(name);
            supplyObject.transform.SetParent(parent, false);
            var supplyManager = supplyObject.AddComponent<SupplyManager>();
            supplyManager.Initialize(team);
            return supplyManager;
        }

        private static void CreateTeamUnlockState(string name, UnitTeam team, Transform parent)
        {
            var stateObject = new GameObject(name);
            stateObject.transform.SetParent(parent, false);
            var state = stateObject.AddComponent<TeamUnlockState>();
            state.Configure(team);
        }

        private static void InstantiateBuilding(
            GameObject prefab,
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
            var building = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            building.name = name;
            building.transform.position = position;
            building.transform.SetParent(parent, true);
            SetSortingOrder(building, BuildingSortingOrder);

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

        private static void EnsureFormalMatchLayout(
            ProjectSTilemapWorld tilemapWorld,
            Vector3 playerStart,
            Vector3 aiStart)
        {
            if (tilemapWorld == null || tilemapWorld.Grid == null)
            {
                return;
            }

            var mineralsTile = AssetDatabase.LoadAssetAtPath<ResourceTile>(MineralsResourceTilePath);
            var gasTile = AssetDatabase.LoadAssetAtPath<ResourceTile>(GasResourceTilePath);
            if (mineralsTile == null || gasTile == null)
            {
                Debug.LogWarning("Formal match layout skipped because resource tile assets are missing.");
                return;
            }

            var resourceTilemap = FindOrCreateTilemap(tilemapWorld.Grid.transform, "Resource", ResourceSortingOrder);
            if (resourceTilemap.GetUsedTilesCount() == 0)
            {
                PaintResourceCluster(resourceTilemap, tilemapWorld, playerStart + new Vector3(-3f, -3f, 0f), mineralsTile, gasTile);
                PaintResourceCluster(resourceTilemap, tilemapWorld, aiStart + new Vector3(3f, 3f, 0f), mineralsTile, gasTile);

                var center = tilemapWorld.GetCellCenterWorld(new Vector3Int(
                    Mathf.RoundToInt((tilemapWorld.CellBounds.xMin + tilemapWorld.CellBounds.xMax - 1) * 0.5f),
                    Mathf.RoundToInt((tilemapWorld.CellBounds.yMin + tilemapWorld.CellBounds.yMax - 1) * 0.5f),
                    0));
                PaintResourceCluster(resourceTilemap, tilemapWorld, center + new Vector3(-7f, 0f, 0f), mineralsTile, gasTile);
                PaintResourceCluster(resourceTilemap, tilemapWorld, center + new Vector3(7f, 0f, 0f), mineralsTile, gasTile);
            }

            var obstacleTile = AssetDatabase.LoadAssetAtPath<ProjectSTile>(ObstacleTilePath);
            var obstacleTilemap = FindOrCreateTilemap(tilemapWorld.Grid.transform, "Obstacle", ResourceSortingOrder - 1);
            if (obstacleTile != null && obstacleTilemap.GetUsedTilesCount() == 0)
            {
                var bounds = tilemapWorld.CellBounds;
                var centerX = Mathf.FloorToInt((bounds.xMin + bounds.xMax - 1) * 0.5f);
                var centerY = Mathf.FloorToInt((bounds.yMin + bounds.yMax - 1) * 0.5f);
                for (var offset = -5; offset <= 5; offset++)
                {
                    if (offset >= -1 && offset <= 1)
                    {
                        continue;
                    }

                    obstacleTilemap.SetTile(new Vector3Int(centerX, centerY + offset, 0), obstacleTile);
                }
            }

            tilemapWorld.MarkNavigationCacheDirty();
        }

        private static Tilemap FindOrCreateTilemap(Transform grid, string name, int sortingOrder)
        {
            foreach (var existingTilemap in grid.GetComponentsInChildren<Tilemap>(true))
            {
                if (existingTilemap != null && existingTilemap.gameObject.name == name)
                {
                    return existingTilemap;
                }
            }

            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(grid, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static void PaintResourceCluster(
            Tilemap resourceTilemap,
            ProjectSTilemapWorld tilemapWorld,
            Vector3 center,
            ResourceTile mineralsTile,
            ResourceTile gasTile)
        {
            var centerCell = tilemapWorld.WorldToCell(center);
            var mineralOffsets = new[]
            {
                new Vector3Int(-2, 0, 0),
                new Vector3Int(-1, 1, 0),
                new Vector3Int(0, 0, 0),
                new Vector3Int(-1, -1, 0)
            };

            for (var i = 0; i < mineralOffsets.Length; i++)
            {
                resourceTilemap.SetTile(centerCell + mineralOffsets[i], mineralsTile);
            }

            resourceTilemap.SetTile(centerCell + new Vector3Int(2, 0, 0), gasTile);
        }

        private static void CreateResourceCluster(Vector3 center, Transform parent, ProjectSTilemapWorld tilemapWorld)
        {
            var mineralPrefab = LoadRequired<GameObject>(MineralPrefabPath);
            var gasPrefab = LoadRequired<GameObject>(GasPrefabPath);
            var offsets = new[]
            {
                new Vector3(-1.5f, 0f, 0f),
                new Vector3(0f, 0.8f, 0f),
                new Vector3(1.5f, 0f, 0f),
                new Vector3(0f, -0.8f, 0f)
            };

            for (var i = 0; i < offsets.Length; i++)
            {
                var node = InstantiateResource(mineralPrefab, $"Mineral Field {i + 1}", center + offsets[i], parent, tilemapWorld);
            node.Configure(ResourceType.Minerals, 1500, 8, 2.4f, 0.95f, true);
            }

            var gas = InstantiateResource(gasPrefab, "Vespene Geyser", center + new Vector3(3f, 0f, 0f), parent, tilemapWorld);
            gas.Configure(ResourceType.Gas, 1500, 8, 2.4f, 0.95f, true);
        }

        private static ResourceNode InstantiateResource(
            GameObject prefab,
            string name,
            Vector3 position,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var resourceObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            resourceObject.name = name;
            resourceObject.transform.position = Snap(tilemapWorld, position);
            resourceObject.transform.SetParent(parent, true);
            SetSortingOrder(resourceObject, ResourceSortingOrder);
            var node = resourceObject.GetComponent<ResourceNode>();
            return node != null ? node : resourceObject.AddComponent<ResourceNode>();
        }

        private static void CreateStartingUnits(
            UnitTeam team,
            Vector3 start,
            GameObject workerPrefab,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var xSign = team == UnitTeam.Team1 ? 1f : -1f;
            var ySign = team == UnitTeam.Team1 ? -1f : 1f;
            InstantiateUnit(workerPrefab, $"{team} Worker 1", team, start + new Vector3(-1.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrefab, $"{team} Worker 2", team, start + new Vector3(-0.5f * xSign, -2.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrefab, $"{team} Worker 3", team, start + new Vector3(0.5f * xSign, -1.5f * ySign, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrefab, $"{team} Worker 4", team, start + new Vector3(1.5f * xSign, -2.5f * ySign, 0f), parent, tilemapWorld);
        }

        private static void InstantiateUnit(
            GameObject prefab,
            string name,
            UnitTeam team,
            Vector3 position,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            unit.name = name;
            unit.transform.position = Snap(tilemapWorld, position);
            unit.transform.SetParent(parent, true);
            SetSortingOrder(unit, UnitSortingOrder);

            var status = unit.GetComponent<PrototypeUnitStatus>();
            if (status != null)
            {
                status.SetTeam(team);
            }
        }

        private static UnitProductionDefinition CreateProductionDefinition(
            string displayName,
            PrototypeUnitType unitType,
            GameObject prefab,
            ResourceAmount cost,
            float duration,
            int supplyCost = 1,
            int outputCount = 1,
            BuildingKind[] allowedProductionBuildings = null,
            UnitProductionRequirement[] requirements = null)
        {
            var definition = new UnitProductionDefinition();
            definition.Configure(displayName, unitType, prefab, cost, duration, supplyCost, outputCount, requirements);
            definition.ConfigureAllowedProductionBuildings(allowedProductionBuildings);
            definition.ApplyBalanceCatalog();
            return definition;
        }

        private static UnitProductionDefinition CreateSpecializedProductionDefinition(
            string displayName,
            PrototypeUnitType unitType,
            GameObject prefab,
            int standardSupplyCost,
            BuildingKind productionBuilding)
        {
            var defaults = new UnitProductionDefinition();
            var buildingRequirement = new UnitProductionRequirement();
            buildingRequirement.ConfigureCompletedBuilding(productionBuilding);
            return CreateProductionDefinition(
                displayName,
                unitType,
                prefab,
                defaults.Cost,
                defaults.ProductionTime,
                standardSupplyCost,
                allowedProductionBuildings: new[] { productionBuilding },
                requirements: new[] { buildingRequirement });
        }

        private static Vector3 Snap(ProjectSTilemapWorld tilemapWorld, Vector3 position)
        {
            if (tilemapWorld == null)
            {
                return position;
            }

            return tilemapWorld.GetCellCenterWorld(tilemapWorld.WorldToCell(ClampToBounds(tilemapWorld, position)));
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

        private static void SetSortingOrder(GameObject root, int sortingOrder)
        {
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = Mathf.Max(renderers[i].sortingOrder, sortingOrder);
                }
            }
        }

        private static Vector3 ClampToBounds(ProjectSTilemapWorld tilemapWorld, Vector3 position)
        {
            var bounds = tilemapWorld.CellBounds;
            return new Vector3(
                Mathf.Clamp(position.x, bounds.xMin + 1f, bounds.xMax - 1f),
                Mathf.Clamp(position.y, bounds.yMin + 1f, bounds.yMax - 1f),
                position.z);
        }

        private static T LoadRequired<T>(string path)
            where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException($"Required asset was not found: {path}", path);
            }

            return asset;
        }

        private static void RemoveExistingSetupRoot()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (var i = transforms.Length - 1; i >= 0; i--)
            {
                var current = transforms[i];
                if (current != null && current.name == SetupRootName && current.parent == null)
                {
                    Object.DestroyImmediate(current.gameObject);
                }
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = Path.Combine(parent, child).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
