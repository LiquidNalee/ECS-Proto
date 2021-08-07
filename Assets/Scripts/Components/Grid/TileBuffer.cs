using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Components.Grid
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "Unity.RedundantSerializeFieldAttribute")]
    public unsafe struct TileBuffer
    {
        [SerializeField]
        private readonly Entity E0;
        [SerializeField]
        private readonly Entity E1;
        [SerializeField]
        private readonly Entity E2;
        [SerializeField]
        private readonly Entity E3;
        [SerializeField]
        private readonly Entity E4;
        [SerializeField]
        private readonly Entity E5;

        public ref Entity this[int i]
        {
            get
            {
                RequireIndexInBounds(i);

                fixed (Entity* elements = &E0) { return ref elements[i]; }
            }
        }

        public static TileBuffer Empty
        {
            get
            {
                var tileBuffer = new TileBuffer();
                tileBuffer.Clear();
                return tileBuffer;
            }
        }

        public void Clear()
        {
            fixed (Entity* elements = &E0)
            {
                for (var i = 0; i < 6; ++i) elements[i] = Entity.Null;
            }
        }

        public TileBuffer Clone()
        {
            var tileBuffer = new TileBuffer();

            fixed (Entity* from = &E0)
            {
                for (var i = 0; i < 6; ++i) tileBuffer[i] = from[i];
            }

            return tileBuffer;
        }

        [BurstDiscard]
        private static void RequireIndexInBounds(int i)
        {
            if (i < 0 || i > 5) throw new InvalidOperationException("Index out of bounds: " + i);
        }
    }
}