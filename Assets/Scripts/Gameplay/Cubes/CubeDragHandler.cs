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
    public class CubeDragHandler :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
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

        private bool _isFromTower;
        private int _towerIndex;

        private HoleDropZone _holeDropZone;

        [Inject]
        private void Construct(DiContainer container)
        {
            _diContainer = container;
        }

        public void Initialize(ScrollRect scrollRect, RectTransform dragLayer, HoleDropZone holeDropZone = null, CubeItemView ghostPrefab = null)
        {
            _scrollRect = scrollRect;
            _dragLayer = dragLayer;
            _holeDropZone = holeDropZone;

            //Если HoleDropZone не передана, ищем в сцене
            if (_holeDropZone == null)
            {
                _holeDropZone = FindObjectOfType<HoleDropZone>();
            }

            //Если передан ghostPrefab, используем его
            if (ghostPrefab != null)
            {
                _ghostItemPrefab = ghostPrefab;
            }
        }

        private void Awake()
        {
            _source = GetComponent<CubeItemView>();
        }

        private void HideSource()
        {
            //Скрываем через прозрачность, НЕ через SetActive
            //SetActive(false) блокирует события!
            CanvasGroup canvasGroup = _source.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void ShowSource()
        {
            //Восстанавливаем прозрачность
            CanvasGroup canvasGroup = _source.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private void StartDrag()
        {
            _isDragging = true;

            if (_dragLayer == null)
            {
                Debug.LogError("_dragLayer is null! Cannot start drag.");
                ResetState();
                return;
            }

            if (_ghostItemPrefab == null)
            {
                Debug.LogError("_ghostItemPrefab is null! Cannot create ghost.");
                ResetState();
                return;
            }

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

            //Для кубов из башни: используем центр куба как offset (держим за центр)
            if (_isFromTower)
            {
                //Offset = центр куба (нулевая точка в локальных координатах ghost)
                _pointerOffsetLocal = Vector2.zero;

                //Скрываем оригинал визуально через прозрачность (НЕ SetActive!)
                HideSource();
            }

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

            //Сначала проверяем дроп на HoleDropZone с овальной проверкой
            if (_holeDropZone != null && _holeDropZone.IsPointInsideOval(Input.mousePosition, _eventCamera))
            {

                if (_isFromTower)
                {
                    //Удаляем куб из башни по индексу
                    if (_holeDropZone.TryRemoveCubeAt(_towerIndex, out CubeItemView removedCube))
                    {
                        AnimateCubeIntoHole(removedCube);
                    }
                }

                //Ghost летит в дыру с анимацией засасывания
                AnimateCubeIntoHole(_ghost);
                _ghost = null;
                return;
            }

            if (dropTarget == null)
            {

                //Возвращаем куб если он из башни
                if (_isFromTower && _source != null)
                {
                    ShowSource();
                }

                AnimateCubeDisappear(_ghost);
                _ghost = null;
                return;
            }

            //Проверяем дроп на TowerDropZone
            TowerDropZone towerDropZone = dropTarget.GetComponentInParent<TowerDropZone>();
            if (towerDropZone != null)
            {

                if (_isFromTower)
                {
                    //Куб из башни - возвращаем обратно (показываем source)
                    if (_source != null)
                    {
                        ShowSource();
                    }
                    AnimateCubeDisappear(_ghost);
                    _ghost = null;
                }
                else
                {
                    //Куб из нижней панели - добавляем в башню
                    bool added = towerDropZone.AddCube(_ghost, destroyAfterAnimation: false);
                    if (added)
                    {
                        //Включаем обратно raycast и CubeDragHandler для кубов в башне
                        //чтобы их можно было перетащить в дыру
                        _ghost.SetBlocksRaycasts(true);

                        CubeDragHandler ghostHandler = _ghost.GetComponent<CubeDragHandler>();
                        if (ghostHandler != null)
                        {
                            ghostHandler.enabled = true;
                            ghostHandler.Initialize(_scrollRect, _dragLayer, _holeDropZone, _ghostItemPrefab);
                        }

                        _ghost = null;
                    }
                    else
                    {
                        AnimateCubeDisappear(_ghost);
                        _ghost = null;
                    }
                }
                return;
            }

            //Дропнули куда-то еще - куб исчезает

            //Возвращаем куб если он из башни
            if (_isFromTower && _source != null)
            {
                ShowSource();
            }

            AnimateCubeDisappear(_ghost);
            _ghost = null;
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

        private void AnimateCubeIntoHole(CubeItemView cube)
        {
            if (cube == null || cube.RectTransform == null)
            {
                Debug.LogWarning("Cube already destroyed, skipping animation.");
                return;
            }

            if (_holeDropZone == null)
            {
                AnimateCubeDisappear(cube);
                return;
            }

            //Получаем RectTransform дыры (именно dropZoneRect, не GameObject!)
            RectTransform holeRect = _holeDropZone.GetDropZoneRect();
            if (holeRect == null)
            {
                Debug.LogWarning("HoleDropZone has no dropZoneRect!");
                AnimateCubeDisappear(cube);
                return;
            }

            //Конвертируем позицию центра дыры в координаты dragLayer
            Vector3[] holeCorners = new Vector3[4];
            holeRect.GetWorldCorners(holeCorners);
            Vector3 holeCenter = (holeCorners[0] + holeCorners[2]) / 2f;

            Vector2 holeScreenPos = RectTransformUtility.WorldToScreenPoint(_eventCamera, holeCenter);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragLayer,
                    holeScreenPos,
                    _eventCamera,
                    out Vector2 holeLocalPos
                ))
            {
                Debug.LogWarning("Failed to convert hole position to dragLayer local space!");
                AnimateCubeDisappear(cube);
                return;
            }

            //Анимация: куб летит к центру дыры, вращается и уменьшается
            Sequence sequence = DOTween.Sequence();

            sequence.Append(cube.RectTransform.DOAnchorPos(holeLocalPos, 0.4f).SetEase(Ease.InQuad));
            sequence.Join(cube.RectTransform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack));
            sequence.Join(cube.RectTransform.DORotate(new Vector3(0, 0, 360), 0.4f, RotateMode.FastBeyond360).SetEase(Ease.InQuad));

            sequence.SetTarget(cube.RectTransform);
            sequence.SetAutoKill(true);
            sequence.OnComplete(() =>
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
            _isFromTower = false;
            _towerIndex = -1;
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _isDragging = false;
            _eventCamera = eventData.pressEventCamera;

            //Кэшируем точку клика для определения направления движения
            _pointerDownScreenPos = eventData.position;

            //Определяем откуда тащим куб: из башни или из нижней панели
            TowerDropZone tower = _source.GetComponentInParent<TowerDropZone>();
            if (tower != null)
            {
                _isFromTower = true;
                _towerIndex = tower.GetCubeIndex(_source);
            }
            else
            {
                _isFromTower = false;
                _towerIndex = -1;
            }

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
            //Кубы из башни ВСЕГДА драгаются, скролл запрещен
            if (_isFromTower)
            {
                _isScrolling = false;
                StartDrag();
                return;
            }

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
    }
}
