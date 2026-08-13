using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public sealed class GravityWorld
    {
        private readonly List<IGravityField> _fields;

        public GravityWorld(int initialCapacity = 4)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _fields = new List<IGravityField>(initialCapacity);
        }

        public int Count => _fields.Count;

        public void Register(IGravityField field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            for (int index = 0; index < _fields.Count; index++)
            {
                if (_fields[index].Id == field.Id)
                {
                    throw new InvalidOperationException($"Gravity field ID {field.Id} is already registered.");
                }
            }

            _fields.Add(field);
        }

        public bool Unregister(GravityFieldId id)
        {
            for (int index = 0; index < _fields.Count; index++)
            {
                if (_fields[index].Id != id)
                {
                    continue;
                }

                _fields.RemoveAt(index);
                return true;
            }

            return false;
        }

        public GravitySample Sample(float3 worldPosition, uint tick)
        {
            GravitySample strongest = GravitySample.None;
            float strongestMagnitudeSquared = -1f;

            for (int index = 0; index < _fields.Count; index++)
            {
                GravitySample candidate = _fields[index].Sample(worldPosition, tick);
                if (!candidate.IsFinite)
                {
                    continue;
                }

                float magnitudeSquared = math.lengthsq(candidate.Acceleration);
                if (magnitudeSquared <= strongestMagnitudeSquared)
                {
                    continue;
                }

                strongest = candidate;
                strongestMagnitudeSquared = magnitudeSquared;
            }

            return strongest;
        }
    }
}
