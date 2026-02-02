using UI.Views;
using UnityEngine;

namespace UI.DropZones
{
    public class HoleDropZone : MonoBehaviour
    {
        [SerializeField]
        private TowerDropZone towerDropZone;

        [SerializeField]
        private RectTransform dropZoneRect;

        private void Awake()
        {
            if (dropZoneRect == null)
            {
                dropZoneRect = GetComponent<RectTransform>();
            }
        }

        public bool IsPointInsideOval(Vector2 screenPoint, Camera eventCamera)
        {
            if (dropZoneRect == null)
            {
                return false;
            }

            //Преобразуем экранную точку в локальные координаты RectTransform дыры
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dropZoneRect,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint
                )
            )
            {
                return false;
            }

            //Получаем размеры овала (радиусы эллипса)
            float radiusX = dropZoneRect.rect.width * 0.5f;
            float radiusY = dropZoneRect.rect.height * 0.5f;

            //Проверяем попадание точки в эллипс по формуле:
            //(x / radiusX)² + (y / radiusY)² <= 1
            float normalizedX = localPoint.x / radiusX;
            float normalizedY = localPoint.y / radiusY;

            return (normalizedX * normalizedX + normalizedY * normalizedY) <= 1f;
        }

        public bool TryRemoveCubeAt(int index, out CubeItemView removedCube)
        {
            if (towerDropZone == null)
            {
                removedCube = null;
                return false;
            }

            //Удаляем куб по индексу из башни
            removedCube = towerDropZone.RemoveCubeAt(index);
            return removedCube != null;
        }

        public RectTransform GetDropZoneRect()
        {
            return dropZoneRect;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (dropZoneRect == null)
            {
                dropZoneRect = GetComponent<RectTransform>();
            }

            if (dropZoneRect == null)
            {
                return;
            }

            //Отрисовываем овал в редакторе для визуализации зоны дропа
            Gizmos.color = Color.red;

            Vector3 center = dropZoneRect.position;
            float radiusX = dropZoneRect.rect.width * dropZoneRect.lossyScale.x * 0.5f;
            float radiusY = dropZoneRect.rect.height * dropZoneRect.lossyScale.y * 0.5f;

            int segments = 64;
            Vector3 previousPoint = center + new Vector3(radiusX, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 newPoint = center + new Vector3(
                    Mathf.Cos(angle) * radiusX,
                    Mathf.Sin(angle) * radiusY,
                    0
                );

                Gizmos.DrawLine(previousPoint, newPoint);
                previousPoint = newPoint;
            }
        }
#endif
    }
}
