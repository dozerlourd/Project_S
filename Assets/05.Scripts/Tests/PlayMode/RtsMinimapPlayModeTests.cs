using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectS.Tilemaps;
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

            var mapWidth = 224f;
            var mapHeight = Mathf.Min(mapWidth, Screen.height - 412f - 116f - 46f);
            var guiCenter = new Vector2(Screen.width - mapWidth - 12f + mapWidth * 0.5f, 116f + mapHeight * 0.5f);
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
