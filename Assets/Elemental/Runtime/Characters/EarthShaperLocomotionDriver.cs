using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class EarthShaperLocomotionDriver : MonoBehaviour
    {
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private MagicExecutor magic;
        [SerializeField] private Transform targetsRoot;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftUpperLeg;
        [SerializeField] private Transform leftLowerLeg;
        [SerializeField] private Transform rightUpperLeg;
        [SerializeField] private Transform rightLowerLeg;

        private Vector3 _rootBasePosition;
        private Quaternion _chestBase;
        private Quaternion _leftArmBase;
        private Quaternion _rightArmBase;
        private Quaternion _leftUpperBase;
        private Quaternion _leftLowerBase;
        private Quaternion _rightUpperBase;
        private Quaternion _rightLowerBase;
        private float _stridePhase;

        public void Configure(
            Rigidbody configuredBody,
            PlanetMotor configuredMotor,
            ActiveRagdollPuppet configuredPuppet,
            MagicExecutor configuredMagic,
            Transform configuredTargetsRoot,
            Transform configuredChest,
            Transform configuredLeftArm,
            Transform configuredRightArm,
            Transform configuredLeftUpperLeg,
            Transform configuredLeftLowerLeg,
            Transform configuredRightUpperLeg,
            Transform configuredRightLowerLeg)
        {
            rootBody = configuredBody;
            motor = configuredMotor;
            puppet = configuredPuppet;
            magic = configuredMagic;
            targetsRoot = configuredTargetsRoot;
            chest = configuredChest;
            leftArm = configuredLeftArm;
            rightArm = configuredRightArm;
            leftUpperLeg = configuredLeftUpperLeg;
            leftLowerLeg = configuredLeftLowerLeg;
            rightUpperLeg = configuredRightUpperLeg;
            rightLowerLeg = configuredRightLowerLeg;
            CaptureBases();
        }

        private void Awake()
        {
            if (rootBody == null) rootBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            CaptureBases();
        }

        private void Update()
        {
            if (targetsRoot == null || rootBody == null || motor == null) return;
            CharacterPhysicalMode mode = puppet != null
                ? puppet.CurrentState.Mode
                : CharacterPhysicalMode.AnimatedMotor;
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(rootBody.linearVelocity, motor.LocalUp);
            float speed01 = Mathf.Clamp01(tangentVelocity.magnitude / 6.4f);
            _stridePhase += Time.deltaTime * Mathf.Lerp(2.2f, 10.5f, speed01);
            float stride = Mathf.Sin(_stridePhase) * speed01;
            float knee = Mathf.Max(0f, Mathf.Sin(_stridePhase + Mathf.PI * 0.5f)) * speed01;
            float breathing = Mathf.Sin(Time.time * 1.7f) * (1f - speed01);
            bool magicArms = magic != null && magic.HeldBody != null;

            if (mode == CharacterPhysicalMode.FullRagdoll)
            {
                targetsRoot.localPosition = Vector3.Lerp(targetsRoot.localPosition, _rootBasePosition, Time.deltaTime * 2f);
                return;
            }

            float stagger = mode == CharacterPhysicalMode.Stagger ? 1f : 0f;
            Vector3 bob = Vector3.up * ((Mathf.Abs(Mathf.Sin(_stridePhase)) * 0.055f * speed01) + (breathing * 0.012f));
            targetsRoot.localPosition = Vector3.Lerp(
                targetsRoot.localPosition,
                _rootBasePosition + bob,
                Time.deltaTime * 12f);
            chest.localRotation = Quaternion.Slerp(
                chest.localRotation,
                _chestBase * Quaternion.Euler((breathing * 1.8f) + (stagger * 18f), 0f, -stride * 4f),
                Time.deltaTime * 10f);
            leftUpperLeg.localRotation = Quaternion.Slerp(
                leftUpperLeg.localRotation,
                _leftUpperBase * Quaternion.Euler(stride * 34f, 0f, 0f),
                Time.deltaTime * 14f);
            rightUpperLeg.localRotation = Quaternion.Slerp(
                rightUpperLeg.localRotation,
                _rightUpperBase * Quaternion.Euler(-stride * 34f, 0f, 0f),
                Time.deltaTime * 14f);
            leftLowerLeg.localRotation = Quaternion.Slerp(
                leftLowerLeg.localRotation,
                _leftLowerBase * Quaternion.Euler(-Mathf.Max(0f, -stride) * 38f - (knee * 14f), 0f, 0f),
                Time.deltaTime * 16f);
            rightLowerLeg.localRotation = Quaternion.Slerp(
                rightLowerLeg.localRotation,
                _rightLowerBase * Quaternion.Euler(-Mathf.Max(0f, stride) * 38f - ((speed01 - knee) * 14f), 0f, 0f),
                Time.deltaTime * 16f);
            if (magicArms) return;
            leftArm.localRotation = Quaternion.Slerp(
                leftArm.localRotation,
                _leftArmBase * Quaternion.Euler(-stride * 18f + (stagger * 28f), 0f, 0f),
                Time.deltaTime * 10f);
            rightArm.localRotation = Quaternion.Slerp(
                rightArm.localRotation,
                _rightArmBase * Quaternion.Euler(stride * 18f + (stagger * 28f), 0f, 0f),
                Time.deltaTime * 10f);
        }

        private void CaptureBases()
        {
            if (targetsRoot != null) _rootBasePosition = targetsRoot.localPosition;
            if (chest != null) _chestBase = chest.localRotation;
            if (leftArm != null) _leftArmBase = leftArm.localRotation;
            if (rightArm != null) _rightArmBase = rightArm.localRotation;
            if (leftUpperLeg != null) _leftUpperBase = leftUpperLeg.localRotation;
            if (leftLowerLeg != null) _leftLowerBase = leftLowerLeg.localRotation;
            if (rightUpperLeg != null) _rightUpperBase = rightUpperLeg.localRotation;
            if (rightLowerLeg != null) _rightLowerBase = rightLowerLeg.localRotation;
        }
    }
}
