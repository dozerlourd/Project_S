using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    public sealed class UnitTeamIndicator : MonoBehaviour
    {
        [SerializeField] private UnitTeam playerTeam = UnitTeam.Team1;
        [SerializeField] private Color allyColor = new Color(0.1f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.18f, 0.12f, 0.95f);
        [SerializeField] private Color neutralColor = new Color(1f, 0.9f, 0.2f, 0.95f);
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private Vector2 size = new Vector2(0.62f, 0.08f);
        [SerializeField] private int sortingOrder = 40;

        private static Sprite indicatorSprite;
        private PrototypeUnitStatus status;
        private SpriteRenderer indicatorRenderer;

        private void Awake()
        {
            status = GetComponent<PrototypeUnitStatus>();
            indicatorRenderer = GetComponentInChildren<UnitTeamIndicatorMarker>(true)?.GetComponent<SpriteRenderer>();
            if (indicatorRenderer == null)
            {
                indicatorRenderer = CreateIndicatorRenderer();
            }

            ApplyIndicatorVisual();
        }

        private void LateUpdate()
        {
            ApplyIndicatorVisual();
        }

        private SpriteRenderer CreateIndicatorRenderer()
        {
            var marker = new GameObject("TeamIndicator");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = localOffset;
            marker.transform.localScale = new Vector3(size.x, size.y, 1f);
            marker.AddComponent<UnitTeamIndicatorMarker>();

            var spriteRenderer = marker.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetIndicatorSprite();
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        private void ApplyIndicatorVisual()
        {
            if (indicatorRenderer == null || status == null)
            {
                return;
            }

            indicatorRenderer.transform.localPosition = localOffset;
            indicatorRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            indicatorRenderer.sortingOrder = sortingOrder;
            indicatorRenderer.color = GetTeamColor();
        }

        private Color GetTeamColor()
        {
            if (status.Team == playerTeam)
            {
                return allyColor;
            }

            return status.Team == UnitTeam.Team2 ? enemyColor : neutralColor;
        }

        private static Sprite GetIndicatorSprite()
        {
            if (indicatorSprite != null)
            {
                return indicatorSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "UnitTeamIndicatorSprite",
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            indicatorSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            indicatorSprite.name = "UnitTeamIndicatorSprite";
            return indicatorSprite;
        }
    }
}
