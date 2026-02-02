using System.Collections.Generic;
using DG.Tweening;
using UI.DropZones;
using UI.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Gameplay.Cubes
{
    [RequireComponent(typeof(CubeItemView))]
    public class CubeDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private float minDragDistance = 10f;

        [SerializeField]
        private float directionalThreshold = 1.5f;

        [SerializeField]
        private CubeItemView _ghostItemPrefab;

        private ScrollRect _scrollRect;
        private RectTransform _dragLayer;
        private DiContainer _diContainer;

        private CubeItemView _source;
        private CubeItemView _ghost;

        private Vector2 _pointerOffsetLocal;
        private bool _isDragging;
        private bool _isScrolling;
        private bool _pointerDown;
        private Vector2 _pointerDownScreenPos;
        private Camera _eventCamera;

        [Inject]
        private void Construct(DiContainer container)
        {
            _diContainer = container;
        }

        public void Initialize(ScrollRect scrollRect, RectTransform dragLayer)
        {
            _scrollRect = scrollRect;
            _dragLayer = dragLayer;
        }

        private void Awake()
        {
            _source = GetComponent<CubeItemView>();
        }

        private void StartDrag()
        {
            _isDragging = true;

            //Создаём ghost-клон, которого фактически будем тащить
            //TODO: заменить на пулл
            _ghost = _diContainer.InstantiatePrefabForComponent<CubeItemView>(
                _ghostItemPrefab,
                _dragLayer
            );

            _ghost.Bind(_source.Descriptor);

            //Устанавливаем ghost последним ребёнком, чтобы он отрисовывался поверх всего
            _ghost.transform.SetAsLastSibling();

            //Для корректного определения drop-зоны ghost не должен блокировать рейкаст
            _ghost.SetBlocksRaycasts(false);

            //Отключаем CubeDragHandler на ghost для защиты от рекурсии
            CubeDragHandler ghostHandler = _ghost.GetComponent<CubeDragHandler>();
            if (ghostHandler != null)
            {
                ghostHandler.enabled = false;
            }

            //Source тоже не блокирует во время драга
            _source.SetBlocksRaycasts(false);

            //Сразу позиционируем ghost под курсором
            MoveGhost(Input.mousePosition);
        }

        private void MoveGhost(Vector2 screenPos)
        {
            if (_ghost == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer,
                    screenPos,
                    _eventCamera,
                    out Vector2 localPoint
                )
            )
            {
                //Смещение, чтобы курсор держал кубик за место клика
                _ghost.RectTransform.anchoredPosition = localPoint - _pointerOffsetLocal;
            }
        }

        private void EndDrag(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                return;
            }

            //Возвращаем блокировку рейкаста source обратно
            if (_source != null)
            {
                _source.SetBlocksRaycasts(true);
            }

            HandleDrop(eventData);

            ResetState();
        }

        private void HandleDrop(PointerEventData eventData)
        {
            GameObject dropTarget = null;

            if (eventData != null)
            {
                dropTarget = eventData.pointerCurrentRaycast.gameObject;
            }
            else
            {
                //Ручной рейкаст через EventSystem (если eventData == null)
                List<RaycastResult> results = new List<RaycastResult>();
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                EventSystem.current.RaycastAll(pointerData, results);
                if (results.Count > 0)
                {
                    dropTarget = results[0].gameObject;
                }
            }

            if (dropTarget == null)
            {
                Debug.Log("Dropped over: nothing");

                AnimateCubeDisappear(_ghost);
                _ghost = null;
                return;
            }

            TowerDropZone towerDropZone = dropTarget.GetComponentInParent<TowerDropZone>();
            if (towerDropZone != null)
            {
                Debug.Log($"Dropped over Tower: {dropTarget.name}");

                bool added = towerDropZone.AddCube(_ghost, destroyAfterAnimation: false);
                if (added)
                {
                    _ghost = null;
                }
                else
                {
                    Debug.Log("Tower is full, cube rejected");
                    AnimateCubeDisappear(_ghost);
                    _ghost = null;
                }
            }
            else
            {
                Debug.Log("Dropped outside of tower");
                AnimateCubeDisappear(_ghost);
                _ghost = null;
            }
        }

        private void AnimateCubeDisappear(CubeItemView cube)
        {
            if (cube == null || cube.RectTransform == null)
            {
                Debug.LogWarning("Cube already destroyed, skipping animation.");
                return;
            }

            cube.RectTransform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetTarget(cube.RectTransform)
                .SetAutoKill(true)
                .OnComplete(() =>
                {
                    if (cube != null && cube.gameObject != null)
                    {
                        Destroy(cube.gameObject);
                    }
                });
        }

        private void ResetState()
        {
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
            }

            _ghost = null;
            _isDragging = false;
            _isScrolling = false;
            _pointerDown = false;
            _pointerOffsetLocal = Vector2.zero;
            _pointerDownScreenPos = Vector2.zero;
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _isDragging = false;
            _eventCamera = eventData.pressEventCamera;

            //Кэшируем точку клика для определения направления движения
            _pointerDownScreenPos = eventData.position;

            //Рассчитываем смещение внутри куба, чтобы он не прыгал центром под курсором
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _source.RectTransform,
                eventData.position,
                _eventCamera,
                out Vector2 sourcePoint
            );

            _pointerOffsetLocal = sourcePoint;
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            //Если драг не начался (клик без движения), завершаем состояние
            if (!_isDragging && !_isScrolling)
            {
                _pointerDown = false;
            }
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            //Определяем направление движения для выбора режима: скролл или драг
            Vector2 delta = eventData.position - _pointerDownScreenPos;

            //Проверяем минимальную дистанцию для надежного определения направления
            if (delta.magnitude < minDragDistance)
            {
                return;
            }

            float horizontal = Mathf.Abs(delta.x);
            float vertical = Mathf.Abs(delta.y);

            //Используем порог для четкого разделения направлений
            //directionalThreshold = 1.5 означает что горизонталь должна быть в 1.5 раза больше вертикали
            if (horizontal > vertical * directionalThreshold)
            {
                //Горизонтальное движение - скроллим BottomPanel
                _isScrolling = true;
                if (_scrollRect != null)
                {
                    ExecuteEvents.Execute(_scrollRect.gameObject, eventData, ExecuteEvents.beginDragHandler);
                }
            }
            else if (vertical > horizontal * directionalThreshold)
            {
                //Вертикальное движение - драгим кубик
                _isScrolling = false;
                StartDrag();
            }
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            if (_isScrolling && _scrollRect != null)
            {
                //Режим скролла - передаём события в ScrollRect
                ExecuteEvents.Execute(_scrollRect.gameObject, eventData, ExecuteEvents.dragHandler);
            }
            else if (_isDragging)
            {
                //Режим драга - двигаем ghost
                MoveGhost(eventData.position);
            }
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            if (_isScrolling && _scrollRect != null)
            {
                //Завершаем скролл
                ExecuteEvents.Execute(_scrollRect.gameObject, eventData, ExecuteEvents.endDragHandler);
                _isScrolling = false;
            }
            else if (_isDragging)
            {
                //Завершаем драг и обрабатываем drop
                EndDrag(eventData);
            }

            _pointerDown = false;
        }

        private void Update()
        {
            //Fallback на случай если OnEndDrag не сработал
            if (_isDragging && !Input.GetMouseButton(0))
            {
                EndDrag(null);
            }
        }

        private void OnDestroy()
        {
            if (_isDragging || _pointerDown)
            {
                if (_source != null)
                {
                    _source.SetBlocksRaycasts(true);
                }

                ResetState();
            }
        }

        void IInitializePotentialDragHandler.OnInitializePotentialDrag(PointerEventData eventData)
        {
            //Сообщаем EventSystem что мы обрабатываем drag события
        }
    }
}
