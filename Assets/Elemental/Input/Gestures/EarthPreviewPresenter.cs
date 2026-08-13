using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    [DisallowMultipleComponent]
    public sealed class EarthPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;

        public int PositionCount => line != null ? line.positionCount : 0;

        public void Configure(LineRenderer configuredLine) => line = configuredLine;

        public void Present(IReadOnlyList<Vector3> points)
        {
            if (line == null) return;
            int count = points?.Count ?? 0;
            line.positionCount = count;
            for (int index = 0; index < count; index++) line.SetPosition(index, points[index]);
        }

        public void Clear()
        {
            if (line != null) line.positionCount = 0;
        }
    }
}
