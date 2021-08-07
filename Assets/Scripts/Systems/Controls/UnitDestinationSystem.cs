using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Movement;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(ControlSystemGroup))]
    public class UnitDestinationSystem : ConsumeSingleEventSystemBase<RightClickEvent>
    {
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;
        private PhysicsWorld _physicsWorld;

        protected override void OnStartRunning()
        {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                                 .PhysicsWorld;
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        protected override void OnEvent(RightClickEvent e)
        {
            NativeArray<RigidBody> bodies = _physicsWorld.Bodies;

            Entities.WithAll<UnitComponent>()
                    .WithReadOnly(bodies)
                    .ForEach(
                            (ref UnitComponent actor) =>
                            {
                                actor.Destination = bodies[e.Hit.RigidBodyIndex]
                                                    .WorldFromBody.pos;
                            }
                        )
                    .ScheduleParallel();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}