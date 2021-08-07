using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Systems.Utils.Jobs
{
    namespace HashMapUtilityJobs
    {
        [BurstCompile]
        public struct GetUniqueMultHMapKeysJob<TKey, TValue> : IJob
            where TKey : struct, IEquatable<TKey>, IComparable<TKey>
            where TValue : struct
        {
            [ReadOnly]
            public NativeMultiHashMap<TKey, TValue> multiHashMap;

            [WriteOnly]
            public NativeList<TKey> keys;

            [BurstDiscard]
            private void Log(NativeArray<TKey> duplicates, NativeList<TKey> uniques)
            {
                var log = "Duplicates: ";
                for (var i = 0; i < duplicates.Length; ++i)
                    log += "[" + i + "] " + duplicates[i] + "; ";
                log += "\nUniques: ";
                for (var i = 0; i < uniques.Length; ++i) log += "[" + i + "] " + uniques[i] + "; ";
                Debug.Log(log);
            }

            public unsafe void Execute()
            {
                NativeArray<TKey> withDuplicates = multiHashMap.GetKeyArray(Allocator.Temp);
                withDuplicates.Sort();
                var uniqueCount = withDuplicates.Unique();
                keys.AddRange(withDuplicates.GetUnsafeReadOnlyPtr(), uniqueCount);
                withDuplicates.Dispose();
            }
        }

        [BurstCompile]
        public struct GetHMapKeysJob<TKey, TValue> : IJob
            where TKey : struct, IEquatable<TKey> where TValue : struct
        {
            [ReadOnly]
            public NativeHashMap<TKey, TValue> hashMap;

            [WriteOnly]
            public NativeList<TKey> keys;

            public void Execute()
            {
                NativeArray<TKey> keyArray = hashMap.GetKeyArray(Allocator.Temp);
                keys.AddRange(keyArray);
                keyArray.Dispose();
            }
        }
    }
}