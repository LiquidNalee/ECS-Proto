using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;

namespace Systems.Grid.GridGenerationGroup.Utils
{
    public struct GridPosition : IEquatable<GridPosition>, IComparable<GridPosition>
    {
        private float3 _value;

        private GridPosition(float3 value)
        {
            for (var i = 0; i < 3; ++i)
                value[i] = (float) Math.Round(value[i], 5);
            _value = value;
        }

        public bool Equals(GridPosition other) { return _value.Equals(other._value); }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public int CompareTo(GridPosition other)
        {
            if (_value.x != other._value.x) return _value.x.CompareTo(other._value.x);
            if (_value.y != other._value.y) return _value.y.CompareTo(other._value.y);
            if (_value.z != other._value.z) return _value.z.CompareTo(other._value.z);

            return 0;
        }

        public override string ToString() { return _value.ToString(); }

        public static implicit operator GridPosition(float3 v) { return new GridPosition(v); }

        public static implicit operator float3(GridPosition gp) { return gp._value; }
    }
}