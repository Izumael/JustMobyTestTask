using Core.Domain;

namespace Core.Interfaces
{
    public interface IGameConfigProvider
    {
        GameConfig GetConfig();
    }
}
