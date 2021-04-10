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
    public enum ClickState : ushort
    {
        Null = 0,
        Down = 1,
        Hold = 2,
        Up = 3
    }

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
            UpdateState();
            if (_state == ClickState.Null) return;

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

        private void UpdateState()
        {
            if (_state == ClickState.Up) _state = ClickState.Null;

            if (Input.GetMouseButtonDown(_buttonID))
            {
                if (_state != ClickState.Null) return;
                _state = ClickState.Down;
            }
            else if (Input.GetMouseButton(_buttonID)) { _state = ClickState.Hold; }
            else if (Input.GetMouseButtonUp(_buttonID))
            {
                if (_state != ClickState.Hold) return;
                _state = ClickState.Up;
            }
        }

        protected abstract TEvent EventFromRaycastHit(RaycastHit hit, ClickState state);

        protected enum ButtonID
        {
            Left = 0,
            Right = 1
        }
    }
}