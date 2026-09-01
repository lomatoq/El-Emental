using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum UnifiedLightingMaterialRole
    {
        Character = 0,
        IntactSandstone = 1,
        LooseRock = 2,
        FractureExterior = 3,
        FractureInterior = 4,
        PlanetGround = 5,
        MagicConstruct = 6
    }

    public enum UnifiedLightingMaterialFamily
    {
        Character = 0,
        SandstoneExterior = 1,
        SandstoneInterior = 2,
        PlanetGround = 3,
        MagicConstruct = 4
    }

    public enum UnifiedLightingProjectionMode
    {
        AuthoredUv = 0,
        ObjectLocal = 1,
        CapturedStructureLocal = 2,
        PlanetLocal = 3
    }

    public readonly struct UnifiedLightingRoleContract
    {
        public UnifiedLightingRoleContract(
            UnifiedLightingMaterialRole role,
            UnifiedLightingMaterialFamily family,
            UnifiedLightingProjectionMode projectionMode)
        {
            Role = role;
            Family = family;
            ProjectionMode = projectionMode;
        }

        public UnifiedLightingMaterialRole Role { get; }
        public UnifiedLightingMaterialFamily Family { get; }
        public UnifiedLightingProjectionMode ProjectionMode { get; }

        public static UnifiedLightingRoleContract Resolve(
            UnifiedLightingMaterialRole role)
        {
            switch (role)
            {
                case UnifiedLightingMaterialRole.Character:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.Character,
                        UnifiedLightingProjectionMode.AuthoredUv);
                case UnifiedLightingMaterialRole.LooseRock:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.SandstoneExterior,
                        UnifiedLightingProjectionMode.ObjectLocal);
                case UnifiedLightingMaterialRole.IntactSandstone:
                case UnifiedLightingMaterialRole.FractureExterior:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.SandstoneExterior,
                        UnifiedLightingProjectionMode.CapturedStructureLocal);
                case UnifiedLightingMaterialRole.FractureInterior:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.SandstoneInterior,
                        UnifiedLightingProjectionMode.CapturedStructureLocal);
                case UnifiedLightingMaterialRole.PlanetGround:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.PlanetGround,
                        UnifiedLightingProjectionMode.PlanetLocal);
                case UnifiedLightingMaterialRole.MagicConstruct:
                    return new UnifiedLightingRoleContract(
                        role,
                        UnifiedLightingMaterialFamily.MagicConstruct,
                        UnifiedLightingProjectionMode.ObjectLocal);
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(role),
                        role,
                        "Unknown unified-lighting material role.");
            }
        }
    }

    public readonly struct UnifiedLightingProjectionFrame
    {
        public UnifiedLightingProjectionFrame(
            UnifiedLightingProjectionMode mode,
            Vector3 planetCenterWorld,
            Matrix4x4 localToStructure)
        {
            Mode = mode;
            PlanetCenterWorld = planetCenterWorld;
            LocalToStructure = localToStructure;
        }

        public UnifiedLightingProjectionMode Mode { get; }
        public Vector3 PlanetCenterWorld { get; }
        public Matrix4x4 LocalToStructure { get; }
        public Matrix4x4 NormalToStructure => LocalToStructure.inverse.transpose;

        public bool IsValid
        {
            get
            {
                if (!DuelShadowMath.IsFinite(PlanetCenterWorld))
                    return false;
                if (Mode != UnifiedLightingProjectionMode.CapturedStructureLocal)
                    return true;
                return DuelShadowMath.IsFinite(LocalToStructure) &&
                    Mathf.Abs(LocalToStructure.determinant) > 0.000001f;
            }
        }

        public bool TryResolveMappingPosition(
            Vector3 positionObject,
            Vector3 positionWorld,
            out Vector3 mappingPosition)
        {
            mappingPosition = default;
            if (!IsValid ||
                !DuelShadowMath.IsFinite(positionObject) ||
                !DuelShadowMath.IsFinite(positionWorld))
                return false;
            switch (Mode)
            {
                case UnifiedLightingProjectionMode.PlanetLocal:
                    mappingPosition = positionWorld - PlanetCenterWorld;
                    break;
                case UnifiedLightingProjectionMode.CapturedStructureLocal:
                    mappingPosition = LocalToStructure.MultiplyPoint3x4(positionObject);
                    break;
                default:
                    mappingPosition = positionObject;
                    break;
            }
            return DuelShadowMath.IsFinite(mappingPosition);
        }

        public bool TryResolveMappingNormal(
            Vector3 normalObject,
            Vector3 normalWorld,
            out Vector3 mappingNormal)
        {
            mappingNormal = default;
            if (!IsValid ||
                !DuelShadowMath.IsFinite(normalObject) ||
                !DuelShadowMath.IsFinite(normalWorld))
                return false;
            switch (Mode)
            {
                case UnifiedLightingProjectionMode.PlanetLocal:
                    mappingNormal = normalWorld;
                    break;
                case UnifiedLightingProjectionMode.CapturedStructureLocal:
                    mappingNormal = NormalToStructure.MultiplyVector(normalObject);
                    break;
                default:
                    mappingNormal = normalObject;
                    break;
            }
            float lengthSquared = mappingNormal.sqrMagnitude;
            if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.0000001f)
                return false;
            mappingNormal /= Mathf.Sqrt(lengthSquared);
            return DuelShadowMath.IsFinite(mappingNormal);
        }
    }

    public static class UnifiedLightingMath
    {
        public static Vector3 EvaluateTriplanarWeights(Vector3 normal, float sharpness)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(normal.x),
                Mathf.Abs(normal.y),
                Mathf.Abs(normal.z));
            float exponent = Mathf.Max(1f, sharpness);
            Vector3 weights = new Vector3(
                Mathf.Pow(absolute.x, exponent),
                Mathf.Pow(absolute.y, exponent),
                Mathf.Pow(absolute.z, exponent));
            float sum = weights.x + weights.y + weights.z;
            return float.IsFinite(sum) && sum > 0.000001f
                ? weights / sum
                : Vector3.zero;
        }

        public static float EvaluateDiffuseRamp(float normalLightDot)
        {
            float wrapped = Mathf.Clamp01((normalLightDot + 0.24f) / 1.24f);
            return SmoothStep(0.08f, 0.92f, wrapped);
        }

        public static float EvaluateBaseFormLuminance(
            float normalLightDot,
            float ambientStrength,
            float shadowFloor)
        {
            float ramp = EvaluateDiffuseRamp(normalLightDot);
            float direct = Mathf.Lerp(
                Mathf.Clamp(shadowFloor, 0.3f, 0.8f),
                1f,
                ramp);
            float ambient = Mathf.Max(0f, ambientStrength) * 0.18f;
            return direct + ambient;
        }

        private static float SmoothStep(float minimum, float maximum, float value)
        {
            float t = Mathf.Clamp01((value - minimum) / (maximum - minimum));
            return t * t * (3f - 2f * t);
        }
    }
}
