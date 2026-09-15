using UnityEngine;

namespace ProjectS.Buildings
{
    public enum BuildingRangeIndicatorSource
    {
        AutoTurretAttack,
        SpeedAura
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingRangeIndicator : MonoBehaviour
    {
        private const int CircleSegmentCount = 64;
        private const string SortingLayerName = "RangeIndicators";

        [SerializeField] private BuildingRangeIndicatorSource source;
        [SerializeField] private Color lineColor = new Color(0.3f, 0.85f, 1f, 0.55f);
        [SerializeField, Min(0.01f)] private float lineWidth = 0.045f;

        private LineRenderer lineRenderer;
        private BuildingAutoTurret autoTurret;
        private BuildingSpeedAura speedAura;
        private float lastRadius = -1f;

        public void Configure(BuildingRangeIndicatorSource rangeSource)
        {
            source = rangeSource;
            RefreshVisual(true);
        }

        private void Awake()
        {
            ResolveSources();
            EnsureLineRenderer();
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
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void ResolveSources()
        {
            autoTurret ??= GetComponent<BuildingAutoTurret>();
            speedAura ??= GetComponent<BuildingSpeedAura>();
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer != null)
            {
                return;
            }

            var rangeObject = new GameObject("RangeIndicator");
            rangeObject.transform.SetParent(transform, false);
            rangeObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            lineRenderer = rangeObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = CircleSegmentCount;
            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 2;
            lineRenderer.sortingLayerName = SortingLayerName;
            lineRenderer.sortingOrder = 0;
        }

        private void RefreshVisual(bool force)
        {
            ResolveSources();
            EnsureLineRenderer();

            var radius = GetRadius();
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.enabled = radius > 0f && isActiveAndEnabled;
            if (!force && Mathf.Approximately(radius, lastRadius))
            {
                return;
            }

            lastRadius = radius;
            for (var i = 0; i < CircleSegmentCount; i++)
            {
                var angle = i * Mathf.PI * 2f / CircleSegmentCount;
                lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private float GetRadius()
        {
            return source switch
            {
                BuildingRangeIndicatorSource.AutoTurretAttack => autoTurret != null ? autoTurret.AttackRange : 0f,
                BuildingRangeIndicatorSource.SpeedAura => speedAura != null ? speedAura.Radius : 0f,
                _ => 0f
            };
        }
    }
}
