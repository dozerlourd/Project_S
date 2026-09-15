using UnityEngine;

namespace ProjectS.Buildings
{
    public sealed class PrototypeBuildingVisual : MonoBehaviour
    {
        [SerializeField] private Color bodyColor = new Color(0.42f, 0.62f, 0.88f, 1f);
        [SerializeField] private Color trimColor = new Color(0.08f, 0.14f, 0.24f, 1f);
        [SerializeField] private Vector2 worldSize = new Vector2(2f, 2f);
        [SerializeField] private string spriteResourcePath;
        [SerializeField] private string sortingLayerName = "Structures";
        [SerializeField] private int sortingOrder = 20;

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                ApplyVisual();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ApplyVisual();
            }
        }

        private void OnValidate()
        {
            worldSize = new Vector2(Mathf.Max(0.25f, worldSize.x), Mathf.Max(0.25f, worldSize.y));
        }

        public void Configure(
            Color body,
            Color trim,
            Vector2 size,
            string resourcePath = null,
            int renderOrder = 20)
        {
            bodyColor = body;
            trimColor = trim;
            worldSize = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
            spriteResourcePath = resourcePath ?? string.Empty;
            sortingOrder = renderOrder;
            if (Application.isPlaying)
            {
                ApplyVisual();
            }
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sprite = CreateSpriteFromResource() ?? spriteRenderer.sprite;
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = worldSize;
            spriteRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Structures"
                : sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
                if (boxCollider == null)
                {
                    boxCollider = gameObject.AddComponent<BoxCollider2D>();
                }
            }

            boxCollider.size = worldSize;
            boxCollider.isTrigger = true;
        }

        private Sprite CreateSpriteFromResource()
        {
            if (string.IsNullOrWhiteSpace(spriteResourcePath))
            {
                return null;
            }

            var texture = UnityEngine.Resources.Load<Texture2D>(spriteResourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(texture.width, texture.height) / Mathf.Max(worldSize.x, worldSize.y));
        }
    }
}
