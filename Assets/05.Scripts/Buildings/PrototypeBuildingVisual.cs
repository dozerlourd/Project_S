using UnityEngine;

namespace ProjectS.Buildings
{
    public sealed class PrototypeBuildingVisual : MonoBehaviour
    {
        [SerializeField] private Color bodyColor = new Color(0.42f, 0.62f, 0.88f, 1f);
        [SerializeField] private Color trimColor = new Color(0.08f, 0.14f, 0.24f, 1f);
        [SerializeField] private Vector2 worldSize = new Vector2(2f, 2f);
        [SerializeField] private string spriteResourcePath;

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

        public void Configure(Color body, Color trim, Vector2 size, string resourcePath = null)
        {
            bodyColor = body;
            trimColor = trim;
            worldSize = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
            spriteResourcePath = resourcePath ?? string.Empty;
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

            spriteRenderer.sprite = CreateSpriteFromResource() ?? CreateSprite();
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = worldSize;
            spriteRenderer.sortingOrder = 20;

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

        private Sprite CreateSprite()
        {
            const int width = 48;
            const int height = 48;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PrototypeBuildingSprite",
                filterMode = FilterMode.Point
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var border = x < 3 || x >= width - 3 || y < 3 || y >= height - 3;
                    var roof = y >= height - 10;
                    var door = x >= 20 && x <= 28 && y < 16;
                    var window = (x >= 9 && x <= 15 && y >= 22 && y <= 29)
                        || (x >= 33 && x <= 39 && y >= 22 && y <= 29);
                    var color = border || roof || door || window ? trimColor : bodyColor;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 24f);
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
