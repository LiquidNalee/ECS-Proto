using System.Diagnostics.CodeAnalysis;
using Systems.Utils;
using BovineLabs.Event.Systems;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using static Systems.Utils.ClickEventUtils;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Systems.Events
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UpdateInGroup(typeof(EventProducerSystemGroup))]
    public abstract class ClickEventSystem<TEvent> : SystemBase
        where TEvent : struct, IComponentData
    {
        protected int _buttonID;
        private EventSystem _eventSystem;
        protected CollisionFilter _filter;

        private Camera _mainCamera;
        protected PhysicsWorld _physicsWorld;
        private ClickState _state;

        protected override void OnStartRunning()
        {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                                 .PhysicsWorld;
            _eventSystem = World.GetExistingSystem<EventSystem>();
            _mainCamera = Camera.main;
            _state = ClickState.Null;
        }

        protected override void OnUpdate()
        {
            if (!UpdateState()) return;

            var rayInput = RaycastUtils.RaycastInputFromRay(
                _mainCamera.ScreenPointToRay(Input.mousePosition),
                _filter
            );

            var raycastJob = new RaycastUtils.SingleRaycastJob
                             {
                                 RaycastInput = rayInput, PhysicsWorld = _physicsWorld
                             };
            raycastJob.Execute();

            if (!raycastJob.HasHit) return;

            var writer = _eventSystem.CreateEventWriter<TEvent>();
            writer.Write(EventFromRaycastHit(raycastJob.Hit, _state));
            _eventSystem.AddJobHandleForProducer<TEvent>(Dependency);
        }

        private bool UpdateState()
        {
            if (_state == ClickState.Up) _state = ClickState.Null;

            if (Input.GetMouseButtonDown(_buttonID) && _state == ClickState.Null)
                _state = ClickState.Down;
            else if (Input.GetMouseButton(_buttonID))
                _state = ClickState.Hold;
            else if (Input.GetMouseButtonUp(_buttonID) && _state == ClickState.Hold)
                _state = ClickState.Up;

            return _state != ClickState.Null;
        }

        protected abstract TEvent EventFromRaycastHit(RaycastHit hit, ClickState state);
    }
}