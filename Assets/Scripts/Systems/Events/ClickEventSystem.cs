using System.Diagnostics.CodeAnalysis;
using Systems.Utils;
using BovineLabs.Event.Systems;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Systems.Events
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public abstract class ClickEventSystem<TEvent> : SystemBase
        where TEvent : struct, IComponentData
    {
        protected int _buttonID;
        private EventSystem _eventSystem;
        protected CollisionFilter _filter;

        private Camera _mainCamera;
        protected PhysicsWorld _physicsWorld;

        protected override void OnStartRunning() {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                                 .PhysicsWorld;
            _eventSystem = World.GetExistingSystem<EventSystem>();
            _mainCamera = Camera.main;
        }

        protected override void OnUpdate() {
            if (!Input.GetMouseButtonDown(_buttonID)) return;

            var rayInput = RaycastUtils.RaycastInputFromRay(
                _mainCamera.ScreenPointToRay(Input.mousePosition),
                _filter
            );

            var raycastJob = new RaycastUtils.SingleRaycastJob{
                RaycastInput = rayInput,
                PhysicsWorld = _physicsWorld
            };
            raycastJob.Execute();

            if (!raycastJob.HasHit) return;

            var writer = _eventSystem.CreateEventWriter<TEvent>();
            writer.Write(EventFromRaycastHit(raycastJob.Hit));
            _eventSystem.AddJobHandleForProducer<TEvent>(Dependency);
        }

        protected abstract TEvent EventFromRaycastHit(RaycastHit hit);

        protected enum ButtonID
        {
            Left = 0,
            Right = 1
        }
    }
}