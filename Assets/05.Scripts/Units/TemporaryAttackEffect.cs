using UnityEngine;

namespace ProjectS.Units
{
    [RequireComponent(typeof(PrototypeUnitStatus))]
    [RequireComponent(typeof(UnitCommandAgent))]
    public sealed class TemporaryAttackEffect : MonoBehaviour
    {
        [SerializeField] private Color effectColor = new Color(1f, 0.85f, 0.25f, 0.9f);
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float effectDuration = 0.08f;
        [SerializeField] private float muzzleOffset = 0.25f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 30;

        private LineRenderer attackLine;
        private Material effectMaterial;
        private float hideAtTime;

        private void Awake()
        {
            attackLine = CreateAttackLine();
            attackLine.enabled = false;
        }

        private void Update()
        {
            if (attackLine.enabled && Time.time >= hideAtTime)
            {
                attackLine.enabled = false;
            }
        }

        private LineRenderer CreateAttackLine()
        {
            var effectObject = new GameObject("TemporaryAttackEffect");
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
            if (effectMaterial != null)
            {
                Destroy(effectMaterial);
            }
        }

        public void PlayAttackFlash(Vector3 targetPosition)
        {
            if (attackLine == null)
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
            attackLine.enabled = true;
            hideAtTime = Time.time + effectDuration;
        }
    }
}
