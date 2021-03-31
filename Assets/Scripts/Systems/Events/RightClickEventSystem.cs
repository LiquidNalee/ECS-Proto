using Systems.Utils;
using BovineLabs.Event.Systems;
using Components.Controls;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

namespace Systems.Events
{
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public class RightClickEventSystem : SystemBase
    {
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
                var job = new RaycastUtils.SingleRaycastJob{
                    RaycastInput = RaycastUtils.RaycastInputFromRay(
                        _mainCamera.ScreenPointToRay(Input.mousePosition),
                        PhysicsUtils.GridCollisionFilter
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
    }
}