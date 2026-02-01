using UnityEngine;

namespace Core.Domain
{
    public readonly struct CubeDescriptor
    {
        public readonly Color Color;

        public CubeDescriptor(Color color)
        {
            Color = color;
        }
    }
}
