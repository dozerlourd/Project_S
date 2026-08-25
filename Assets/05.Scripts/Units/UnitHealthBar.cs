using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(UnitHealth))]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        [SerializeField] private Color fillColor = new Color(0.2f, 1f, 0.25f, 0.95f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.12f, 0.95f);
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.76f, 0f);
        [SerializeField] private Vector2 size = new Vector2(0.62f, 0.075f);
        [SerializeField] private int sortingOrder = 41;
        [SerializeField] private float lowHealthThreshold = 0.35f;

        private static Sprite barSprite;
        private UnitHealth health;
        private Transform barRoot;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;

        private void Awake()
        {
            health = GetComponent<UnitHealth>();
            ResolveRenderers();
            ApplyVisual();
        }

        private void LateUpdate()
        {
            ApplyVisual();
        }

        private void ResolveRenderers()
        {
            barRoot = transform.Find("HealthBar");
            if (barRoot == null)
            {
                barRoot = new GameObject("HealthBar").transform;
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

            var spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = GetBarSprite();
            spriteRenderer.sortingOrder = order;
            spriteRenderer.color = color;
            return spriteRenderer;
        }

        private void ApplyVisual()
        {
            if (health == null || backgroundRenderer == null || fillRenderer == null)
            {
                return;
            }

            var maxHealth = Mathf.Max(0.001f, health.MaxHealth);
            var healthRatio = Mathf.Clamp01(health.CurrentHealth / maxHealth);

            barRoot.localPosition = localOffset;
            backgroundRenderer.transform.localPosition = Vector3.zero;
            backgroundRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);

            var fillWidth = size.x * healthRatio;
            fillRenderer.transform.localPosition = new Vector3((fillWidth - size.x) * 0.5f, 0f, 0f);
            fillRenderer.transform.localScale = new Vector3(fillWidth, size.y * 0.7f, 1f);
            fillRenderer.color = healthRatio <= lowHealthThreshold ? lowHealthColor : fillColor;
            fillRenderer.enabled = healthRatio > 0f;
        }

        private static Sprite GetBarSprite()
        {
            if (barSprite != null)
            {
                return barSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "UnitHealthBarSprite",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            barSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            barSprite.name = "UnitHealthBarSprite";
            return barSprite;
        }
    }
}
