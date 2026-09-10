using Elemental.Input.Gestures;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    public sealed partial class EarthDuelHud
    {
        private static readonly ElementId[] SchoolOrder = { ElementId.Fire, ElementId.Earth, ElementId.Water, ElementId.Air };
        private readonly VisualElement[] _schoolTokens = new VisualElement[4];
        private readonly Label[] _schoolLabels = new Label[4];
        private bool _schoolHudBound;
        private MagicInputController _schoolInput;
        private int _schoolCapabilities = -1;
        private bool _schoolCosmeticsForQa=true;
        public void SetSchoolCosmeticsForQa(bool enabled)
        {
            _schoolCosmeticsForQa=enabled;
            if(!enabled)for(int i=0;i<4;i++)if(_schoolTokens[i]!=null)_schoolTokens[i].style.scale=new Scale(Vector3.one);
        }
        private float _schoolPulseAge = 1;
        private void OnSchoolSelected(ElementId _) => _schoolPulseAge = 0;

        private void ClearSchoolHud()
        {
            if (_schoolInput != null) { _schoolInput.ConfigureWorldPointerEligibility(null); _schoolInput.SelectedElementChanged -= OnSchoolSelected; }
            _schoolInput = null; _schoolHudBound = false; _schoolCapabilities = -1;
            for (int i = 0; i < 4; i++) { _schoolTokens[i] = null; _schoolLabels[i] = null; }
        }

        private bool WorldPointerEligible(Vector2 screenPoint)
        {
            if(!isActiveAndEnabled||!_combatVisible||_root?.panel==null)return true;
            Vector2 panelPoint=RuntimePanelUtils.ScreenToPanel(_root.panel,new Vector2(screenPoint.x,Screen.height-screenPoint.y));
            for(VisualElement picked=_root.panel.Pick(panelPoint);picked!=null;picked=picked.parent)
                if(picked is Button)return false;
            return true;
        }
        private void TickSchoolHud()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.SchoolHud);
            if (_root == null || magic == null) return;
            if (_schoolInput != magic)
            {
                if (_schoolInput != null) { _schoolInput.ConfigureWorldPointerEligibility(null); _schoolInput.SelectedElementChanged -= OnSchoolSelected; }
                _schoolInput = magic; _schoolInput.ConfigureWorldPointerEligibility(WorldPointerEligible); _schoolInput.SelectedElementChanged += OnSchoolSelected;
            }
            if (!_schoolHudBound)
            {
                _schoolHudBound = true;
                for (int i = 0; i < 4; i++)
                {
                    string name = SchoolOrder[i].ToString();
                    _schoolTokens[i] = _root.Q("reference-token-" + name) ?? _root.Q("element-" + name);
                    _schoolLabels[i] = _root.Q<Label>("reference-label-" + name) ?? _schoolTokens[i]?.Q<Label>();
                    if (_schoolLabels[i] != null) _schoolLabels[i].text = (i + 1) + " " + name.ToUpperInvariant();
                }
            }
            int capabilities = 0;
            for (int i = 0; i < 4; i++) if (magic.IsElementAvailable(SchoolOrder[i])) capabilities |= 1 << i;
            if (capabilities != _schoolCapabilities)
            {
                _schoolCapabilities = capabilities;
                for (int i = 0; i < 4; i++)
                {
                    bool available = (capabilities & (1 << i)) != 0;
                    if (_schoolTokens[i] != null) _schoolTokens[i].style.opacity = available ? 1 : .28f;
                    if (_schoolLabels[i] != null) _schoolLabels[i].style.color = available ? new Color(.92f,.85f,.67f) : Color.gray;
                }
            }
            if (_schoolCosmeticsForQa)
            {
            float pulse = Elemental.Simulation.Bending.EarthResponsePreset.SchoolScale(_schoolPulseAge, _reducedMotion);
            _schoolPulseAge += Time.unscaledDeltaTime;
            for (int i = 0; i < 4; i++)
                if (_schoolTokens[i] != null) _schoolTokens[i].style.scale = new Scale(Vector3.one * (SchoolOrder[i] == SelectedElement ? pulse : 1));
            }
        }
    }
}
