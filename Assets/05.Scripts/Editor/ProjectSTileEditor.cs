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
            EditorGUI.DrawPreviewTexture(previewRect, projectTile.sprite.texture, null, ScaleMode.ScaleToFit);
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var projectTile = (ProjectSTile)target;
            if (projectTile == null || projectTile.sprite == null)
            {
                return base.RenderStaticPreview(assetPath, subAssets, width, height);
            }

            var preview = AssetPreview.GetAssetPreview(projectTile.sprite);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(projectTile.sprite) as Texture2D;
            }

            return preview != null
                ? ResizePreview(preview, width, height)
                : base.RenderStaticPreview(assetPath, subAssets, width, height);
        }

        private static Texture2D ResizePreview(Texture source, int width, int height)
        {
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            var result = new Texture2D(width, height, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);

            return result;
        }
    }
}
