using UnityEngine;

namespace ProjectS.Buildings
{
    public enum BuildingRangeIndicatorSource
    {
        AutoTurretAttack,
        SpeedAura,
        StructureVision
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingRangeIndicator : MonoBehaviour
    {
        // Keep the area marker above terrain but below world objects and combat feedback.
        private const string SortingLayerName = "RangeIndicators";

        [SerializeField] private BuildingRangeIndicatorSource source;
        [SerializeField] private Sprite rangeSprite;
        [SerializeField, Range(0.1f, 1f)] private float verticalScale = 0.58f;

        private SpriteRenderer rangeRenderer;
        private BuildingStatus structure;
        private BuildingAutoTurret autoTurret;
        private BuildingSpeedAura speedAura;
        private float lastRadius = -1f;

        public void Configure(BuildingRangeIndicatorSource rangeSource, Sprite sprite = null)
        {
            source = rangeSource;
            if (sprite != null)
            {
                rangeSprite = sprite;
            }

            RefreshVisual(true);
        }

        private void Awake()
        {
            ResolveSources();
            EnsureRenderer();
            RefreshVisual(true);
        }

        private void OnEnable()
        {
            RefreshVisual(true);
        }

        private void LateUpdate()
        {
            RefreshVisual(false);
        }

        private void OnDisable()
        {
            if (rangeRenderer != null)
            {
                rangeRenderer.enabled = false;
            }
        }

        private void ResolveSources()
        {
            structure ??= GetComponent<BuildingStatus>();
            autoTurret ??= GetComponent<BuildingAutoTurret>();
            speedAura ??= GetComponent<BuildingSpeedAura>();
        }

        private void EnsureRenderer()
        {
            if (rangeRenderer != null)
            {
                return;
            }

            var rangeTransform = transform.Find("RangeIndicator");
            var rangeObject = rangeTransform != null ? rangeTransform.gameObject : new GameObject("RangeIndicator");
            rangeObject.transform.SetParent(transform, false);
            rangeObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            var legacyLine = rangeObject.GetComponent<LineRenderer>();
            if (legacyLine != null)
            {
                Destroy(legacyLine);
            }

            rangeSprite ??= BuildingStatusUiSprites.RangeIndicator;
            rangeRenderer = rangeObject.GetComponent<SpriteRenderer>();
            if (rangeRenderer == null)
            {
                rangeRenderer = rangeObject.AddComponent<SpriteRenderer>();
            }

            rangeRenderer.sprite = rangeSprite;
            rangeRenderer.sortingLayerName = SortingLayerName;
            rangeRenderer.sortingOrder = 0;
        }

        private void RefreshVisual(bool force)
        {
            ResolveSources();
            EnsureRenderer();

            var radius = GetRadius();
            if (rangeRenderer == null)
            {
                return;
            }

            rangeRenderer.sprite = rangeSprite;
            rangeRenderer.enabled = radius > 0f && rangeSprite != null && isActiveAndEnabled;
            if (!force && Mathf.Approximately(radius, lastRadius))
            {
                return;
            }

            lastRadius = radius;
            rangeRenderer.color = Color.white;
            rangeRenderer.transform.localScale = new Vector3(
                radius * 2f,
                radius * 2f * Mathf.Clamp(verticalScale, 0.1f, 1f),
                1f);
        }

        private float GetRadius()
        {
            return source switch
            {
                BuildingRangeIndicatorSource.AutoTurretAttack => autoTurret != null ? autoTurret.AttackRange : 0f,
                BuildingRangeIndicatorSource.SpeedAura => speedAura != null ? speedAura.Radius : 0f,
                BuildingRangeIndicatorSource.StructureVision => structure != null ? structure.VisionRadius : 0f,
                _ => 0f
            };
        }

    }
}
