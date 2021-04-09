using Systems.Events;
using Systems.Utils;
using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Tags.Selection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(ControlSystemGroup))]
    public class SelectionSystem : ConsumeSingleEventSystemBase<LeftClickEvent>
    {
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;
        private float3 _endPos;
        private PhysicsWorld _physicsWorld;
        private float3 _startPos;

        protected override void OnStartRunning()
        {
            _physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>()
                .PhysicsWorld;
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        protected override void OnEvent(LeftClickEvent e)
        {
            switch (e.State)
            {
                case (ushort) ClickState.Down:
                    _startPos = e.Hit.Position;
                    break;

                case (ushort) ClickState.Up:
                    ResetSelection();
                    // ReSharper disable once InconsistentNaming
                    _endPos = e.Hit.Position;

                    if (math.distance(_startPos, _endPos) <= .2f)
                        OnSingleSelection(e.Entity);
                    else
                        OnWideSelection(_startPos, _endPos);
                    break;
            }
        }

        private void ResetSelection()
        {
            var parallelWriter = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            Entities.WithAll<SelectedTag>()
                .ForEach((Entity entity, int entityInQueryIndex) =>
                    parallelWriter.RemoveComponent<SelectedTag>(entityInQueryIndex, entity)
                )
                .ScheduleParallel();
            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private void OnSingleSelection(Entity entity)
        {
            if (EntityManager.HasComponent<SelectableTag>(entity))
                _ecbSystem.CreateCommandBuffer().AddComponent<SelectedTag>(entity);
        }

        private void OnWideSelection(float3 startPos, float3 endPos)
        {
            var upperBound = new float3(
                math.max(startPos.x, endPos.x),
                0f,
                math.min(startPos.z, endPos.z)
            );
            var lowerBound = new float3(
                math.min(startPos.x, endPos.x),
                0f,
                math.max(startPos.z, endPos.z)
            );
            var cmdBuffer = _ecbSystem.CreateCommandBuffer();

            var boxCastJob = new RaycastUtils.ColliderCastJob
            {
                PhysicsWorld = _physicsWorld,
                Origin = upperBound / 2f + lowerBound / 2f,
                Collider = RaycastUtils.GetBoxCollider(upperBound, lowerBound)
            };
            boxCastJob.Execute();

            foreach (var hit in boxCastJob.Hits)
                if (EntityManager.HasComponent<SelectableTag>(hit.Entity))
                    cmdBuffer.AddComponent<SelectedTag>(hit.Entity);

            boxCastJob.Hits.Dispose();
        }
    }
}