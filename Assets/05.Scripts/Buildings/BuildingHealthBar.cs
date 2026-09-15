using UnityEngine;

namespace ProjectS.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingHealth))]
    public sealed class BuildingHealthBar : MonoBehaviour
    {
        // Building status belongs above the building sprite but must stay below units and effects.
        private const string SortingLayerName = "BuildingStatusUI";

        [SerializeField] private Sprite barSprite;
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.88f);
        [SerializeField] private Color fillColor = new Color(0.2f, 1f, 0.25f, 0.95f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.12f, 0.95f);
        [SerializeField, Min(0.1f)] private float widthRatio = 0.62f;
        [SerializeField, Min(0.01f)] private float height = 0.12f;
        [SerializeField, Min(0f)] private float bottomMargin = 0.18f;
        [SerializeField, Min(0)] private int sortingOrder = 0;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.35f;

        private BuildingHealth health;
        private BoxCollider2D buildingCollider;
        private Transform barRoot;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;

        private void Awake()
        {
            health = GetComponent<BuildingHealth>();
            buildingCollider = GetComponent<BoxCollider2D>();
            ResolveRenderers();
            ApplyVisual();
        }

        public void ConfigureSprite(Sprite sprite)
        {
            if (sprite != null)
            {
                barSprite = sprite;
            }

            widthRatio = 0.62f;
            height = 0.12f;
            bottomMargin = 0.18f;
            sortingOrder = 0;
            ResolveRenderers();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<BuildingHealth>();
            }

            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
            }

            ApplyVisual();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
            }
        }

        private void LateUpdate()
        {
            // Building placement can resize the collider after this component is created.
            if (backgroundRenderer == null || fillRenderer == null)
            {
                ResolveRenderers();
                ApplyVisual();
                return;
            }

            ApplyLayout();
        }

        private void OnHealthChanged(BuildingHealth _, float __)
        {
            ApplyVisual();
        }

        private void ResolveRenderers()
        {
            barRoot = transform.Find("BuildingHealthBar");
            if (barRoot == null)
            {
                barRoot = new GameObject("BuildingHealthBar").transform;
                barRoot.SetParent(transform, false);
            }

            backgroundRenderer = ResolveChildRenderer("Background", sortingOrder, backgroundColor);
            fillRenderer = ResolveChildRenderer("Fill", sortingOrder + 1, fillColor);
        }

        private SpriteRenderer ResolveChildRenderer(string childName, int order, Color color)
        {
            var child = barRoot.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(barRoot, false);
            }

            barSprite ??= BuildingStatusUiSprites.HealthBar;
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = barSprite;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        private void ApplyVisual()
        {
            ApplyLayout();
            if (health == null || fillRenderer == null)
            {
                return;
            }

            var healthRatio = Mathf.Clamp01(health.CurrentHealth / Mathf.Max(0.001f, health.MaxHealth));
            var width = GetBarWidth();
            var fillWidth = width * healthRatio;
            fillRenderer.transform.localPosition = new Vector3((fillWidth - width) * 0.5f, 0f, 0f);
            fillRenderer.transform.localScale = GetSpriteScale(fillRenderer, fillWidth, height * 0.7f);
            fillRenderer.color = healthRatio <= lowHealthThreshold ? lowHealthColor : fillColor;
            fillRenderer.enabled = healthRatio > 0f;
        }

        private void ApplyLayout()
        {
            if (barRoot == null || backgroundRenderer == null)
            {
                return;
            }

            var bounds = GetBuildingBounds();
            var width = GetBarWidth();
            barRoot.localPosition = new Vector3(bounds.center.x, bounds.min.y - bottomMargin, 0f);
            backgroundRenderer.transform.localPosition = Vector3.zero;
            backgroundRenderer.transform.localScale = GetSpriteScale(backgroundRenderer, width, height);
        }

        private Bounds GetBuildingBounds()
        {
            var buildingRenderer = GetComponent<SpriteRenderer>();
            if (buildingRenderer != null && buildingRenderer.sprite != null)
            {
                return buildingRenderer.localBounds;
            }

            if (buildingCollider == null)
            {
                buildingCollider = GetComponent<BoxCollider2D>();
            }

            return buildingCollider != null
                ? new Bounds(buildingCollider.offset, buildingCollider.size)
                : new Bounds(Vector3.zero, Vector3.one);
        }

        private float GetBarWidth()
        {
            return Mathf.Max(0.9f, GetBuildingBounds().size.x * widthRatio);
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

    // Serialized PNG sprites are the normal path. These fallbacks keep dynamic buildings visible
    // until Unity has imported and bound the generated assets to their prefabs.
    internal static class BuildingStatusUiSprites
    {
        private static Sprite rangeIndicator;
        private static Sprite healthBar;

        public static Sprite RangeIndicator => rangeIndicator ??= CreateRangeIndicator();
        public static Sprite HealthBar => healthBar ??= CreateHealthBar();

        private static Sprite CreateRangeIndicator()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BuildingRangeIndicatorFallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    texture.SetPixel(x, y, distance > 1f
                        ? Color.clear
                        : distance >= 0.9f
                            ? new Color(0.05f, 0.23f, 0.74f, 0.92f)
                            : new Color(0.18f, 0.59f, 1f, 0.3f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateHealthBar()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "BuildingHealthBarFallback",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
