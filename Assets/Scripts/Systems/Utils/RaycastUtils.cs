using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics;
using Ray = UnityEngine.Ray;

namespace Systems.Utils
{
    public static class RaycastUtils
    {
        private const float MaxRayDist = 1000;

        public static RaycastInput RaycastInputFromRay(
            Ray ray,
            CollisionFilter filter,
            float maxRayDist = MaxRayDist
        ) {
            return new RaycastInput{
                Start = ray.origin,
                End = ray.origin + ray.direction * maxRayDist,
                Filter = filter
            };
        }

        [BurstCompile]
        public struct SingleRaycastJob : IJob
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public RaycastInput RaycastInput;

            [WriteOnly] public RaycastHit Hit;
            [WriteOnly] public bool HasHit;

            public void Execute() {
                Hit = new RaycastHit();
                HasHit = PhysicsWorld.CastRay(RaycastInput, out Hit);
            }
        }
    }
}