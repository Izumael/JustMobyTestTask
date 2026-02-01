using System;
using Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Views
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public class CubeItemView : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private CanvasGroup canvasGroup;

        public RectTransform RectTransform { get; private set; }
        public CubeDescriptor Descriptor { get; private set; }

        private void Awake()
        {
            RectTransform = (RectTransform)transform;

            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public void Bind(CubeDescriptor descriptor)
        {
            Descriptor = descriptor;
            image.color = descriptor.Color;
        }

        public void SetBlocksRaycasts(bool newValue)
        {
            canvasGroup.blocksRaycasts = newValue;
        }
    }
}