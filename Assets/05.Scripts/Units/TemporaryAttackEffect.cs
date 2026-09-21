using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    public sealed class TemporaryAttackEffect : MonoBehaviour
    {
        [SerializeField] private Color effectColor = new Color(1f, 0.85f, 0.25f, 0.9f);
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField, Min(0.01f)] private float effectDuration = 0.08f;
        [SerializeField] private float muzzleOffset = 0.25f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 30;

        private LineRenderer attackLine;
        private GameObject effectObject;
        private Material effectMaterial;
        private float hideAtRealtime;
        private bool flashActive;
        private bool visibilityAllowed = true;

        private void Awake()
        {
            attackLine = CreateAttackLine();
            attackLine.enabled = false;
        }

        private void Update()
        {
            if (flashActive && Time.unscaledTime >= hideAtRealtime)
            {
                StopFlash();
            }
        }

        private void OnDisable()
        {
            StopFlash();
        }

        private LineRenderer CreateAttackLine()
        {
            effectObject = new GameObject("TemporaryAttackEffect");
            effectObject.transform.SetParent(transform, false);

            var line = effectObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.35f;
            line.startColor = effectColor;
            line.endColor = new Color(effectColor.r, effectColor.g, effectColor.b, 0f);
            effectMaterial = new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial = effectMaterial;
            line.sortingLayerName = sortingLayerName;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private void OnDestroy()
        {
            StopFlash();
            if (effectObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(effectObject);
                }
                else
                {
                    DestroyImmediate(effectObject);
                }
            }

            if (effectMaterial != null)
            {
                Destroy(effectMaterial);
            }
        }

        public bool OwnsRenderer(Renderer renderer)
        {
            return renderer != null && renderer == attackLine;
        }

        public void SetVisibilityAllowed(bool allowed)
        {
            visibilityAllowed = allowed;
            RefreshLineVisibility();
        }

        public void PlayAttackFlash(Vector3 targetPosition)
        {
            if (!isActiveAndEnabled || attackLine == null)
            {
                return;
            }

            var start = transform.position;
            var direction = targetPosition - start;
            direction.z = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * muzzleOffset;
            }

            attackLine.SetPosition(0, start);
            attackLine.SetPosition(1, targetPosition);
            flashActive = true;
            hideAtRealtime = Time.unscaledTime + Mathf.Max(0.01f, effectDuration);
            RefreshLineVisibility();
        }

        private void StopFlash()
        {
            flashActive = false;
            hideAtRealtime = 0f;
            RefreshLineVisibility();
        }

        private void RefreshLineVisibility()
        {
            if (attackLine != null)
            {
                attackLine.enabled = flashActive && visibilityAllowed;
            }
        }
    }
}
