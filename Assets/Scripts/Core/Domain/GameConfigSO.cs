using System.Collections.Generic;
using UnityEngine;

namespace Core.Domain
{
    [CreateAssetMenu(menuName = "JustMoby/Game Config", fileName = "GameConfigSO")]
    public class GameConfigSO : ScriptableObject
    {
        [Min(1)]
        public int BottomCubeCount = 20;
        public List<Color> CubeColors = new List<Color>()
        {
            Color.red, Color.green, Color.blue, Color.yellow
        };

        private void OnValidate()
        {
            BottomCubeCount = Mathf.Max(1, BottomCubeCount);

            CubeColors ??= new List<Color>();
        }
    }
}