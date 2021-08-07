using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Ray = UnityEngine.Ray;

namespace Systems.Utils
{
    public static class RaycastUtils
    {
        private const float MaxRayDist = 1000;

        public static RaycastInput RaycastInputFromRay(Ray ray,
                                                       CollisionFilter filter,
                                                       float maxRayDist = MaxRayDist)
        {
            return new RaycastInput
                   {
                       Start = ray.origin,
                       End = ray.origin + ray.direction * maxRayDist,
                       Filter = filter
                   };
        }

        public static BlobAssetReference<Collider> GetBoxCollider(float3 upperBound,
            float3 lowerBound,
            CollisionFilter filter = default)
        {
            return BoxCollider.Create(
                    new BoxGeometry
                    {
                        Size = new float3(
                                upperBound.x - lowerBound.x,
                                upperBound.y - lowerBound.y,
                                lowerBound.z - upperBound.z
                            ),
                        Center = float3.zero,
                        Orientation = quaternion.identity,
                        BevelRadius = 0f
                    },
                    filter
                );
        }

        [BurstCompile]
        public struct SingleRaycastJob : IJob
        {
            [ReadOnly]
            public PhysicsWorld physicsWorld;
            [ReadOnly]
            public RaycastInput raycastInput;

            [WriteOnly]
            public RaycastHit hit;
            [WriteOnly]
            public bool hasHit;

            public void Execute()
            {
                hit = new RaycastHit();
                hasHit = physicsWorld.CastRay(raycastInput, out hit);
            }
        }

        [BurstCompile]
        public unsafe struct ColliderCastJob : IJob
        {
            [ReadOnly]
            public PhysicsWorld physicsWorld;
            [ReadOnly]
            public float3 origin;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public BlobAssetReference<Collider> collider;

            public NativeList<ColliderCastHit> hits;
            [WriteOnly]
            public bool hasHit;

            public void Execute()
            {
                var colliderCastInput = new ColliderCastInput
                                        {
                                            Collider = (Collider*) collider.GetUnsafePtr(),
                                            Start = origin + new float3(0f, 1f, 0f),
                                            End = origin,
                                            Orientation = quaternion.identity
                                        };

                if (!hits.IsCreated) hits = new NativeList<ColliderCastHit>(Allocator.TempJob);
                hasHit = physicsWorld.CastCollider(colliderCastInput, ref hits);
            }
        }
    }
}