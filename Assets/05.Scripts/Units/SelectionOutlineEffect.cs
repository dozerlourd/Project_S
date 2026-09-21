using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectS.Units
{
    [DisallowMultipleComponent]
    public sealed class SelectionOutlineEffect : MonoBehaviour
    {
        private const string OutlineRootName = "SelectionOutline";
        private const string ShaderName = "ProjectS/SpriteSelectionOutline";
        private const int MaxSourceRefreshIntervalFrames = 15;
        private const int VisibilitySyncIntervalFrames = 5;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int InnerColorId = Shader.PropertyToID("_InnerColor");
        private static readonly int OuterColorId = Shader.PropertyToID("_OuterColor");
        private static readonly int UvMinMaxId = Shader.PropertyToID("_UvMinMax");
        private static readonly int InnerWidthId = Shader.PropertyToID("_InnerWidth");
        private static readonly int OuterWidthId = Shader.PropertyToID("_OuterWidth");
        private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
        private static Material sharedOutlineMaterial;

        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Color innerColor = new Color(1f, 0.72f, 0.18f, 0.98f);
        [SerializeField] private Color outerColor = new Color(0.78f, 0.34f, 0.03f, 0.9f);
        [SerializeField, Min(0.25f)] private float innerWidthPixels = 45f;
        [SerializeField, Min(0.25f)] private float outerWidthPixels = 100f;
        [SerializeField, Range(0.001f, 0.99f)] private float alphaThreshold = 0.08f;
        [SerializeField] private bool visibleOnEnable;
        [SerializeField] private int sortingOrderOffset = -1;
        [SerializeField] private string[] excludedAncestorNames =
        {
            "HealthBar",
            "BuildingHealthBar",
            "ConstructionProgressBar",
            "RangeIndicator",
            "SelectionRing",
            "TargetRing",
            "Shadow",
            "AttackEffect",
            "Feedback"
        };

        private Transform outlineRoot;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh outlineMesh;
        private MaterialPropertyBlock propertyBlock;
        private bool outlineVisible;
        private int nextSourceRefreshFrame;
        private int nextVisibilitySyncFrame;

        private SpriteRenderer lastSourceRenderer;
        private Sprite lastSprite;
        private Texture lastTexture;
        private Vector4 lastRendererLocalMinMax;
        private bool lastFlipX;
        private bool lastFlipY;
        private Color lastSourceColor;
        private string lastSortingLayerName;
        private int lastSortingOrder;
        private float lastInnerWidthPixels;
        private float lastOuterWidthPixels;
        private Color lastInnerColor;
        private Color lastOuterColor;
        private float lastAlphaThreshold;
        private int lastSortingOrderOffset;

        public SpriteRenderer SourceRenderer => sourceRenderer;
        public bool IsOutlineVisible => outlineVisible;

        private void Awake()
        {
            outlineVisible = visibleOnEnable;
        }

        private void OnEnable()
        {
            outlineVisible = visibleOnEnable;
            if (outlineVisible)
            {
                EnsureOutlineRenderer();
                SyncOutline(true);
                return;
            }

            ResolveSourceRenderer();
            SetRendererVisible(false);
        }

        private void LateUpdate()
        {
            if (!outlineVisible)
            {
                return;
            }

            if (Time.frameCount < nextVisibilitySyncFrame)
            {
                return;
            }

            nextVisibilitySyncFrame = Time.frameCount + VisibilitySyncIntervalFrames;
            RefreshSourceIfNeeded();
            if (meshRenderer == null)
            {
                EnsureOutlineRenderer();
                SyncOutline(true);
                return;
            }

            SyncOutline(false);
        }

        private void OnDisable()
        {
            SetRendererVisible(false);
        }

        private void OnDestroy()
        {
            DestroyOutlineRenderer();
        }

        public void Configure(Color color, SpriteRenderer renderer = null, int orderOffset = -1)
        {
            Configure(color, GetOuterColor(color), renderer, orderOffset);
        }

        public void Configure(Color inner, Color outer, SpriteRenderer renderer = null, int orderOffset = -1)
        {
            innerColor = inner;
            outerColor = outer;
            sortingOrderOffset = orderOffset;
            if (renderer != null)
            {
                sourceRenderer = renderer;
            }

            InvalidateCachedState();
            if (outlineVisible)
            {
                EnsureOutlineRenderer();
                SyncOutline(true);
            }
        }

        public void SetSourceRenderer(SpriteRenderer renderer)
        {
            if (sourceRenderer == renderer)
            {
                return;
            }

            sourceRenderer = renderer;
            RebuildForSourceChange();
        }

        public void SetOutlineVisible(bool visible)
        {
            outlineVisible = visible;
            if (!outlineVisible)
            {
                SetRendererVisible(false);
                return;
            }

            EnsureOutlineRenderer();
            SyncOutline(true);
        }

        public void Prepare()
        {
            ResolveSourceRenderer();
            SetRendererVisible(false);
        }

        public bool OwnsRenderer(Renderer renderer)
        {
            return renderer != null && renderer == meshRenderer;
        }

        private void EnsureOutlineRenderer()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveSourceRenderer();
            if (sourceRenderer == null)
            {
                SetRendererVisible(false);
                return;
            }

            if (outlineRoot != null && meshFilter != null && meshRenderer != null && outlineRoot.parent == sourceRenderer.transform)
            {
                return;
            }

            DestroyOutlineRenderer();
            var outlineObject = new GameObject(OutlineRootName);
            outlineRoot = outlineObject.transform;
            outlineRoot.SetParent(sourceRenderer.transform, false);
            outlineRoot.localPosition = Vector3.zero;
            outlineRoot.localRotation = Quaternion.identity;
            outlineRoot.localScale = Vector3.one;

            meshFilter = outlineObject.AddComponent<MeshFilter>();
            meshRenderer = outlineObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetSharedOutlineMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineMesh = new Mesh
            {
                name = "SelectionOutlineMesh"
            };
            meshFilter.sharedMesh = outlineMesh;
            propertyBlock ??= new MaterialPropertyBlock();
            InvalidateCachedState();
        }

        private void RefreshSourceIfNeeded()
        {
            if (Time.frameCount < nextSourceRefreshFrame && IsValidSourceRenderer(sourceRenderer))
            {
                return;
            }

            nextSourceRefreshFrame = Time.frameCount + MaxSourceRefreshIntervalFrames;
            var previous = sourceRenderer;
            ResolveSourceRenderer();
            if (sourceRenderer != previous)
            {
                RebuildForSourceChange();
            }
        }

        private void ResolveSourceRenderer()
        {
            if (IsValidSourceRenderer(sourceRenderer))
            {
                return;
            }

            sourceRenderer = null;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var bestScore = int.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!IsValidSourceRenderer(renderer))
                {
                    continue;
                }

                var score = GetSourceRendererScore(renderer);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                sourceRenderer = renderer;
            }
        }

        private bool IsValidSourceRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return false;
            }

            if (outlineRoot != null && renderer.transform.IsChildOf(outlineRoot))
            {
                return false;
            }

            return renderer.GetComponentInParent<SelectionOutlineEffect>() == this
                && !HasExcludedAncestor(renderer.transform);
        }

        private int GetSourceRendererScore(SpriteRenderer renderer)
        {
            var score = renderer.sortingOrder;
            if (renderer.transform == transform)
            {
                score += 10000;
            }

            if (renderer.enabled)
            {
                score += 1000;
            }

            if (renderer.gameObject.activeInHierarchy)
            {
                score += 100;
            }

            if (renderer.name.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 50;
            }

            return score;
        }

        private bool HasExcludedAncestor(Transform candidate)
        {
            while (candidate != null && candidate != transform.parent)
            {
                if (IsExcludedName(candidate.name))
                {
                    return true;
                }

                if (candidate == transform)
                {
                    break;
                }

                candidate = candidate.parent;
            }

            return false;
        }

        private bool IsExcludedName(string objectName)
        {
            if (excludedAncestorNames == null || string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            for (var i = 0; i < excludedAncestorNames.Length; i++)
            {
                var excludedName = excludedAncestorNames[i];
                if (!string.IsNullOrWhiteSpace(excludedName)
                    && objectName.IndexOf(excludedName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SyncOutline(bool force)
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null || meshRenderer == null || meshFilter == null)
            {
                SetRendererVisible(false);
                return;
            }

            var sprite = sourceRenderer.sprite;
            var texture = sprite.texture;
            if (texture == null)
            {
                SetRendererVisible(false);
                return;
            }

            var sourceVisible = sourceRenderer.enabled && sourceRenderer.gameObject.activeInHierarchy;
            var rendererLocalMinMax = GetRendererLocalMinMax(sourceRenderer);
            var changed = force
                || sourceRenderer != lastSourceRenderer
                || sprite != lastSprite
                || texture != lastTexture
                || rendererLocalMinMax != lastRendererLocalMinMax
                || sourceRenderer.flipX != lastFlipX
                || sourceRenderer.flipY != lastFlipY
                || sourceRenderer.color != lastSourceColor
                || sourceRenderer.sortingLayerName != lastSortingLayerName
                || sourceRenderer.sortingOrder != lastSortingOrder
                || !Mathf.Approximately(innerWidthPixels, lastInnerWidthPixels)
                || !Mathf.Approximately(outerWidthPixels, lastOuterWidthPixels)
                || innerColor != lastInnerColor
                || outerColor != lastOuterColor
                || !Mathf.Approximately(alphaThreshold, lastAlphaThreshold)
                || sortingOrderOffset != lastSortingOrderOffset;

            if (!changed)
            {
                SetRendererVisible(sourceVisible);
                return;
            }

            RebuildMesh(sprite, rendererLocalMinMax);
            ApplyRendererState(sprite, texture, sourceVisible);
            lastSourceRenderer = sourceRenderer;
            lastSprite = sprite;
            lastTexture = texture;
            lastRendererLocalMinMax = rendererLocalMinMax;
            lastFlipX = sourceRenderer.flipX;
            lastFlipY = sourceRenderer.flipY;
            lastSourceColor = sourceRenderer.color;
            lastSortingLayerName = sourceRenderer.sortingLayerName;
            lastSortingOrder = sourceRenderer.sortingOrder;
            lastInnerWidthPixels = innerWidthPixels;
            lastOuterWidthPixels = outerWidthPixels;
            lastInnerColor = innerColor;
            lastOuterColor = outerColor;
            lastAlphaThreshold = alphaThreshold;
            lastSortingOrderOffset = sortingOrderOffset;
        }

        private void RebuildMesh(Sprite sprite, Vector4 rendererLocalMinMax)
        {
            if (outlineMesh == null)
            {
                outlineMesh = new Mesh { name = "SelectionOutlineMesh" };
                meshFilter.sharedMesh = outlineMesh;
            }

            var texture = sprite.texture;
            var uvMinMax = GetSpriteUvMinMax(sprite);
            var maxOutlinePixels = Mathf.Max(innerWidthPixels, outerWidthPixels);
            var textureSize = new Vector2(Mathf.Max(1, texture.width), Mathf.Max(1, texture.height));
            var uvPadding = new Vector2(maxOutlinePixels / textureSize.x, maxOutlinePixels / textureSize.y);
            var localPadding = GetLocalPaddingForSpritePixels(sprite, rendererLocalMinMax, maxOutlinePixels);

            var min = new Vector2(rendererLocalMinMax.x - localPadding.x, rendererLocalMinMax.y - localPadding.y);
            var max = new Vector2(rendererLocalMinMax.z + localPadding.x, rendererLocalMinMax.w + localPadding.y);

            var uvMin = new Vector2(uvMinMax.x, uvMinMax.y) - uvPadding;
            var uvMax = new Vector2(uvMinMax.z, uvMinMax.w) + uvPadding;
            if (sourceRenderer.flipX)
            {
                (uvMin.x, uvMax.x) = (uvMax.x, uvMin.x);
            }

            if (sourceRenderer.flipY)
            {
                (uvMin.y, uvMax.y) = (uvMax.y, uvMin.y);
            }

            outlineMesh.Clear();
            outlineMesh.vertices = new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, max.y, 0f),
                new Vector3(max.x, min.y, 0f)
            };
            outlineMesh.uv = new[]
            {
                new Vector2(uvMin.x, uvMin.y),
                new Vector2(uvMin.x, uvMax.y),
                new Vector2(uvMax.x, uvMax.y),
                new Vector2(uvMax.x, uvMin.y)
            };
            outlineMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            outlineMesh.RecalculateBounds();
        }

        private void ApplyRendererState(Sprite sprite, Texture texture, bool sourceVisible)
        {
            meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            meshRenderer.sortingLayerName = sourceRenderer.sortingLayerName;
            meshRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            meshRenderer.sharedMaterial = GetSharedOutlineMaterial();

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetTexture(MainTexId, texture);
            propertyBlock.SetColor(InnerColorId, MultiplyAlpha(innerColor, sourceRenderer.color.a));
            propertyBlock.SetColor(OuterColorId, MultiplyAlpha(outerColor, sourceRenderer.color.a));
            propertyBlock.SetVector(UvMinMaxId, GetSpriteUvMinMax(sprite));
            propertyBlock.SetFloat(InnerWidthId, Mathf.Max(0.25f, innerWidthPixels));
            propertyBlock.SetFloat(OuterWidthId, Mathf.Max(innerWidthPixels, outerWidthPixels));
            propertyBlock.SetFloat(AlphaThresholdId, alphaThreshold);
            meshRenderer.SetPropertyBlock(propertyBlock);
            SetRendererVisible(sourceVisible);
        }

        private static Vector4 GetRendererLocalMinMax(SpriteRenderer renderer)
        {
            var bounds = renderer.localBounds;
            return new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
        }

        private static Vector2 GetLocalPaddingForSpritePixels(Sprite sprite, Vector4 rendererLocalMinMax, float pixelPadding)
        {
            var rect = sprite.rect;
            var rendererSize = new Vector2(
                Mathf.Abs(rendererLocalMinMax.z - rendererLocalMinMax.x),
                Mathf.Abs(rendererLocalMinMax.w - rendererLocalMinMax.y));

            var localPerPixel = new Vector2(
                rendererSize.x / Mathf.Max(1f, rect.width),
                rendererSize.y / Mathf.Max(1f, rect.height));

            return new Vector2(pixelPadding * localPerPixel.x, pixelPadding * localPerPixel.y);
        }

        private static Vector4 GetSpriteUvMinMax(Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture != null)
            {
                try
                {
                    var textureRect = sprite.textureRect;
                    var textureSize = new Vector2(Mathf.Max(1, texture.width), Mathf.Max(1, texture.height));
                    return new Vector4(
                        textureRect.xMin / textureSize.x,
                        textureRect.yMin / textureSize.y,
                        textureRect.xMax / textureSize.x,
                        textureRect.yMax / textureSize.y);
                }
                catch (UnityException)
                {
                    // Tight-packed sprites may reject textureRect access. Fall back to the imported UV extent.
                }
            }

            var uv = sprite.uv;
            var min = uv[0];
            var max = uv[0];
            for (var i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            return new Vector4(min.x, min.y, max.x, max.y);
        }

        private static Color MultiplyAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, color.a * alpha);
        }

        private void SetRendererVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }
        }

        private void DestroyOutlineRenderer()
        {
            var meshToDestroy = outlineMesh;
            if (outlineRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(outlineRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(outlineRoot.gameObject);
                }
            }

            if (meshToDestroy != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(meshToDestroy);
                }
                else
                {
                    DestroyImmediate(meshToDestroy);
                }
            }

            outlineRoot = null;
            meshFilter = null;
            meshRenderer = null;
            outlineMesh = null;
            InvalidateCachedState();
        }

        private void RebuildForSourceChange()
        {
            DestroyOutlineRenderer();
            InvalidateCachedState();
            if (outlineVisible)
            {
                EnsureOutlineRenderer();
                SyncOutline(true);
            }
        }

        private void InvalidateCachedState()
        {
            lastSourceRenderer = null;
            lastSprite = null;
            lastTexture = null;
            lastRendererLocalMinMax = default;
            lastFlipX = false;
            lastFlipY = false;
            lastSourceColor = default;
            lastSortingLayerName = null;
            lastSortingOrder = int.MinValue;
            lastInnerWidthPixels = -1f;
            lastOuterWidthPixels = -1f;
            lastInnerColor = default;
            lastOuterColor = default;
            lastAlphaThreshold = -1f;
            lastSortingOrderOffset = int.MinValue;
            nextSourceRefreshFrame = 0;
            nextVisibilitySyncFrame = 0;
        }

        private static Material GetSharedOutlineMaterial()
        {
            if (sharedOutlineMaterial != null)
            {
                return sharedOutlineMaterial;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Selection outline shader '{ShaderName}' was not found.");
                shader = Shader.Find("Sprites/Default");
            }

            sharedOutlineMaterial = new Material(shader)
            {
                name = "SelectionOutlineMaterial"
            };
            sharedOutlineMaterial.hideFlags = HideFlags.HideAndDontSave;
            return sharedOutlineMaterial;
        }

        private static Color GetOuterColor(Color color)
        {
            return new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, color.a);
        }
    }
}
