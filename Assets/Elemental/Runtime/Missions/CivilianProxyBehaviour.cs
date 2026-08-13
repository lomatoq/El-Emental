using Elemental.Simulation.Missions;
using UnityEngine;

namespace Elemental.Runtime.Missions
{
    [DisallowMultipleComponent]
    public sealed class CivilianProxyBehaviour : MonoBehaviour
    {
        [SerializeField] private Transform routeAStart;
        [SerializeField] private Transform routeAEnd;
        [SerializeField] private Transform routeBStart;
        [SerializeField] private Transform routeBEnd;
        [SerializeField] private MeshRenderer targetRenderer;
        [SerializeField] private Color safeColor = new Color(0.35f, 1f, 0.55f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.22f, 0.12f);
        private MaterialPropertyBlock _properties;

        public CivilianProxy State { get; private set; }

        public void Configure(Transform aStart, Transform aEnd, Transform bStart, Transform bEnd, MeshRenderer renderer)
        {
            routeAStart = aStart; routeAEnd = aEnd; routeBStart = bStart; routeBEnd = bEnd; targetRenderer = renderer;
        }

        public void Apply(in CivilianProxy state)
        {
            State = state;
            Transform start = state.RouteIndex == 0 ? routeAStart : routeBStart;
            Transform end = state.RouteIndex == 0 ? routeAEnd : routeBEnd;
            if (start != null && end != null) transform.position = Vector3.Lerp(start.position, end.position, state.RouteProgress);
            gameObject.SetActive(state.State != CivilianRescueState.Rescued && state.State != CivilianRescueState.Lost);
            if (targetRenderer != null)
            {
                if (_properties == null) _properties = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", Color.Lerp(safeColor, dangerColor, Mathf.Clamp01(state.Danger)));
                _properties.SetColor("_Color", Color.Lerp(safeColor, dangerColor, Mathf.Clamp01(state.Danger)));
                targetRenderer.SetPropertyBlock(_properties);
            }
        }
    }
}
