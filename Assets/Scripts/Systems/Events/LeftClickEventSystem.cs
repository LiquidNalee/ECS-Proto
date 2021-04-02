using Systems.Utils;
using BovineLabs.Event.Systems;
using Components.Controls;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using static Systems.Utils.PhysicsUtils;

namespace Systems.Events
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public class LeftClickEventSystem : SystemBase
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
            if (!Input.GetMouseButtonDown(0)) return;

            var rayInput = RaycastUtils.RaycastInputFromRay(
                _mainCamera.ScreenPointToRay(Input.mousePosition),
                new CollisionFilter{
                    BelongsTo = ~0u,
                    CollidesWith =
                        (uint) (CollisionLayer.Unit | CollisionLayer.Grid),
                    GroupIndex = 0
                }
            );

            var raycastJob = new RaycastUtils.SingleRaycastJob{
                RaycastInput = rayInput,
                PhysicsWorld = _physicsWorld
            };
            raycastJob.Execute();

            if (!raycastJob.HasHit) return;

            var writer = _eventSystem.CreateEventWriter<LeftClickEvent>();
            writer.Write(
                new LeftClickEvent{
                    Entity = raycastJob.Hit.Entity
                }
            );
            _eventSystem.AddJobHandleForProducer<LeftClickEvent>(Dependency);
        }
    }
}