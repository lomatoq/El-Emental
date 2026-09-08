using UnityEngine;

namespace Elemental.Presentation.Fire
{
    // Explicit shared lab/world owner. There is no global light registry.
    public sealed class FireLightPool : MonoBehaviour
    {
        [SerializeField, Range(0,2)] private int capacity = 2;
        private readonly Light[] lights = new Light[2];
        private readonly Object[] owners = new Object[2];
        private void Awake()
        {
            for (int i = 0; i < capacity; i++)
            {
                var child = new GameObject("Fire light " + i);
                child.transform.SetParent(transform, false);
                var lamp = child.AddComponent<Light>();
                lamp.type = LightType.Point; lamp.shadows = LightShadows.None;
                lamp.color = new Color(1, 0.32f, 0.06f); lamp.enabled = false;
                lights[i] = lamp;
            }
        }
        public void Publish(Object owner, Vector3 position, float intensity, float range)
        {
            int available = -1;
            for (int i = 0; i < capacity; i++)
            {
                if (owners[i] == owner) { available = i; break; }
                if (owners[i] == null && available < 0) available = i;
            }
            if (available < 0 || lights[available] == null) return;
            owners[available] = owner;
            var lamp = lights[available]; lamp.transform.position = position;
            lamp.intensity = intensity; lamp.range = range; lamp.enabled = intensity > 0;
        }
        public void Release(Object owner)
        {
            for (int i = 0; i < capacity; i++)
                if (owners[i] == owner) { owners[i] = null; if (lights[i] != null) lights[i].enabled = false; }
        }
    }
}
