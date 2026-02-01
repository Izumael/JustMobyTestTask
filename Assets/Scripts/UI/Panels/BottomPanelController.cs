using Core.Domain;
using Core.Interfaces;
using Gameplay.Cubes;
using UI.Views;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace UI.Panels
{
    public class BottomPanelController : MonoBehaviour
    {
        [SerializeField]
        private Transform contentRoot;

        [SerializeField]
        private ScrollRect scrollRect;

        [SerializeField]
        private RectTransform dragLayer;

        [SerializeField]
        private CubeItemView cubeItemPrefab;

        private IGameConfigProvider _gameConfigProvider;
        private DiContainer _container;

        [Inject]
        public void Construct(IGameConfigProvider gameConfigProvider, DiContainer container)
        {
            _gameConfigProvider = gameConfigProvider;
            _container = container;
        }

        private void Start()
        {
            if (contentRoot == null)
            {
                Debug.LogError("contentRoot is not set");
                return;
            }

            if (cubeItemPrefab == null)
            {
                Debug.LogError("cubeItemPrefab is not set");
                return;
            }

            Initialize();
        }

        private void Initialize()
        {
            //Очистка на случай повторного инициализации
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }

            GameConfig config = _gameConfigProvider.GetConfig();

            if (config.CubeColors == null || config.CubeColors.Count == 0)
            {
                Debug.LogError("cubeColors is not set");
                return;
            }

            for (int i = 0; i < config.BottomCubeCount; i++)
            {
                //Инстанциируем view через контейнер, чтобы пробросить зависимости
                CubeItemView item = _container.InstantiatePrefabForComponent<CubeItemView>(
                    cubeItemPrefab,
                    contentRoot
                );

                //Цвета объектам устанавливаем "по кругу"
                Color color = config.CubeColors[i % config.CubeColors.Count];
                item.Bind(new CubeDescriptor(color));
                CubeDragHandler drag = item.gameObject.AddComponent<CubeDragHandler>();
                drag.Initialize(scrollRect, dragLayer, cubeItemPrefab, _container);
            }
        }
    }
}