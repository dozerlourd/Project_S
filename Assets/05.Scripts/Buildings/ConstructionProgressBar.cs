using ProjectS.Visibility;
using UnityEngine;

namespace ProjectS.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConstructionSite))]
    public sealed class ConstructionProgressBar : MonoBehaviour
    {
        // Kept above the construction visual and separate from completed-building health bars.
        private const string SortingLayerName = "BuildingStatusUI";

        [SerializeField] private Sprite barSprite;
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.9f);
        [SerializeField] private Color fillColor = new Color(1f, 0.72f, 0.18f, 0.98f);
        [SerializeField, Min(0.1f)] private float widthRatio = 0.72f;
        [SerializeField, Min(0.01f)] private float height = 0.12f;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.2f, 0f);
        [SerializeField, Min(0)] private int sortingOrder = 2;

        private ConstructionSite constructionSite;
        private BoxCollider2D siteCollider;
        private Transform barRoot;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;

        private void Awake()
        {
            constructionSite = GetComponent<ConstructionSite>();
            siteCollider = GetComponent<BoxCollider2D>();
            ResolveRenderers();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            if (!ShouldBeVisible())
            {
                Hide();
                return;
            }

            ResolveRenderers();
            if (backgroundRenderer == null || fillRenderer == null)
            {
                return;
            }

            var bounds = GetSiteBounds();
            var width = Mathf.Max(0.9f, bounds.size.x * widthRatio);
            var progress01 = Mathf.Clamp01(constructionSite.BuildProgress01);
            barRoot.localPosition = new Vector3(
                bounds.center.x + localOffset.x,
                bounds.max.y + localOffset.y,
                localOffset.z);
            backgroundRenderer.transform.localPosition = Vector3.zero;
            backgroundRenderer.transform.localScale = GetSpriteScale(backgroundRenderer, width, height);

            var fillWidth = width * progress01;
            fillRenderer.transform.localPosition = new Vector3((fillWidth - width) * 0.5f, 0f, 0f);
            fillRenderer.transform.localScale = GetSpriteScale(fillRenderer, fillWidth, height * 0.7f);
            backgroundRenderer.enabled = true;
            fillRenderer.enabled = fillWidth > 0f;
        }

        public void Hide()
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.enabled = false;
            }

            if (fillRenderer != null)
            {
                fillRenderer.enabled = false;
            }
        }

        private bool ShouldBeVisible()
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            constructionSite ??= GetComponent<ConstructionSite>();
            if (constructionSite == null
                || !constructionSite.isActiveAndEnabled
                || !constructionSite.HasConstructionStarted
                || constructionSite.Completed)
            {
                return false;
            }

            var fog = FogOfWarManager.ActiveInstance;
            return fog == null
                || constructionSite.Team == fog.PlayerTeam
                || fog.IsWorldPositionVisible(transform.position);
        }

        private void ResolveRenderers()
        {
            if (barRoot == null)
            {
                barRoot = transform.Find("ConstructionProgressBar");
                if (barRoot == null)
                {
                    barRoot = new GameObject("ConstructionProgressBar").transform;
                    barRoot.SetParent(transform, false);
                }
            }

            backgroundRenderer ??= ResolveChildRenderer("Background", sortingOrder, backgroundColor);
            fillRenderer ??= ResolveChildRenderer("Fill", sortingOrder + 1, fillColor);
        }

        private SpriteRenderer ResolveChildRenderer(string childName, int order, Color color)
        {
            var child = barRoot.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(barRoot, false);
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = barSprite != null ? barSprite : BuildingStatusUiSprites.HealthBar;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        private Bounds GetSiteBounds()
        {
            var siteRenderer = GetComponent<SpriteRenderer>();
            if (siteRenderer != null && siteRenderer.sprite != null)
            {
                return siteRenderer.localBounds;
            }

            siteCollider ??= GetComponent<BoxCollider2D>();
            return siteCollider != null
                ? new Bounds(siteCollider.offset, siteCollider.size)
                : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Vector3 GetSpriteScale(SpriteRenderer renderer, float width, float height)
        {
            var spriteSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.one;
            return new Vector3(
                width / Mathf.Max(0.0001f, spriteSize.x),
                height / Mathf.Max(0.0001f, spriteSize.y),
                1f);
        }
    }
}
