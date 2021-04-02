using Systems.Utils;
using BovineLabs.Event.Systems;
using Components.Controls;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

namespace Systems.Events
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
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
            if (!Input.GetMouseButtonDown(1)) return;

            var rayInput = RaycastUtils.RaycastInputFromRay(
                _mainCamera.ScreenPointToRay(Input.mousePosition),
                PhysicsUtils.GridFilter
            );

            var raycastJob = new RaycastUtils.SingleRaycastJob{
                RaycastInput = rayInput,
                PhysicsWorld = _physicsWorld
            };
            raycastJob.Execute();

            if (!raycastJob.HasHit) return;

            var writer = _eventSystem.CreateEventWriter<RightClickEvent>();
            writer.Write(
                new RightClickEvent{
                    Entity = raycastJob.Hit.Entity,
                    Position = _physicsWorld.Bodies[raycastJob.Hit.RigidBodyIndex]
                                            .WorldFromBody.pos
                }
            );
            _eventSystem.AddJobHandleForProducer<RightClickEvent>(Dependency);
        }
    }
}