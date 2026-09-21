using UnityEngine;

namespace ProjectS.Visibility
{
    public sealed class FogVisibilityTarget : MonoBehaviour
    {
        private Renderer[] renderers;
        private ProjectS.Units.UnitHealthBar[] healthBars;
        private ProjectS.Units.UnitTeamIndicator[] teamIndicators;
        private ProjectS.Units.UnitTeamIndicatorMarker[] teamIndicatorMarkers;

        private void LateUpdate()
        {
            var fog = FogOfWarManager.ActiveInstance;
            if (fog == null || !TryGetTeam(out var team))
            {
                GetComponent<ProjectS.Units.TemporaryAttackEffect>()?.SetVisibilityAllowed(true);
                return;
            }

            var visible = team == fog.PlayerTeam || fog.IsWorldPositionVisible(transform.position);
            var attackEffect = GetComponent<ProjectS.Units.TemporaryAttackEffect>();
            ResolveRenderers();
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null
                    && !IsSelectionOutlineRenderer(renderers[i])
                    && (attackEffect == null || !attackEffect.OwnsRenderer(renderers[i])))
                {
                    renderers[i].enabled = visible;
                }
            }

            attackEffect?.SetVisibilityAllowed(visible);
            SetUiVisibility(visible);
        }

        private bool TryGetTeam(out ProjectS.Units.UnitTeam team)
        {
            var unit = GetComponent<ProjectS.Units.PrototypeUnitStatus>();
            if (unit != null)
            {
                team = unit.Team;
                return true;
            }

            team = default;
            return false;
        }

        private void ResolveRenderers()
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
                healthBars = GetComponentsInChildren<ProjectS.Units.UnitHealthBar>(true);
                teamIndicators = GetComponentsInChildren<ProjectS.Units.UnitTeamIndicator>(true);
                teamIndicatorMarkers = GetComponentsInChildren<ProjectS.Units.UnitTeamIndicatorMarker>(true);
            }
        }

        private bool IsSelectionOutlineRenderer(Renderer renderer)
        {
            var outline = renderer != null ? renderer.GetComponentInParent<ProjectS.Units.SelectionOutlineEffect>() : null;
            return outline != null && outline.OwnsRenderer(renderer);
        }

        private void SetUiVisibility(bool visible)
        {
            SetEnabled(healthBars, visible);
            SetEnabled(teamIndicators, visible);
            SetEnabled(teamIndicatorMarkers, visible);
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
