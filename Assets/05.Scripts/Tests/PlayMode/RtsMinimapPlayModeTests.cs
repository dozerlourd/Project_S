using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Tilemaps;
using ProjectS.Units;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class RtsMinimapPlayModeTests
    {
        [UnityTest]
        public IEnumerator TilemapWorld_ReportsActualCellWorldBounds()
        {
            var gridObject = new GameObject("Minimap Test Grid");
            gridObject.AddComponent<Grid>();
            var tilemapObject = new GameObject("Minimap Test Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var world = tilemapObject.AddComponent<ProjectSTilemapWorld>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tilemap.SetTile(new Vector3Int(2, 3, 0), tile);

            yield return null;

            Assert.That(world.TryGetWorldBounds(out var worldBounds), Is.True);
            Assert.That(worldBounds.min.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(worldBounds.min.y, Is.EqualTo(3f).Within(0.001f));
            Assert.That(worldBounds.max.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(worldBounds.max.y, Is.EqualTo(4f).Within(0.001f));

            Object.Destroy(gridObject);
            Object.Destroy(tile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RtsMinimap_MapsGuiCenterToTilemapWorldCenter()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            var originalFullScreen = Screen.fullScreen;
            Screen.SetResolution(1280, 720, false);
            yield return null;

            var gridObject = new GameObject("Minimap Mapping Grid");
            gridObject.AddComponent<Grid>();
            var tilemapObject = new GameObject("Minimap Mapping Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var world = tilemapObject.AddComponent<ProjectSTilemapWorld>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tilemap.SetTile(new Vector3Int(0, 0, 0), tile);
            tilemap.SetTile(new Vector3Int(9, 9, 0), tile);

            var minimapObject = new GameObject("Minimap Mapping UI");
            var minimap = minimapObject.AddComponent(GetGameplayType("ProjectS.UI.RtsMinimap"));
            SetPrivateField(minimap, "tilemapWorld", world);

            yield return null;

            var mapWidth = 448f;
            var mapHeight = 288f;
            var guiCenter = new Vector2(
                Screen.width - mapWidth - 12f + mapWidth * 0.5f,
                Screen.height - 12f - 42f - 8f - mapHeight + mapHeight * 0.5f);
            var arguments = new object[] { guiCenter, null };
            var mapped = (bool)minimap.GetType().GetMethod("TryGetWorldPoint").Invoke(minimap, arguments);

            Assert.That(mapped, Is.True);
            var point = (Vector3)arguments[1];
            Assert.That(point.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(point.y, Is.EqualTo(5f).Within(0.001f));

            Object.Destroy(gridObject);
            Object.Destroy(minimapObject);
            Object.Destroy(tile);
            Screen.SetResolution(originalWidth, originalHeight, originalFullScreen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RtsMinimap_CachesWalkableBlockedAndNonBuildableTerrainSeparately()
        {
            var gridObject = new GameObject("Minimap Terrain Grid");
            gridObject.AddComponent<Grid>();
            var tilemapObject = new GameObject("Minimap Terrain Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var world = tilemapObject.AddComponent<ProjectSTilemapWorld>();
            var walkableTile = ScriptableObject.CreateInstance<ProjectSTile>();
            var blockedTile = ScriptableObject.CreateInstance<ProjectSTile>();
            var nonBuildableTile = ScriptableObject.CreateInstance<ProjectSTile>();
            SetPrivateField(blockedTile, "blocksMovement", true);
            SetPrivateField(nonBuildableTile, "blocksConstruction", true);
            tilemap.SetTile(new Vector3Int(0, 0, 0), walkableTile);
            tilemap.SetTile(new Vector3Int(1, 0, 0), blockedTile);
            tilemap.SetTile(new Vector3Int(2, 0, 0), nonBuildableTile);
            world.RebuildNavigationCache();

            var minimapObject = new GameObject("Minimap Terrain UI");
            var minimap = minimapObject.AddComponent(GetGameplayType("ProjectS.UI.RtsMinimap"));
            SetPrivateField(minimap, "tilemapWorld", world);
            InvokePrivate(minimap, "RebuildTerrainTextureIfNeeded");
            var terrainTexture = GetPrivateField<Texture2D>(minimap, "terrainTexture");

            Assert.That(terrainTexture, Is.Not.Null);
            Assert.That(terrainTexture.width, Is.EqualTo(3));
            Assert.That(terrainTexture.height, Is.EqualTo(1));
            Assert.That(terrainTexture.GetPixel(0, 0), Is.Not.EqualTo(terrainTexture.GetPixel(1, 0)));
            Assert.That(terrainTexture.GetPixel(0, 0), Is.Not.EqualTo(terrainTexture.GetPixel(2, 0)));
            Assert.That(terrainTexture.GetPixel(1, 0), Is.Not.EqualTo(terrainTexture.GetPixel(2, 0)));

            Object.Destroy(minimapObject);
            Object.Destroy(gridObject);
            Object.Destroy(walkableTile);
            Object.Destroy(blockedTile);
            Object.Destroy(nonBuildableTile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerCommandController_TreatsMinimapPointAsRuntimeHud()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            var originalFullScreen = Screen.fullScreen;
            Screen.SetResolution(1280, 720, false);
            yield return null;

            var minimapObject = new GameObject("Input Blocking Minimap");
            minimapObject.AddComponent(GetGameplayType("ProjectS.UI.RtsMinimap"));
            yield return null;

            var mapWidth = 448f;
            var mapHeight = 288f;
            var guiPointInsideMinimap = new Vector2(
                Screen.width - mapWidth - 12f + mapWidth * 0.5f,
                Screen.height - 12f - 42f - 8f - mapHeight + mapHeight * 0.5f);
            var screenPointInsideMinimap = new Vector2(guiPointInsideMinimap.x, Screen.height - guiPointInsideMinimap.y);
            var controllerType = GetGameplayType("ProjectS.Units.PlayerUnitCommandController");
            var method = controllerType.GetMethod("IsPointerOverRuntimeHud", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Could not find IsPointerOverRuntimeHud.");

            var isBlocked = (bool)method.Invoke(null, new object[] { screenPointInsideMinimap });

            Assert.That(isBlocked, Is.True);

            Object.Destroy(minimapObject);
            Screen.SetResolution(originalWidth, originalHeight, originalFullScreen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RtsMinimap_RightClickIssuesSelectedUnitCommandOnce()
        {
            var controllerObject = new GameObject("Minimap Command Controller");
            var controller = controllerObject.AddComponent<PlayerUnitCommandController>();
            var unitObject = new GameObject("Minimap Command Unit");
            unitObject.transform.position = Vector3.zero;
            unitObject.AddComponent<BoxCollider2D>();
            var status = unitObject.AddComponent<PrototypeUnitStatus>();
            status.ConfigurePrototypeDefaults(PrototypeUnitType.Worker, UnitTeam.Team1);
            var commandAgent = unitObject.GetComponent<UnitCommandAgent>();
            GetPrivateField<List<UnitCommandAgent>>(controller, "selectedUnits").Add(commandAgent);
            yield return null;

            var minimapType = GetGameplayType("ProjectS.UI.RtsMinimap");
            var method = minimapType.GetMethod("TryHandleUnitCommand", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Could not find TryHandleUnitCommand.");

            var mapRect = new Rect(100f, 100f, 448f, 288f);
            var worldBounds = new Bounds(new Vector3(5f, 5f, 0f), new Vector3(10f, 10f, 0f));
            var guiPoint = new Vector2(mapRect.xMin + mapRect.width * 0.75f, mapRect.yMin + mapRect.height * 0.25f);
            var handled = (bool)method.Invoke(null, new object[] { guiPoint, mapRect, worldBounds, false });

            Assert.That(handled, Is.True);
            Assert.That(commandAgent.Mode, Is.EqualTo(UnitCommandMode.Move));
            Assert.That(commandAgent.CommandDestination.x, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(commandAgent.CommandDestination.y, Is.EqualTo(7.5f).Within(0.001f));

            Object.Destroy(unitObject);
            Object.Destroy(controllerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NamedObstacleTilemapChanges_RefreshNavigationAndMinimapTerrain()
        {
            var gridObject = new GameObject("Obstacle Cache Grid");
            gridObject.AddComponent<Grid>();
            var groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(gridObject.transform, false);
            var groundTilemap = groundObject.AddComponent<Tilemap>();
            var obstacleObject = new GameObject("Obstacle");
            obstacleObject.transform.SetParent(gridObject.transform, false);
            var obstacleTilemap = obstacleObject.AddComponent<Tilemap>();
            var world = gridObject.AddComponent<ProjectSTilemapWorld>();
            var navigator = gridObject.AddComponent<ProjectSTilemapNavigator>();
            var groundTile = ScriptableObject.CreateInstance<Tile>();
            var obstacleTile = ScriptableObject.CreateInstance<Tile>();
            var changedCell = new Vector3Int(1, 0, 0);
            groundTilemap.SetTile(new Vector3Int(0, 0, 0), groundTile);
            groundTilemap.SetTile(changedCell, groundTile);
            groundTilemap.SetTile(new Vector3Int(2, 0, 0), groundTile);

            yield return null;

            Assert.That(world.IsWalkable(changedCell), Is.True);
            var cacheRebuildCount = world.CacheRebuildCount;

            var minimapObject = new GameObject("Obstacle Cache Minimap");
            var minimap = minimapObject.AddComponent(GetGameplayType("ProjectS.UI.RtsMinimap"));
            SetPrivateField(minimap, "tilemapWorld", world);
            InvokePrivate(minimap, "RebuildTerrainTextureIfNeeded");
            var terrainTexture = GetPrivateField<Texture2D>(minimap, "terrainTexture");
            var walkableColor = terrainTexture.GetPixel(1, 0);

            obstacleTilemap.SetTile(changedCell, obstacleTile);

            Assert.That(world.IsWalkable(changedCell), Is.False);
            Assert.That(world.IsBuildable(changedCell), Is.False);
            Assert.That(world.CacheRebuildCount, Is.GreaterThan(cacheRebuildCount));
            Assert.That(navigator.TryFindPath(
                world.GetCellCenterWorld(new Vector3Int(0, 0, 0)),
                world.GetCellCenterWorld(new Vector3Int(2, 0, 0)),
                new List<Vector3>()), Is.False);

            InvokePrivate(minimap, "RebuildTerrainTextureIfNeeded");
            Assert.That(terrainTexture.GetPixel(1, 0), Is.Not.EqualTo(walkableColor));

            obstacleTilemap.SetTile(changedCell, null);
            var removalCacheRebuildCount = world.CacheRebuildCount;
            Assert.That(world.IsWalkable(changedCell), Is.True);
            Assert.That(world.IsBuildable(changedCell), Is.True);
            Assert.That(world.CacheRebuildCount, Is.GreaterThan(removalCacheRebuildCount));
            Assert.That(navigator.TryFindPath(
                world.GetCellCenterWorld(new Vector3Int(0, 0, 0)),
                world.GetCellCenterWorld(new Vector3Int(2, 0, 0)),
                new List<Vector3>()), Is.True);
            InvokePrivate(minimap, "RebuildTerrainTextureIfNeeded");
            Assert.That(terrainTexture.GetPixel(1, 0), Is.EqualTo(walkableColor));

            Object.Destroy(minimapObject);
            Object.Destroy(gridObject);
            Object.Destroy(groundTile);
            Object.Destroy(obstacleTile);
            yield return null;
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length && type == null; i++)
                {
                    type = assemblies[i].GetType(typeName, false);
                }
            }

            Assert.That(type, Is.Not.Null, $"Could not resolve gameplay type {typeName}.");
            return type;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find field {name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find field {name}.");
            return (T)field.GetValue(target);
        }

        private static object InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not find method {name}.");
            return method.Invoke(target, null);
        }
    }
}
