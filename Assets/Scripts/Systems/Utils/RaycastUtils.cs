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
            public PhysicsWorld PhysicsWorld;
            [ReadOnly]
            public RaycastInput RaycastInput;

            [WriteOnly]
            public RaycastHit Hit;
            [WriteOnly]
            public bool HasHit;

            public void Execute()
            {
                Hit = new RaycastHit();
                HasHit = PhysicsWorld.CastRay(RaycastInput, out Hit);
            }
        }

        [BurstCompile]
        public unsafe struct ColliderCastJob : IJob
        {
            [ReadOnly]
            public PhysicsWorld PhysicsWorld;
            [ReadOnly]
            public float3 Origin;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public BlobAssetReference<Collider> Collider;

            public NativeList<ColliderCastHit> Hits;
            [WriteOnly]
            public bool HasHit;

            public void Execute()
            {
                var colliderCastInput = new ColliderCastInput
                                        {
                                            Collider = (Collider*) Collider.GetUnsafePtr(),
                                            Start = Origin + new float3(0f, 1f, 0f),
                                            End = Origin,
                                            Orientation = quaternion.identity
                                        };

                if (!Hits.IsCreated) Hits = new NativeList<ColliderCastHit>(Allocator.TempJob);
                HasHit = PhysicsWorld.CastCollider(colliderCastInput, ref Hits);
            }
        }
    }
}