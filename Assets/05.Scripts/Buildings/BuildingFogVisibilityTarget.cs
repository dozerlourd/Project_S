using ProjectS.Visibility;
using UnityEngine;

namespace ProjectS.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingStatus))]
    public sealed class BuildingFogVisibilityTarget : MonoBehaviour
    {
        private Renderer[] renderers;
        private BuildingHealthBar[] healthBars;
        private BuildingRangeIndicator[] rangeIndicators;
        private BuildingStatus building;

        private void LateUpdate()
        {
            var fog = FogOfWarManager.ActiveInstance;
            if (fog == null || !TryGetBuilding(out var targetBuilding))
            {
                return;
            }

            var visible = targetBuilding.Team == fog.PlayerTeam || fog.IsWorldPositionVisible(transform.position);
            ResolveRenderers();
            SetRenderersVisible(visible);
            SetEnabled(healthBars, visible);
            SetEnabled(rangeIndicators, visible);
        }

        private bool TryGetBuilding(out BuildingStatus targetBuilding)
        {
            building ??= GetComponent<BuildingStatus>();
            targetBuilding = building;
            return targetBuilding != null;
        }

        private void ResolveRenderers()
        {
            if (renderers != null)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            healthBars = GetComponentsInChildren<BuildingHealthBar>(true);
            rangeIndicators = GetComponentsInChildren<BuildingRangeIndicator>(true);
        }

        private void SetRenderersVisible(bool visible)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        private static void SetEnabled<T>(T[] components, bool enabled) where T : Behaviour
        {
            if (components == null)
            {
                return;
            }

            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    components[i].enabled = enabled;
                }
            }
        }
    }
}
