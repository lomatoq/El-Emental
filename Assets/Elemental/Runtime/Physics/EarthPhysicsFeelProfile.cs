using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public enum EarthPhysicsBodyClass : byte
    {
        LightStone = 0,
        HeavyBlock = 1,
        Structure = 2
    }

    [CreateAssetMenu(menuName = "Elemental/Physics/Earth Physics Feel Profile", fileName = "EarthPhysicsFeelProfile")]
    public sealed class EarthPhysicsFeelProfile : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float stoneDynamicFriction = 0.62f;
        [SerializeField, Range(0f, 1f)] private float stoneStaticFriction = 0.78f;
        [SerializeField, Range(0f, 1f)] private float stoneBounciness = 0.08f;
        [Header("Heavy block")]
        [SerializeField, Range(0f, 1f)] private float heavyDynamicFriction = 0.72f;
        [SerializeField, Range(0f, 1f)] private float heavyStaticFriction = 0.86f;
        [SerializeField, Range(0f, 1f)] private float heavyBounciness = 0.035f;
        [Header("Structure")]
        [SerializeField, Range(0f, 1f)] private float structureDynamicFriction = 0.82f;
        [SerializeField, Range(0f, 1f)] private float structureStaticFriction = 0.94f;
        [SerializeField, Range(0f, 1f)] private float structureBounciness = 0.01f;
        [SerializeField, Min(0f)] private float lightImpactEnergy = 45f;
        [SerializeField, Min(0f)] private float heavyImpactEnergy = 650f;
        [SerializeField, Min(0f)] private float catastrophicImpactEnergy = 2600f;
        [SerializeField, Range(1f, 100f)] private float fastBodyCcdSpeed = 18f;
        [SerializeField, Min(1f)] private float maximumAngularSpeed = 22f;
        [SerializeField, Min(1f)] private float heavyMaximumAngularSpeed = 12f;
        [SerializeField, Min(1f)] private float structureMaximumAngularSpeed = 6f;
        [SerializeField, Min(1f)] private float lightMaximumDepenetrationSpeed = 16f;
        [SerializeField, Min(1f)] private float heavyMaximumDepenetrationSpeed = 10f;
        [SerializeField, Min(1f)] private float structureMaximumDepenetrationSpeed = 5f;
        [Header("Projectile sweep guard")]
        [SerializeField, Range(5f, 40f)] private float projectileSweepMinimumSpeed = 16f;
        [SerializeField, Range(0.5f, 1f)] private float projectileSweepExtentRatio = 0.82f;
        [SerializeField, Range(0.001f, 0.08f)] private float projectileSweepSkin = 0.015f;
        [SerializeField, Range(0f, 0.35f)] private float projectileSweepRebound = 0.06f;

        [System.NonSerialized] private PhysicsMaterial _lightMaterial;
        [System.NonSerialized] private PhysicsMaterial _heavyMaterial;
        [System.NonSerialized] private PhysicsMaterial _structureMaterial;

        public float StoneDynamicFriction => stoneDynamicFriction;
        public float StoneStaticFriction => stoneStaticFriction;
        public float StoneBounciness => stoneBounciness;
        public float LightImpactEnergy => lightImpactEnergy;
        public float HeavyImpactEnergy => Mathf.Max(lightImpactEnergy, heavyImpactEnergy);
        public float CatastrophicImpactEnergy => Mathf.Max(HeavyImpactEnergy, catastrophicImpactEnergy);
        public float FastBodyCcdSpeed => fastBodyCcdSpeed;
        public float MaximumAngularSpeed => maximumAngularSpeed;
        public float ProjectileSweepMinimumSpeed => projectileSweepMinimumSpeed;
        public float ProjectileSweepExtentRatio => projectileSweepExtentRatio;
        public float ProjectileSweepSkin => projectileSweepSkin;
        public float ProjectileSweepRebound => projectileSweepRebound;

        public void Apply(Rigidbody body, Collider collider, EarthPhysicsBodyClass bodyClass)
        {
            if (body != null)
            {
                body.maxAngularVelocity = bodyClass switch
                {
                    EarthPhysicsBodyClass.HeavyBlock => heavyMaximumAngularSpeed,
                    EarthPhysicsBodyClass.Structure => structureMaximumAngularSpeed,
                    _ => maximumAngularSpeed
                };
                body.maxDepenetrationVelocity = bodyClass switch
                {
                    EarthPhysicsBodyClass.HeavyBlock => heavyMaximumDepenetrationSpeed,
                    EarthPhysicsBodyClass.Structure => structureMaximumDepenetrationSpeed,
                    _ => lightMaximumDepenetrationSpeed
                };
            }
            if (collider != null) collider.sharedMaterial = MaterialFor(bodyClass);
        }

        private PhysicsMaterial MaterialFor(EarthPhysicsBodyClass bodyClass)
        {
            switch (bodyClass)
            {
                case EarthPhysicsBodyClass.HeavyBlock:
                    return _heavyMaterial ??= CreateMaterial(
                        "Earth Heavy Block Physics", heavyDynamicFriction, heavyStaticFriction, heavyBounciness);
                case EarthPhysicsBodyClass.Structure:
                    return _structureMaterial ??= CreateMaterial(
                        "Earth Structure Physics", structureDynamicFriction, structureStaticFriction, structureBounciness);
                default:
                    return _lightMaterial ??= CreateMaterial(
                        "Earth Light Stone Physics", stoneDynamicFriction, stoneStaticFriction, stoneBounciness);
            }
        }

        private static PhysicsMaterial CreateMaterial(
            string materialName,
            float dynamicFriction,
            float staticFriction,
            float bounciness)
        {
            return new PhysicsMaterial(materialName)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
    }
}
