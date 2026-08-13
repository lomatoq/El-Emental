using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class BendingDebugOverlay : MonoBehaviour
    {
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private bool expanded;

        private readonly Rect _toggleRect = new Rect(12f, 12f, 126f, 28f);
        private readonly Rect _panelRect = new Rect(12f, 46f, 390f, 430f);

        public void Configure(MagicInputController configuredInput, MagicExecutor configuredExecutor)
        {
            input = configuredInput;
            executor = configuredExecutor;
        }

        private void Update()
        {
            if (!expanded || input == null) return;
            Rigidbody body = executor != null ? executor.HeldBody : null;
            Debug.DrawLine(input.BendTargetPosition, input.BendTargetPosition + input.BendTargetVelocity * 0.12f,
                Color.cyan, 0f, false);
            if (body == null) return;
            Debug.DrawLine(body.worldCenterOfMass, input.BendTargetPosition, Color.yellow, 0f, false);
            Debug.DrawRay(body.worldCenterOfMass,
                executor.HeldControlForce * 0.0002f, Color.magenta, 0f, false);
        }

        private void OnGUI()
        {
            if (GUI.Button(_toggleRect, expanded ? "BEND DEBUG ▲" : "BEND DEBUG ▼"))
                expanded = !expanded;
            if (!expanded || input == null) return;

            Rigidbody body = executor != null ? executor.HeldBody : null;
            Vector3 actualPosition = body != null ? body.worldCenterOfMass : Vector3.zero;
            Vector3 actualVelocity = body != null ? body.linearVelocity : Vector3.zero;
            Vector3 error = executor != null ? executor.HeldControlError : Vector3.zero;
            Vector3 force = executor != null ? executor.HeldControlForce : Vector3.zero;
            string source = input.CurrentBendPhase == Elemental.Simulation.Bending.BendPhase.Idle
                ? "none"
                : "planet terrain / quality 1.00";
            string text =
                $"Element              EARTH\n" +
                $"BendPhase            {input.CurrentBendPhase}\n" +
                $"OriginMode           {input.BendOriginMode}\n" +
                $"Source               {source}\n" +
                $"Held Amount          {input.BendAmount01:0.00}\n" +
                $"Effective Mass       {(executor != null ? executor.HeldMass : 0f):0.0} kg\n" +
                $"Charge01             {input.BendCharge01:0.00}\n" +
                $"Focus01              {input.BendFocus01:0.00}\n" +
                $"StanceFactor         1.00\n" +
                $"GestureIntent        {input.CurrentGestureIntent}\n" +
                $"Gesture/Target Speed {input.BendTargetVelocity.magnitude:0.00} m/s\n" +
                $"BendTarget Pos       {Format(input.BendTargetPosition)}\n" +
                $"BendTarget Vel       {Format(input.BendTargetVelocity)}\n" +
                $"Actual Pos           {Format(actualPosition)}\n" +
                $"Actual Vel           {Format(actualVelocity)}\n" +
                $"Control Error        {error.magnitude:0.000} m\n" +
                $"Applied Force        {force.magnitude:0.0} N" +
                (executor != null && executor.HeldControlForceWasClamped ? " (CLAMPED)" : "") + "\n" +
                $"Predicted Release    {actualVelocity.magnitude + (input.BendCharge01 * 24f):0.00} m/s\n" +
                $"Mobility Force       0.00 N\n" +
                $"Overheat / Water     0.00 / 0.00";
            GUI.Box(_panelRect, text);
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
    }
}
