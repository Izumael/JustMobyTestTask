using System.Collections.Generic;
using DG.Tweening;
using UI.Views;
using UnityEngine;

namespace UI.DropZones
{
    public class TowerDropZone : MonoBehaviour
    {
        private readonly List<CubeItemView> _stackedCubes = new List<CubeItemView>();

        [SerializeField]
        private float bottomOffset = 20f;

        [SerializeField]
        [Range(0f, 0.5f)]
        private float horizontalOffsetFactor = 0.3f;

        [SerializeField]
        private RectTransform dropZoneRect;

        public int CubeCount => _stackedCubes.Count;

        private void Awake()
        {
            if (dropZoneRect == null)
            {
                dropZoneRect = GetComponent<RectTransform>();
            }
        }

        private bool CanAddCube(CubeItemView cube)
        {
            if (_stackedCubes.Count == 0)
            {
                return true;
            }

            //Рассчитываем максимальную высоту башни динамически
            float maxHeight = dropZoneRect.rect.height - bottomOffset;

            //Проверяем, влезет ли ещё один куб по высоте
            float cubeHeight = cube.RectTransform.rect.height;
            float newStackHeight = bottomOffset + ((_stackedCubes.Count + 1) * cubeHeight);

            return newStackHeight <= maxHeight;
        }

        public bool AddCube(CubeItemView cube, bool destroyAfterAnimation = false)
        {
            if (!CanAddCube(cube))
            {
                return false;
            }

            //Сохраняем мировую позицию для корректного позиционирования первого куба
            Vector3 worldPosition = cube.transform.position;

            cube.transform.SetParent(dropZoneRect, false);

            //Устанавливаем якоря к нижнему краю родителя для роста башни снизу вверх
            cube.RectTransform.anchorMin = new Vector2(0.5f, 0f);
            cube.RectTransform.anchorMax = new Vector2(0.5f, 0f);
            cube.RectTransform.pivot = new Vector2(0.5f, 0f);

            if (_stackedCubes.Count > 0)
            {
                //Добавляем куб с рандомным горизонтальным смещением для эффекта неровной башни
                CubeItemView lastCube = _stackedCubes[^1];
                Rect cubeRect = cube.RectTransform.rect;

                float maxOffset = cubeRect.width * horizontalOffsetFactor;
                float horizontalOffset = Random.Range(-maxOffset, maxOffset);

                float newPosY = bottomOffset + (_stackedCubes.Count * cubeRect.height);
                cube.RectTransform.anchoredPosition = new Vector2(
                    lastCube.RectTransform.anchoredPosition.x + horizontalOffset,
                    newPosY
                );
            }
            else
            {
                //Первый куб: сохраняем X позицию от места дропа, Y = bottomOffset
                cube.transform.position = worldPosition;

                float currentX = cube.RectTransform.anchoredPosition.x;
                cube.RectTransform.anchoredPosition = new Vector2(currentX, bottomOffset);
            }

            _stackedCubes.Add(cube);

            AnimateCubeAddition(cube, destroyAfterAnimation);

            return true;
        }

        public int GetCubeIndex(CubeItemView cube)
        {
            return _stackedCubes.IndexOf(cube);
        }

        public CubeItemView RemoveCubeAt(int index)
        {
            if (index < 0 || index >= _stackedCubes.Count)
            {
                return null;
            }

            //Удаляем куб по индексу и анимируем опускание верхних
            CubeItemView removed = _stackedCubes[index];
            _stackedCubes.RemoveAt(index);

            //Все кубики выше опускаются вниз
            AnimateCubesAbove(index);

            return removed;
        }

        private void AnimateCubesAbove(int startIndex)
        {
            for (int i = startIndex; i < _stackedCubes.Count; i++)
            {
                CubeItemView cube = _stackedCubes[i];

                //Проверяем касание с нижним кубом
                if (i > 0)
                {
                    CubeItemView lowerCube = _stackedCubes[i - 1];

                    if (!IsTouching(cube, lowerCube))
                    {
                        //Не касается - удаляем этот и все кубики выше
                        RemoveAllCubesAbove(i);
                        return;
                    }
                }

                //Касается или это первый куб - анимируем опускание
                float cubeHeight = cube.RectTransform.rect.height;
                float newPosY = bottomOffset + (i * cubeHeight);

                cube.RectTransform.DOAnchorPosY(newPosY, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .SetTarget(cube.RectTransform)
                    .SetAutoKill(true);
            }
        }

        private bool IsTouching(CubeItemView upper, CubeItemView lower)
        {
            float upperX = upper.RectTransform.anchoredPosition.x;
            float lowerX = lower.RectTransform.anchoredPosition.x;

            float cubeWidth = upper.RectTransform.rect.width;

            //Касаются если расстояние между центрами меньше ширины куба
            float distance = Mathf.Abs(upperX - lowerX);
            return distance < cubeWidth;
        }

        private void RemoveAllCubesAbove(int startIndex)
        {
            //Удаляем все кубы начиная с startIndex с анимацией падения
            for (int i = _stackedCubes.Count - 1; i >= startIndex; i--)
            {
                CubeItemView cube = _stackedCubes[i];
                _stackedCubes.RemoveAt(i);

                //Анимация исчезновения
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
        }

        private void AnimateCubeAddition(CubeItemView cube, bool destroyAfterAnimation)
        {
            if (cube == null || cube.RectTransform == null)
            {
                return;
            }

            float targetY = cube.RectTransform.anchoredPosition.y;

            //Анимация "прыжка" куба: сначала подпрыгивает вверх, затем опускается на целевую позицию
            cube.RectTransform.DOAnchorPosY(targetY + 10, 0.5f)
                .SetEase(Ease.OutBack)
                .SetTarget(cube.RectTransform)
                .SetAutoKill(true)
                .OnComplete(() =>
                {
                    if (cube == null || cube.RectTransform == null)
                    {
                        return;
                    }

                    cube.RectTransform.DOAnchorPosY(targetY, 0.2f)
                        .SetEase(Ease.InOutQuad)
                        .SetTarget(cube.RectTransform)
                        .SetAutoKill(true)
                        .OnComplete(() =>
                        {
                            if (destroyAfterAnimation && cube != null && cube.gameObject != null)
                            {
                                Destroy(cube.gameObject);
                            }
                        });
                });
        }
    }
}
