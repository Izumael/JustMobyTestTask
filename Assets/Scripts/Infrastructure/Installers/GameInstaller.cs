using Core.Domain;
using Core.Interfaces;
using Infrastructure.Config;
using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private GameConfigSO _gameConfigSO;

    public override void InstallBindings()
    {
        Container.BindInstance(_gameConfigSO).AsSingle();

        Container.Bind<IGameConfigProvider>().To<ScriptableObjectGameConfigProvider>().AsSingle();
    }
}