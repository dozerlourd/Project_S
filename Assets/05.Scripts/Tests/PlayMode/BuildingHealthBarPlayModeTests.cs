using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class BuildingHealthBarPlayModeTests
    {
        [UnityTest]
        public IEnumerator HealthBar_MatchesBuildingSpriteScale_AndCapturesPreview()
        {
            var building = new GameObject("Health Bar Scale Test Building");
            var buildingRenderer = building.AddComponent<SpriteRenderer>();
            buildingRenderer.sprite = CreateSprite(260, 260, 100f, new Color(0.1f, 0.3f, 0.7f));
            building.AddComponent<BoxCollider2D>().size = new Vector2(2.6f, 2.6f);

            var statusType = GetGameplayType("ProjectS.Buildings.BuildingStatus");
            building.AddComponent(statusType);
            yield return null;

            var barRenderers = building.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name is "Background" or "Fill")
                .ToArray();
            Assert.That(barRenderers.Length, Is.EqualTo(2));
            var background = barRenderers.Single(renderer => renderer.gameObject.name == "Background");
            Assert.That(background.bounds.size.x, Is.GreaterThanOrEqualTo(buildingRenderer.bounds.size.x * 0.6f));
            Assert.That(background.bounds.size.y, Is.GreaterThanOrEqualTo(0.11f));

            CapturePreview(building);
            Object.Destroy(building);
            yield return null;
        }

        private static Type GetGameplayType(string typeName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Gameplay type was not found: {typeName}");
            return type;
        }

        private static Sprite CreateSprite(int width, int height, float pixelsPerUnit, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(color, width * height).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static void CapturePreview(GameObject building)
        {
            var cameraObject = new GameObject("Health Bar Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.14f);
            camera.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, -10f);

            var renderTexture = new RenderTexture(512, 512, 24);
            var preview = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            preview.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
            preview.Apply();

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "BuildingHealthBarHeadless.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, preview.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.Destroy(renderTexture);
            Object.Destroy(preview);
            Object.Destroy(cameraObject);
        }
    }
}
