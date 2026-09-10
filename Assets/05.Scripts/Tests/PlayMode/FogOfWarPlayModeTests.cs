using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Tilemaps;
using ProjectS.Units;
using ProjectS.Visibility;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class FogOfWarPlayModeTests
    {
        [UnityTest]
        public IEnumerator Visibility_ChangesOnlyWhenProviderChangesCells()
        {
            var worldRoot = CreateWorld(6, out _, out var groundTile);
            var world = worldRoot.GetComponent<ProjectSTilemapWorld>();
            var provider = CreateUnit("Vision Provider", new Vector3(1.5f, 0.5f), UnitTeam.Team1, 1.1f);
            var manager = GetOrCreateManager();
            manager.Configure(world, UnitTeam.Team1);

            Assert.That(manager.GetVisibility(new Vector3Int(1, 0)), Is.EqualTo(FogVisibilityState.Visible));
            Assert.That(manager.GetVisibility(new Vector3Int(4, 0)), Is.EqualTo(FogVisibilityState.Unexplored));
            var overlayMesh = manager.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(overlayMesh, Is.Not.Null);
            Assert.That(overlayMesh.vertexCount, Is.EqualTo(6 * 4));
            var rebuildCount = manager.RebuildCount;

            yield return null;
            yield return null;
            Assert.That(manager.RebuildCount, Is.EqualTo(rebuildCount));

            provider.transform.position = new Vector3(3.5f, 0.5f);
            yield return null;
            yield return null;

            Assert.That(manager.GetVisibility(new Vector3Int(1, 0)), Is.EqualTo(FogVisibilityState.Explored));
            Assert.That(manager.GetVisibility(new Vector3Int(3, 0)), Is.EqualTo(FogVisibilityState.Visible));
            Assert.That(manager.RebuildCount, Is.GreaterThan(rebuildCount));

            Object.Destroy(provider);
            Object.Destroy(worldRoot);
            Object.Destroy(groundTile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlocksVisionTile_HidesCellsBehindItUntilTerrainChanges()
        {
            var worldRoot = CreateWorld(6, out _, out var groundTile);
            var obstacleObject = new GameObject("Obstacle");
            obstacleObject.transform.SetParent(worldRoot.transform, false);
            var obstacleTilemap = obstacleObject.AddComponent<Tilemap>();
            var obstacleTile = ScriptableObject.CreateInstance<ProjectSTile>();
            SetPrivateField(obstacleTile, "blocksVision", true);
            obstacleTilemap.SetTile(new Vector3Int(2, 0), obstacleTile);
            var world = worldRoot.GetComponent<ProjectSTilemapWorld>();
            world.MarkNavigationCacheDirty();
            world.RebuildNavigationCache();
            var provider = CreateUnit("Blocked Vision Provider", new Vector3(0.5f, 0.5f), UnitTeam.Team1, 6f);
            var manager = GetOrCreateManager();
            manager.Configure(world, UnitTeam.Team1);

            Assert.That(manager.GetVisibility(new Vector3Int(2, 0)), Is.EqualTo(FogVisibilityState.Visible));
            Assert.That(manager.GetVisibility(new Vector3Int(3, 0)), Is.EqualTo(FogVisibilityState.Unexplored));

            obstacleTilemap.SetTile(new Vector3Int(2, 0), null);
            yield return null;
            yield return null;

            Assert.That(manager.GetVisibility(new Vector3Int(3, 0)), Is.EqualTo(FogVisibilityState.Visible));

            Object.Destroy(provider);
            Object.Destroy(worldRoot);
            Object.Destroy(groundTile);
            Object.Destroy(obstacleTile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Minimap_HidesEnemyMarkersOutsideCurrentVision()
        {
            var worldRoot = CreateWorld(6, out _, out var groundTile);
            var world = worldRoot.GetComponent<ProjectSTilemapWorld>();
            var provider = CreateUnit("Minimap Vision Provider", new Vector3(0.5f, 0.5f), UnitTeam.Team1, 1.1f);
            var enemy = CreateUnit("Hidden Minimap Enemy", new Vector3(4.5f, 0.5f), UnitTeam.Team2, 1f);
            var manager = GetOrCreateManager();
            manager.Configure(world, UnitTeam.Team1);
            var minimapObject = new GameObject("Fog Minimap");
            var minimap = minimapObject.AddComponent(GetGameplayType("ProjectS.UI.RtsMinimap"));
            SetPrivateField(minimap, "tilemapWorld", world);
            SetPrivateField(minimap, "fogOfWar", manager);

            Assert.That(ShouldDisplay(minimap, UnitTeam.Team2, enemy.transform.position), Is.False);
            Assert.That(ShouldDisplay(minimap, UnitTeam.Team1, provider.transform.position), Is.True);

            provider.transform.position = new Vector3(3.5f, 0.5f);
            yield return null;
            yield return null;

            Assert.That(ShouldDisplay(minimap, UnitTeam.Team2, enemy.transform.position), Is.True);

            Object.Destroy(minimapObject);
            Object.Destroy(provider);
            Object.Destroy(enemy);
            Object.Destroy(worldRoot);
            Object.Destroy(groundTile);
            yield return null;
        }

        private static GameObject CreateWorld(int width, out Tilemap tilemap, out Tile groundTile)
        {
            var root = new GameObject("Fog Test Grid");
            root.AddComponent<Grid>();
            var tilemapObject = new GameObject("Ground");
            tilemapObject.transform.SetParent(root.transform, false);
            tilemap = tilemapObject.AddComponent<Tilemap>();
            groundTile = ScriptableObject.CreateInstance<Tile>();
            for (var x = 0; x < width; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0), groundTile);
            }

            var world = root.AddComponent<ProjectSTilemapWorld>();
            world.RebuildNavigationCache();
            return root;
        }

        private static GameObject CreateUnit(string name, Vector3 position, UnitTeam team, float visionRadius)
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            unit.AddComponent<BoxCollider2D>().isTrigger = true;
            var status = unit.AddComponent<PrototypeUnitStatus>();
            status.Initialize(
                UnitTrial.Human,
                team,
                PrototypeUnitType.Worker,
                MovementDomain.Ground,
                UnitRole.Resource,
                AttackDistanceType.Melee,
                AttackPowerType.Physical,
                PlacementType.Movable,
                UnitGrade.Common,
                AttackTargetType.SingleTarget,
                100f,
                0f,
                0f,
                1f,
                3f,
                1f,
                3f,
                1,
                Vector2Int.one,
                true,
                false,
                0f);
            status.ConfigureVisionRadius(visionRadius);
            return unit;
        }

        private static FogOfWarManager GetOrCreateManager()
        {
            if (FogOfWarManager.ActiveInstance != null)
            {
                return FogOfWarManager.ActiveInstance;
            }

            return new GameObject("Fog Test Manager").AddComponent<FogOfWarManager>();
        }

        private static bool ShouldDisplay(Component minimap, UnitTeam team, Vector3 position)
        {
            var method = minimap.GetType().GetMethod("ShouldDisplayEntity", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(minimap, new object[] { team, position });
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve gameplay type {typeName}.");
            return type;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find field {name}.");
            field.SetValue(target, value);
        }
    }
}
