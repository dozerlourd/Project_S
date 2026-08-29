using System.IO;
using ProjectS.AI;
using ProjectS.Buildings;
using ProjectS.Resources;
using ProjectS.Tilemaps;
using ProjectS.UI;
using ProjectS.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectS.Editor
{
    public static class MapCreateSceneSetupBuilder
    {
        private const string ScenePath = "Assets/06.Scenes/MapCreate_Scene.unity";
        private const string BuildingPrefabFolder = "Assets/03.Prefabs/Buildings";
        private const string SetupRootName = "ProjectS Match Test Setup";
        private const string MainBasePrefabPath = BuildingPrefabFolder + "/PrototypeMainBase.prefab";
        private const string ProductionPrefabPath = BuildingPrefabFolder + "/PrototypeProductionBuilding.prefab";
        private const string ConstructionSitePrefabPath = BuildingPrefabFolder + "/PrototypeConstructionSite.prefab";
        private const string WorkerPrefabPath = "Assets/03.Prefabs/Units/B_Worker.prefab";
        private const string SoldierPrefabPath = "Assets/03.Prefabs/Units/B_Soldier.prefab";
        private const string SpliterPrefabPath = "Assets/03.Prefabs/Units/B_Spliter.prefab";
        private const string RangerPrefabPath = "Assets/03.Prefabs/Units/B_Ranger.prefab";
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
            var playerWallet = CreateWallet("Player Wallet", UnitTeam.Team1, new ResourceAmount(700, 100), root.transform);
            var aiWallet = CreateWallet("AI Wallet", UnitTeam.Team2, new ResourceAmount(700, 100), root.transform);

            var workerPrefab = LoadRequired<GameObject>(WorkerPrefabPath);
            var soldierPrefab = LoadRequired<GameObject>(SoldierPrefabPath);
            var spliterPrefab = LoadRequired<GameObject>(SpliterPrefabPath);
            var rangerPrefab = LoadRequired<GameObject>(RangerPrefabPath);
            var mainBasePrefab = LoadRequired<GameObject>(MainBasePrefabPath);
            var productionPrefab = LoadRequired<GameObject>(ProductionPrefabPath);
            var constructionSitePrefab = LoadRequired<GameObject>(ConstructionSitePrefabPath);

            var workerDefinitions = new[]
            {
                CreateProductionDefinition("Worker", PrototypeUnitType.Worker, workerPrefab, new ResourceAmount(50, 0), 5f)
            };
            var combatDefinitions = new[]
            {
                CreateProductionDefinition("Soldier", PrototypeUnitType.Soldier, soldierPrefab, new ResourceAmount(100, 0), 7f),
                CreateProductionDefinition("Spliter", PrototypeUnitType.Spliter, spliterPrefab, new ResourceAmount(125, 0), 8f),
                CreateProductionDefinition("Ranger", PrototypeUnitType.Ranger, rangerPrefab, new ResourceAmount(100, 25), 8f)
            };

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
                productionPrefab,
                "Player Production",
                UnitTeam.Team1,
                BuildingKind.Production,
                Snap(tilemapWorld, playerStart + new Vector3(4f, -3f, 0f)),
                playerWallet,
                tilemapWorld,
                combatDefinitions,
                new Vector3(2.5f, -0.5f, 0f),
                new Vector3(5f, -1f, 0f),
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
            InstantiateBuilding(
                productionPrefab,
                "AI Production",
                UnitTeam.Team2,
                BuildingKind.Production,
                Snap(tilemapWorld, aiStart + new Vector3(-4f, 3f, 0f)),
                aiWallet,
                tilemapWorld,
                combatDefinitions,
                new Vector3(-2.5f, 0.5f, 0f),
                new Vector3(-5f, 1f, 0f),
                root.transform);

            CreateResourceCluster(playerStart + new Vector3(-3f, -3f, 0f), root.transform, tilemapWorld);
            CreateResourceCluster(aiStart + new Vector3(3f, 3f, 0f), root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team1, playerStart, workerPrefab, soldierPrefab, spliterPrefab, rangerPrefab, root.transform, tilemapWorld);
            CreateStartingUnits(UnitTeam.Team2, aiStart, workerPrefab, soldierPrefab, spliterPrefab, rangerPrefab, root.transform, tilemapWorld);
            CreatePlayerRuntimeSystems(playerWallet, tilemapWorld, constructionSitePrefab, productionPrefab, root.transform);
            CreateAiController(playerStart, root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreatePrototypeBuildingPrefabs()
        {
            EnsureFolder("Assets/03.Prefabs", "Buildings");
            CreateBuildingPrefab(MainBasePrefabPath, "PrototypeMainBase", BuildingKind.MainBase, true, true, new Vector2(2.6f, 2.2f));
            CreateBuildingPrefab(ProductionPrefabPath, "PrototypeProductionBuilding", BuildingKind.Production, false, true, new Vector2(2.4f, 2f));
            CreateConstructionSitePrefab();
        }

        private static void CreateBuildingPrefab(
            string path,
            string name,
            BuildingKind kind,
            bool dropOff,
            bool production,
            Vector2 size)
        {
            var root = new GameObject(name);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = BuildingSortingOrder;
                var visual = root.AddComponent<PrototypeBuildingVisual>();
                var body = kind == BuildingKind.MainBase
                    ? new Color(0.28f, 0.52f, 0.76f, 1f)
                    : new Color(0.48f, 0.36f, 0.68f, 1f);
                visual.Configure(body, new Color(0.08f, 0.13f, 0.18f, 1f), size);

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

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateConstructionSitePrefab()
        {
            var root = new GameObject("PrototypeConstructionSite");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = BuildingSortingOrder - 1;
                var visual = root.AddComponent<PrototypeBuildingVisual>();
                visual.Configure(new Color(0.52f, 0.48f, 0.4f, 0.85f), new Color(0.95f, 0.82f, 0.38f, 1f), new Vector2(2.2f, 2f));

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(2.2f, 2f);
                collider.isTrigger = true;
                root.AddComponent<ConstructionSite>();

                PrefabUtility.SaveAsPrefabAsset(root, ConstructionSitePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreatePlayerRuntimeSystems(
            PlayerResourceWallet wallet,
            ProjectSTilemapWorld tilemapWorld,
            GameObject constructionSitePrefab,
            GameObject productionPrefab,
            Transform parent)
        {
            var runtime = new GameObject("Player Runtime Systems");
            runtime.transform.SetParent(parent, false);

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

            var hud = runtime.AddComponent<RtsGameHud>();
            hud.Configure(UnitTeam.Team1, placementService);
        }

        private static void CreateAiController(Vector3 fallbackAttackPoint, Transform parent)
        {
            var aiObject = new GameObject("Simple Skirmish AI");
            aiObject.transform.SetParent(parent, false);
            var ai = aiObject.AddComponent<SimpleSkirmishAI>();
            ai.Configure(UnitTeam.Team2, UnitTeam.Team1, 4, 4, fallbackAttackPoint);
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
                node.Configure(ResourceType.Minerals, 1500, 8, 1.2f, 0.95f, true);
            }

            var gas = InstantiateResource(gasPrefab, "Vespene Geyser", center + new Vector3(3f, 0f, 0f), parent, tilemapWorld);
            gas.Configure(ResourceType.Gas, 2500, 6, 1.8f, 1.05f, true);
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
            GameObject soldierPrefab,
            GameObject spliterPrefab,
            GameObject rangerPrefab,
            Transform parent,
            ProjectSTilemapWorld tilemapWorld)
        {
            InstantiateUnit(workerPrefab, $"{team} Worker 1", team, start + new Vector3(-1.5f, -1.5f, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrefab, $"{team} Worker 2", team, start + new Vector3(-0.5f, -2.5f, 0f), parent, tilemapWorld);
            InstantiateUnit(workerPrefab, $"{team} Worker 3", team, start + new Vector3(0.5f, -1.5f, 0f), parent, tilemapWorld);
            InstantiateUnit(soldierPrefab, $"{team} Soldier 1", team, start + new Vector3(3f, 1f, 0f), parent, tilemapWorld);
            InstantiateUnit(soldierPrefab, $"{team} Soldier 2", team, start + new Vector3(4f, 0f, 0f), parent, tilemapWorld);
            InstantiateUnit(spliterPrefab, $"{team} Spliter", team, start + new Vector3(3f, -1f, 0f), parent, tilemapWorld);
            InstantiateUnit(rangerPrefab, $"{team} Ranger", team, start + new Vector3(4f, -2f, 0f), parent, tilemapWorld);
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
            float duration)
        {
            var definition = new UnitProductionDefinition();
            definition.Configure(displayName, unitType, prefab, cost, duration);
            return definition;
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
