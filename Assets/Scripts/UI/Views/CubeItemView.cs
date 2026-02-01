using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Views
{
    [RequireComponent(typeof(Image))]
    public class CubeItemView : MonoBehaviour
    {
        [SerializeField] private Image image;

        public RectTransform RectTransform { get; private set; }

        private void Awake()
        {
            RectTransform = (RectTransform)transform;

            if (image == null)
            {
                image = GetComponent<Image>();
            }
        }

        public void SetColor(Color color)
        {
            image.color = color;
        }
    }
}