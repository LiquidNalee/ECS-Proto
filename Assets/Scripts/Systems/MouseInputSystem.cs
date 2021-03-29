using Components.Inputs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Ray = UnityEngine.Ray;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Systems
{
    public struct SpecialCollisionFilter
    {
        public enum CollisionLayer
        {
            Grid = 1 << 0
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
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                                 .PhysicsWorld;
            _mainCamera = Camera.main;
        }

        protected override void OnUpdate() {
            if (Input.GetMouseButtonDown(1))
            {
                var job = new RaycastJob{
                    raycastInput = RaycastInputFromRay(
                        _mainCamera.ScreenPointToRay(Input.mousePosition),
                        SpecialCollisionFilter.Grid
                    ),
                    physicsWorld = _physicsWorld
                };
                job.Execute();

                if (!job.hasHit) return;
                /*
                var entityPos = _physicsWorld.Bodies[job.hit.RigidBodyIndex]
                                             .WorldFromBody.pos;
                                             */
                EntityManager.AddComponentData(
                    job.hit.Entity,
                    new RightClickEvent()
                );
            }
            else if (Input.GetMouseButtonDown(0))
            {}
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

        [BurstCompile]
        private struct RaycastJob : IJob
        {
            [ReadOnly] public PhysicsWorld physicsWorld;
            [ReadOnly] public RaycastInput raycastInput;

            [WriteOnly] public RaycastHit hit;
            [WriteOnly] public bool hasHit;

            public void Execute() {
                hit = new RaycastHit();
                hasHit = physicsWorld.CastRay(raycastInput, out hit);
            }
        }
    }
}