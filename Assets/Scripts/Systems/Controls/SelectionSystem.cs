using Systems.Utils;
using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Tags.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using static Systems.Utils.ClickEventUtils;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(ControlSystemGroup))]
    public class SelectionSystem : ConsumeSingleEventSystemBase<LeftClickEvent>
    {
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;
        private float3 _endPos;
        private BuildPhysicsWorld _physicsSystem;
        private PhysicsWorld _physicsWorld;
        private NativeList<ColliderCastHit> _selectionList;
        private float3 _startPos;

        protected override void OnStartRunning()
        {
            _physicsSystem = World.GetExistingSystem<BuildPhysicsWorld>();
            _physicsWorld = _physicsSystem.PhysicsWorld;
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
            _selectionList = new NativeList<ColliderCastHit>(Allocator.Persistent);
        }

        protected override void OnStopRunning() { _selectionList.Dispose(); }

        protected override void OnEvent(LeftClickEvent e)
        {
            switch (e.State)
            {
                case (ushort) ClickState.Down:
                    _startPos = e.Hit.Position;
                    _endPos = e.Hit.Position;
                    ResetSelection();
                    break;

                case (ushort) ClickState.Up:
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
            if (_selectionList.IsEmpty) return;

            var unselectJob = new SetSelectionJob
                              {
                                  Hits = _selectionList,
                                  Select = false,
                                  ParallelWriter = _ecbSystem.CreateCommandBuffer()
                                                             .AsParallelWriter()
                              }.Schedule(_selectionList, 1);

            var clearSelectionJob =
                new ClearSelectionJob {Hits = _selectionList}.Schedule(unselectJob);

            _ecbSystem.AddJobHandleForProducer(
                JobHandle.CombineDependencies(Dependency, unselectJob, clearSelectionJob)
            );
        }

        private void OnSingleSelection(Entity entity)
        {
            _ecbSystem.CreateCommandBuffer()
                      .AddComponent<SelectedTag>(entity);
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
            var collider = RaycastUtils.GetBoxCollider(
                upperBound,
                lowerBound,
                PhysicsUtils.GridFilter
            );


            var boxCastJob = new RaycastUtils.ColliderCastJob
                             {
                                 PhysicsWorld = _physicsWorld,
                                 Hits = _selectionList,
                                 Origin = upperBound / 2f + lowerBound / 2f,
                                 Collider = collider
                             }.Schedule(_physicsSystem.GetOutputDependency());
            var boxCastDependencies = JobHandle.CombineDependencies(
                Dependency,
                _physicsSystem.GetOutputDependency(),
                boxCastJob
            );

            var selectJob = new SetSelectionJob
                            {
                                Hits = _selectionList,
                                Select = true,
                                ParallelWriter = _ecbSystem.CreateCommandBuffer()
                                                           .AsParallelWriter()
                            }.Schedule(_selectionList, 1, boxCastDependencies);

            _ecbSystem.AddJobHandleForProducer(
                JobHandle.CombineDependencies(boxCastDependencies, selectJob)
            );
        }

        [BurstCompile]
        private struct SetSelectionJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ColliderCastHit> Hits;
            [ReadOnly] public bool Select;
            [WriteOnly] public EntityCommandBuffer.ParallelWriter ParallelWriter;

            public void Execute(int index)
            {
                if (Select)
                    ParallelWriter.AddComponent<SelectedTag>(
                        index,
                        Hits[index]
                            .Entity
                    );
                else
                    ParallelWriter.RemoveComponent<SelectedTag>(
                        index,
                        Hits[index]
                            .Entity
                    );
            }
        }

        [BurstCompile]
        private struct ClearSelectionJob : IJob
        {
            [WriteOnly] public NativeList<ColliderCastHit> Hits;

            public void Execute() { Hits.Clear(); }
        }
    }
}