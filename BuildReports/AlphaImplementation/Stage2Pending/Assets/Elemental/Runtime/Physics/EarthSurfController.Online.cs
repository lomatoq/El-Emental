using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthSurfController
    {
        /// <summary>Explicit owned outputs include released cells no longer parented to the board.</summary>
        public void AppendOnlineWorldRoots(List<Transform> destination)
        {
            if (_boardBody != null) destination.Add(_boardBody.transform);
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i]?.Transform != null && _cells[i].Transform.parent != _boardVisualRoot) destination.Add(_cells[i].Transform);
            for (int i = 0; i < _releasedCells.Length; i++)
                if (_releasedCells[i]?.Transform != null) destination.Add(_releasedCells[i].Transform);
        }
    }
}
