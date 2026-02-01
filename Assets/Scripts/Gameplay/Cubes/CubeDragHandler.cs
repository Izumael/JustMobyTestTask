using System;
using UI.DropZones;
using UI.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Gameplay.Cubes
{
    [RequireComponent(typeof(CubeItemView))]
    public class CubeDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private float dragStartThreshold = 15f;

        private ScrollRect _scrollRect;
        private RectTransform _dragLayer;
        private CubeItemView _ghostItemPrefab;
        private DiContainer _diContainer;

        private CubeItemView _source;
        private CubeItemView _ghost;

        private Vector2 _pointerOffsetLocal;
        private bool _isDragging;
        private Vector2 _pointerDownScreenPos;

        public void Initialize(
            ScrollRect scrollRect,
            RectTransform dragLayer,
            CubeItemView ghostPrefab,
            DiContainer diContainer
        )
        {
            _scrollRect = scrollRect;
            _dragLayer = dragLayer;
            _ghostItemPrefab = ghostPrefab;
            _diContainer = diContainer;
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (_ghost == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint)
               )
            {
                //Смещение, чтобы курсор держал кубик за место клика
                _ghost.RectTransform.anchoredPosition = localPoint - _pointerOffsetLocal;
            }
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            //Кэшируем точку клика и смещение внутри куба, чтобы он не прыгал центром под курсором
            _pointerDownScreenPos = eventData.position;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _source.RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out _pointerOffsetLocal
            );

            _isDragging = false;
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                //Проверка, скроллит игрок ули уже драгает
                float dist = Vector2.Distance(eventData.position, _pointerDownScreenPos);
                if (dist < dragStartThreshold)
                {
                    return;
                }

                //Порог пройден — игрок инициировал drag
                StartDrag();
            }

            MoveGhost(eventData);
            return;

            void StartDrag()
            {
                _isDragging = true;

                //Отключаем ScrollRect, чтобы он не скроллился во время драга
                if (_scrollRect != null)
                {
                    _scrollRect.enabled = false;
                }

                //Создаём ghost-клон, которого фактически будем тащить
                //TODO: заменить на пулл
                _ghost = _diContainer.InstantiatePrefabForComponent<CubeItemView>(_ghostItemPrefab, _dragLayer);
                _ghost.Bind(_source.Descriptor);

                //Устаналиваем ghost последним ребенком, чтобы он отрисовывался поверх всего
                _ghost.transform.SetAsLastSibling();

                //Для корректного определения drop-зоны ghost не должен блокировать рейкаст
                _ghost.SetBlocksRaycasts(false);

                //TODO: делать ghost полупрозрачным?
            }
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            //Включаем ScrollRect обратно
            if (_scrollRect != null)
            {
                _scrollRect.enabled = true;
            }

            if (!_isDragging)
            {
                return;
            }

            //Определяем, что под курсором в момент дропа
            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;

            //TODO: - Drop в башню
            //TODO: - Drop в дыру
            //TODO: - Иначе промах -> анимация исчезновения
            if (dropTarget != null)
            {
                if (dropTarget.GetComponentInParent<TowerDropZone>() != null)
                {
                    Debug.Log($"Dropped over Tower: {dropTarget.name}");
                }
                else if (dropTarget.GetComponentInParent<HoleDropZone>() != null)
                {
                    Debug.Log($"Dropped over Hole: {dropTarget.name}");
                }
            }
            else
            {
                Debug.Log("Dropped over: nothing");
            }

            //Удаляем ghost
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
            }

            _ghost = null;
            _pointerOffsetLocal = Vector2.zero;
            _pointerDownScreenPos = Vector2.zero;
            _isDragging = false;
        }

        private void Awake()
        {
            _source = GetComponent<CubeItemView>();
        }
    }
}