using Systems.Events;
using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Tags.Selection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(ControlSystemGroup))]
    public class SelectionSystem : ConsumeSingleEventSystemBase<LeftClickEvent>
    {
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;
        private float3 _startPos;

        protected override void OnStartRunning()
        {
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        protected override void OnEvent(LeftClickEvent e)
        {
            switch (e.State)
            {
                case (ushort) ClickState.Down:
                    _startPos = e.Position;
                    break;

                case (ushort) ClickState.Up:
                    ResetSelection();
                    // ReSharper disable once InconsistentNaming
                    var _endPos = e.Position;

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
            var upperBound = new float3(math.max(startPos.x, endPos.x), 0f,
                math.max(startPos.z, endPos.z));
            var lowerBound = new float3(math.min(startPos.x, endPos.x), 0f,
                math.min(startPos.z, endPos.z));
            var parallelWriter = _ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.WithAll<SelectableTag, LocalToWorld>()
                .ForEach(
                    (
                        Entity entity,
                        int entityInQueryIndex,
                        in LocalToWorld ltw
                    ) =>
                    {
                        var pos = ltw.Position;

                        if (pos.x >= lowerBound.x && pos.x <= upperBound.x &&
                            pos.z >= lowerBound.z && pos.z <= upperBound.z)
                            parallelWriter.AddComponent<SelectedTag>(
                                entityInQueryIndex,
                                entity
                            );
                    }
                )
                .ScheduleParallel();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}