using BovineLabs.Event.Systems;
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
    public class RightClickEventProducerSystem : SystemBase
    {
        private const float MaxRayDist = 1000;

        private EventSystem _eventSystem;

        private Camera _mainCamera;
        private PhysicsWorld _physicsWorld;

        protected override void OnStartRunning() {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                                 .PhysicsWorld;
            _eventSystem = World.GetExistingSystem<EventSystem>();
            _mainCamera = Camera.main;
        }

        protected override void OnUpdate() {
            if (Input.GetMouseButtonDown(1))
            {
                var job = new RaycastJob{
                    RaycastInput = RaycastInputFromRay(
                        _mainCamera.ScreenPointToRay(Input.mousePosition),
                        SpecialCollisionFilter.Grid
                    ),
                    PhysicsWorld = _physicsWorld
                };
                job.Execute();

                if (!job.HasHit) return;

                var writer = _eventSystem.CreateEventWriter<RightClickEvent>();
                writer.Write(
                    new RightClickEvent{
                        Entity = job.Hit.Entity,
                        Position = _physicsWorld.Bodies[job.Hit.RigidBodyIndex]
                                                .WorldFromBody.pos
                    }
                );
                _eventSystem.AddJobHandleForProducer<RightClickEvent>(Dependency);
            }
        }

        private static RaycastInput RaycastInputFromRay(
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