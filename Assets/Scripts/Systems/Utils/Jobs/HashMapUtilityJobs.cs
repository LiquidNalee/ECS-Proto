using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Systems.Utils.Jobs
{
    namespace HashMapUtilityJobs
    {
        [BurstCompile]
        public struct GetUniqueMultHMapKeysJob<TKey, TValue> : IJob
            where TKey : struct, IEquatable<TKey> where TValue : struct
        {
            [ReadOnly]
            public NativeMultiHashMap<TKey, TValue> MultiHashMap;

            [WriteOnly]
            public NativeList<TKey> Keys;

            public unsafe void Execute()
            {
                var withDuplicates = MultiHashMap.GetKeyArray(Allocator.Temp);
                var uniqueCount = withDuplicates.Unique();
                Keys.AddRange(withDuplicates.GetUnsafeReadOnlyPtr(), uniqueCount);
                withDuplicates.Dispose();
            }
        }

        [BurstCompile]
        public struct GetHMapKeysJob<TKey, TValue> : IJob
            where TKey : struct, IEquatable<TKey> where TValue : struct
        {
            [ReadOnly]
            public NativeHashMap<TKey, TValue> HashMap;

            [WriteOnly]
            public NativeList<TKey> Keys;

            public void Execute()
            {
                var keyArray = HashMap.GetKeyArray(Allocator.Temp);
                Keys.AddRange(keyArray);
                keyArray.Dispose();
            }
        }
    }
}