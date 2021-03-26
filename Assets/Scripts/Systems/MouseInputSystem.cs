using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Ray = UnityEngine.Ray;

namespace Systems
{
    public struct SpecialCollisionFilter
    {
        public enum CollisionLayer
        {
            Grid = 1 << 0,
        }

        public static CollisionFilter Grid => new CollisionFilter{
            BelongsTo = ~0u,
            CollidesWith = (uint) CollisionLayer.Grid,
            GroupIndex = 0
        };
    }

    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public class MouseInputSystem : SystemBase
    {
        private const float MaxRayDist = 1000;
        private Camera _mainCamera;
        private PhysicsWorld _physicsWorld;


        protected override void OnStartRunning() {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>().PhysicsWorld;
            _mainCamera = Camera.main;
        }

        protected override void OnUpdate() {
            if (Input.GetMouseButtonDown(1))
            {
                var raycastInput = RaycastInputFromRay(
                    _mainCamera.ScreenPointToRay(Input.mousePosition),
                    SpecialCollisionFilter.Grid
                );

                if (!_physicsWorld.CastRay(raycastInput, out var hit)) return;
                var entityPos = _physicsWorld.Bodies[hit.RigidBodyIndex]
                                             .WorldFromBody.pos;
                Debug.Log(hit.Entity + ":" + hit.Position + " - " + entityPos);
            }
        }

        private RaycastInput RaycastInputFromRay(
            Ray ray,
            CollisionFilter filter
        ) {
            return new RaycastInput{
                Start = ray.origin,
                End = ray.origin + ray.direction * MaxRayDist,
                Filter = filter
            };
        }
    }
}