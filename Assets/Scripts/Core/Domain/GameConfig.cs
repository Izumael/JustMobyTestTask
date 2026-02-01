using System.Collections.Generic;
using UnityEngine;

namespace Core.Domain
{
    public class GameConfig
    {
        public int BottomCubeCount { get; }
        public List<Color> CubeColors { get; }

        public GameConfig(int bottomCubeCount, List<Color> cubeColors)
        {
            BottomCubeCount = bottomCubeCount;
            CubeColors = cubeColors;
        }
    }
}