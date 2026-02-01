using Core.Domain;
using Core.Interfaces;

namespace Infrastructure.Config
{
    public class ScriptableObjectGameConfigProvider : IGameConfigProvider
    {
        private readonly GameConfigSO _config;

        public ScriptableObjectGameConfigProvider(GameConfigSO config)
        {
            _config = config;
        }

        public GameConfig GetConfig()
        {
            return new GameConfig(_config.BottomCubeCount, _config.CubeColors);
        }
    }
}