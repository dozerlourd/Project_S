using ProjectS.Tilemaps;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Editor
{
    [CustomEditor(typeof(ProjectSTile))]
    public sealed class ProjectSTileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var projectTile = (ProjectSTile)target;
            if (projectTile.sprite == null)
            {
                return;
            }

            GUILayout.Space(8f);
            var previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(96f));
            DrawSprite(previewRect, projectTile.sprite);
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
        {
            var projectTile = (ProjectSTile)target;
            if (projectTile == null || projectTile.sprite == null)
            {
                return base.RenderStaticPreview(assetPath, subAssets, width, height);
            }

            return CreateSpritePreview(projectTile.sprite, width, height);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            var texture = sprite.texture;
            var textureRect = sprite.textureRect;
            var texCoords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
        }

        private static Texture2D CreateSpritePreview(Sprite sprite, int width, int height)
        {
            var texture = sprite.texture;
            var textureRect = sprite.textureRect;
            var texCoords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, width, height, 0);

            var aspectScale = Mathf.Min(width / textureRect.width, height / textureRect.height);
            var drawWidth = textureRect.width * aspectScale;
            var drawHeight = textureRect.height * aspectScale;
            var drawRect = new Rect(
                (width - drawWidth) * 0.5f,
                (height - drawHeight) * 0.5f,
                drawWidth,
                drawHeight);

            Graphics.DrawTexture(drawRect, texture, texCoords, 0, 0, 0, 0);
            GL.PopMatrix();

            var result = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return result;
        }
    }
}
