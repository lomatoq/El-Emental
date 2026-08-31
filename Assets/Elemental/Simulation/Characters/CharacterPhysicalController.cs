using System;
using Elemental.Core.IDs;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct CharacterPhysicalTuning
    {
        public CharacterPhysicalTuning(
            float assistBalanceError,
            float ragdollBalanceError,
            float staggerDebtThreshold,
            float ragdollDebtThreshold,
            float debtDecayPerSecond,
            float recoveryStableSeconds,
            float recoveryDurationSeconds,
            float recoveryMaximumSpeed,
            float maximumBalanceTorque)
        {
            AssistBalanceError = assistBalanceError;
            RagdollBalanceError = ragdollBalanceError;
            StaggerDebtThreshold = staggerDebtThreshold;
            RagdollDebtThreshold = ragdollDebtThreshold;
            DebtDecayPerSecond = debtDecayPerSecond;
            RecoveryStableSeconds = recoveryStableSeconds;
            RecoveryDurationSeconds = recoveryDurationSeconds;
            RecoveryMaximumSpeed = recoveryMaximumSpeed;
            MaximumBalanceTorque = maximumBalanceTorque;
        }

        public static CharacterPhysicalTuning Default => new CharacterPhysicalTuning(
            0.22f,
            0.9f,
            2f,
            5f,
            2.5f,
            0.25f,
            0.6f,
            1.2f,
            220f);

        public float AssistBalanceError { get; }
        public float RagdollBalanceError { get; }
        public float StaggerDebtThreshold { get; }
        public float RagdollDebtThreshold { get; }
        public float DebtDecayPerSecond { get; }
        public float RecoveryStableSeconds { get; }
        public float RecoveryDurationSeconds { get; }
        public float RecoveryMaximumSpeed { get; }
        public float MaximumBalanceTorque { get; }
    }

    public sealed class CharacterPhysicalController
    {
        private readonly ActorId _actor;
        private readonly CharacterPhysicalTuning _tuning;
        private CharacterPhysicalMode _mode;
        private RecoveryCandidate _recovery;
        private float _staggerDebt;
        private float _modeSeconds;
        private float _stableSeconds;
        private float _muscleStrength = 1f;

        public CharacterPhysicalController(ActorId actor, CharacterPhysicalTuning tuning)
        {
            if (!actor.IsValid)
            {
                throw new ArgumentException("Actor must be valid.", nameof(actor));
            }

            _actor = actor;
            _tuning = tuning;
        }

        public CharacterPhysicalMode Mode => _mode;

        public void ForceFullRagdoll()
        {
            _mode = CharacterPhysicalMode.FullRagdoll;
            _modeSeconds = 0f;
            _stableSeconds = 0f;
            _staggerDebt = math.max(_staggerDebt, _tuning.RagdollDebtThreshold);
            _recovery = RecoveryCandidate.None;
            _muscleStrength = 0f;
        }

        public bool TryForceRecovery(RecoveryCandidate recovery)
        {
            if (_mode != CharacterPhysicalMode.FullRagdoll &&
                _mode != CharacterPhysicalMode.Recovery)
                return false;

            _recovery = recovery;
            Enter(CharacterPhysicalMode.Recovery);
            _muscleStrength = 0f;
            return true;
        }

        public void Reset()
        {
            _mode = CharacterPhysicalMode.AnimatedMotor;
            _modeSeconds = 0f;
            _stableSeconds = 0f;
            _staggerDebt = 0f;
            _recovery = RecoveryCandidate.None;
            _muscleStrength = 1f;
        }

        public void ApplyImpact(float impulse, float effectiveMass)
        {
            if (!float.IsFinite(impulse) || !float.IsFinite(effectiveMass) || impulse < 0f || effectiveMass <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(impulse));
            }

            float severity = impulse / effectiveMass;
            _staggerDebt = math.min(_staggerDebt + severity, _tuning.RagdollDebtThreshold * 4f);
            if (_staggerDebt >= _tuning.RagdollDebtThreshold)
            {
                Enter(CharacterPhysicalMode.FullRagdoll);
            }
            else if (_staggerDebt >= _tuning.StaggerDebtThreshold)
            {
                Enter(CharacterPhysicalMode.Stagger);
            }
            else if (_mode == CharacterPhysicalMode.AnimatedMotor)
            {
                Enter(CharacterPhysicalMode.PhysicalAssist);
            }
        }

        public CharacterPhysicalState Step(in CharacterPhysicalFrame frame)
        {
            Validate(frame);
            float dt = frame.DeltaTime;
            float3 up = math.normalizesafe(frame.GravityUp, new float3(0f, 1f, 0f));
            float balanceError = BalanceControllerMath.ComputeBalanceError(
                frame.CenterOfMass,
                frame.SupportCenter,
                up);
            _modeSeconds += dt;
            _staggerDebt = math.max(0f, _staggerDebt - (_tuning.DebtDecayPerSecond * dt));

            if (_mode != CharacterPhysicalMode.FullRagdoll &&
                _mode != CharacterPhysicalMode.Recovery &&
                (balanceError >= _tuning.RagdollBalanceError ||
                 _staggerDebt >= _tuning.RagdollDebtThreshold))
            {
                Enter(CharacterPhysicalMode.FullRagdoll);
            }

            switch (_mode)
            {
                case CharacterPhysicalMode.AnimatedMotor:
                    _muscleStrength = 1f;
                    if (balanceError >= _tuning.AssistBalanceError)
                    {
                        Enter(CharacterPhysicalMode.PhysicalAssist);
                    }
                    break;
                case CharacterPhysicalMode.PhysicalAssist:
                    _muscleStrength = 0.85f;
                    if (_staggerDebt >= _tuning.StaggerDebtThreshold)
                    {
                        Enter(CharacterPhysicalMode.Stagger);
                    }
                    else if (balanceError < _tuning.AssistBalanceError * 0.5f && _staggerDebt <= 0.05f)
                    {
                        Enter(CharacterPhysicalMode.AnimatedMotor);
                    }
                    break;
                case CharacterPhysicalMode.Stagger:
                    _muscleStrength = math.saturate(0.65f - (_staggerDebt / (_tuning.RagdollDebtThreshold * 2f)));
                    if (_staggerDebt < _tuning.StaggerDebtThreshold * 0.35f &&
                        balanceError < _tuning.RagdollBalanceError * 0.5f)
                    {
                        Enter(CharacterPhysicalMode.PhysicalAssist);
                    }
                    break;
                case CharacterPhysicalMode.FullRagdoll:
                    _muscleStrength = 0f;
                    bool stable = frame.ContactCount > 0 &&
                                  math.length(frame.LinearVelocity) <= _tuning.RecoveryMaximumSpeed &&
                                  math.length(frame.AngularVelocity) <= _tuning.RecoveryMaximumSpeed * 2f;
                    _stableSeconds = stable ? _stableSeconds + dt : 0f;
                    if (_stableSeconds >= _tuning.RecoveryStableSeconds)
                    {
                        _recovery = SelectRecovery(up, frame.ChestUp, frame.ChestRight);
                        Enter(CharacterPhysicalMode.Recovery);
                    }
                    break;
                case CharacterPhysicalMode.Recovery:
                    _muscleStrength = math.saturate(_modeSeconds / math.max(_tuning.RecoveryDurationSeconds, 0.001f));
                    if (_modeSeconds >= _tuning.RecoveryDurationSeconds)
                    {
                        _staggerDebt = 0f;
                        _recovery = RecoveryCandidate.None;
                        Enter(CharacterPhysicalMode.AnimatedMotor);
                    }
                    break;
            }

            return new CharacterPhysicalState(
                _actor,
                _mode,
                frame.LinearVelocity,
                frame.AngularVelocity,
                up,
                balanceError,
                _staggerDebt,
                _muscleStrength,
                _recovery);
        }

        public static RecoveryCandidate SelectRecovery(float3 gravityUp, float3 chestUp, float3 chestRight)
        {
            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float faceDot = math.dot(math.normalizesafe(chestUp, up), up);
            if (faceDot >= 0.45f)
            {
                return RecoveryCandidate.FaceUp;
            }

            if (faceDot <= -0.45f)
            {
                return RecoveryCandidate.FaceDown;
            }

            float sideDot = math.dot(math.normalizesafe(chestRight, new float3(1f, 0f, 0f)), up);
            return sideDot >= 0f ? RecoveryCandidate.RightSide : RecoveryCandidate.LeftSide;
        }

        private void Enter(CharacterPhysicalMode next)
        {
            if (_mode == next)
            {
                return;
            }

            _mode = next;
            _modeSeconds = 0f;
            _stableSeconds = 0f;
        }

        private static void Validate(in CharacterPhysicalFrame frame)
        {
            bool finite = float.IsFinite(frame.DeltaTime) && frame.DeltaTime > 0f &&
                          math.all(math.isfinite(frame.GravityUp)) &&
                          math.all(math.isfinite(frame.CenterOfMass)) &&
                          math.all(math.isfinite(frame.SupportCenter)) &&
                          math.all(math.isfinite(frame.LinearVelocity)) &&
                          math.all(math.isfinite(frame.AngularVelocity)) &&
                          math.all(math.isfinite(frame.ChestUp)) &&
                          math.all(math.isfinite(frame.ChestRight));
            if (!finite)
            {
                throw new ArgumentException("Physical frame must be finite with a positive delta time.");
            }
        }
    }

    public static class BalanceControllerMath
    {
        public static float ComputeBalanceError(float3 centerOfMass, float3 supportCenter, float3 gravityUp)
        {
            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            return math.length(ProjectOnPlane(centerOfMass - supportCenter, up));
        }

        public static float3 ComputeCorrectiveTorque(
            float3 centerOfMass,
            float3 supportCenter,
            float3 gravityUp,
            float gain,
            float maximumTorque)
        {
            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float3 offset = ProjectOnPlane(centerOfMass - supportCenter, up);
            float3 torque = math.cross(up, offset) * -gain;
            float magnitude = math.length(torque);
            if (magnitude > maximumTorque && magnitude > 0.0001f)
            {
                torque *= maximumTorque / magnitude;
            }

            return torque;
        }

        private static float3 ProjectOnPlane(float3 value, float3 normal)
        {
            return value - (normal * math.dot(value, normal));
        }
    }
}
