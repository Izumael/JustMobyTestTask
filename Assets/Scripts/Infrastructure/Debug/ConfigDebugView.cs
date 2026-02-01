using Core.Interfaces;
using UnityEngine;
using Zenject;

namespace Infrastructure.Debug
{
    public class ConfigDebugView : MonoBehaviour
    {
        private IGameConfigProvider _provider;

        [Inject]
        public void Construct(IGameConfigProvider provider)
        {
            _provider = provider;
        }

        private void Start()
        {
            var cfg = _provider.GetConfig();
            UnityEngine.Debug.Log($"Bottom cubes: {cfg.BottomCubeCount}, colors: {cfg.CubeColors.Count}");
        }
    }
}