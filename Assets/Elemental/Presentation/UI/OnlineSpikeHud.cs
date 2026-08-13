using Elemental.Runtime.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class OnlineSpikeHud : MonoBehaviour
    {
        [SerializeField] private OnlineSpikeDriver driver;
        private Label _summary;
        private Label _authority;
        private Label _correction;
        public void Configure(OnlineSpikeDriver configuredDriver) => driver = configuredDriver;
        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _summary = root.Q<Label>("net-summary"); _authority = root.Q<Label>("authority-summary");
            _correction = root.Q<Label>("correction-summary");
        }
        private void Update()
        {
            if (driver?.Harness == null) return;
            if (_summary != null) _summary.text = $"{driver.Harness.ClientCount} clients · loss {driver.Harness.DroppedCount} · queue {driver.Harness.QueueDebt}";
            if (_authority != null) _authority.text = $"commands {driver.Harness.Authority.AcceptedCount} · decisions {driver.Harness.Authority.DecisionCount}";
            if (_correction != null) _correction.text = $"soft corrections {driver.CorrectionCount} · max error {driver.MaximumCorrectionError:0.000} m";
        }
    }
}
