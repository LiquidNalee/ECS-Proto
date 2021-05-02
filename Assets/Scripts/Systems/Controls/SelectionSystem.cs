using Systems.Utils;
using Systems.Utils.Physics;
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
        private JobHandle _selectionJobHandle;
        private NativeList<Entity> _selectionList;
        private float3 _startPos;

        protected override void OnStartRunning()
        {
            _physicsSystem = World.GetExistingSystem<BuildPhysicsWorld>();
            _physicsWorld = _physicsSystem.PhysicsWorld;
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
            _selectionList = new NativeList<Entity>(Allocator.Persistent);
            _selectionJobHandle = default;
        }

        protected override void OnStopRunning() { _selectionList.Dispose(); }

        protected override void OnEvent(LeftClickEvent e)
        {
            _selectionJobHandle.Complete();
            _endPos = e.Hit.Position;

            switch (e.State)
            {
                case (ushort) ClickState.Down:
                    _startPos = e.Hit.Position;
                    ResetSelection();
                    break;

                case (ushort) ClickState.Hold:
                    if (math.distance(_startPos, _endPos) > .2f)
                        OnWideSelection(_startPos, _endPos);
                    break;

                case (ushort) ClickState.Up:
                    if (math.distance(_startPos, _endPos) <= .2f) OnSingleSelection(e.Entity);
                    break;
            }

            _ecbSystem.AddJobHandleForProducer(_selectionJobHandle);
        }

        private void ResetSelection()
        {
            if (_selectionList.IsEmpty) return;

            var unselectJob = new SetSelectionJob
                              {
                                  SelectionList = _selectionList,
                                  Select = false,
                                  ParallelWriter = _ecbSystem.CreateCommandBuffer()
                                                             .AsParallelWriter()
                              }.Schedule(_selectionList, 1);

            var clearSelectionJob =
                new ClearSelectionJob {SelectionList = _selectionList}.Schedule(unselectJob);

            _selectionJobHandle = JobHandle.CombineDependencies(
                    Dependency,
                    unselectJob,
                    clearSelectionJob
                );
        }

        private void OnSingleSelection(Entity entity)
        {
            _selectionList.Add(entity);

            _ecbSystem.CreateCommandBuffer()
                      .AddComponent<SelectedTag>(entity);

            _selectionJobHandle = Dependency;
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
                    new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = (uint) CollisionLayer.Grid,
                        GroupIndex = 0
                    }
                );
            var maxNewElements = _selectionList.Length * 2 + 1;

            var hits = new NativeList<ColliderCastHit>(Allocator.TempJob);
            var hitEntities = new NativeList<Entity>(Allocator.TempJob);
            var entitiesToAdd =
                new NativeList<Entity>(maxNewElements, Allocator.TempJob)
                {
                    Length = maxNewElements
                };
            var entitiesToRemove =
                new NativeList<Entity>(maxNewElements, Allocator.TempJob)
                {
                    Length = maxNewElements
                };
            var totalEntities = new NativeList<Entity>(Allocator.TempJob);

            var boxCastJob = new RaycastUtils.ColliderCastJob
                             {
                                 PhysicsWorld = _physicsWorld,
                                 Hits = hits,
                                 Origin = upperBound / 2f + lowerBound / 2f,
                                 Collider = collider
                             }.Schedule(_physicsSystem.GetOutputDependency());
            var boxCastDep = JobHandle.CombineDependencies(
                    Dependency,
                    _physicsSystem.GetOutputDependency(),
                    boxCastJob
                );

            var convertJob = new ConvertHitsToEntitiesJob
                             {
                                 Hits = hits,
                                 HitEntities = hitEntities,
                                 SelectionList = _selectionList,
                                 TotalEntities = totalEntities
                             }.Schedule(boxCastDep);
            var convertDep = JobHandle.CombineDependencies(boxCastDep, convertJob);
            hits.Dispose(convertDep);

            var getSelectionDiffJob = new GetSelectionDiffJob
                                      {
                                          TotalEntities = totalEntities,
                                          SelectionList = _selectionList,
                                          HitEntities = hitEntities,
                                          EntitiesToAdd = entitiesToAdd,
                                          EntitiesToRemove = entitiesToRemove
                                      }.Schedule(totalEntities, 1, convertDep);
            var getSelectionDiffDep = JobHandle.CombineDependencies(
                    convertDep,
                    getSelectionDiffJob
                );
            hitEntities.Dispose(getSelectionDiffDep);
            totalEntities.Dispose(getSelectionDiffDep);

            var selectJob = new SetSelectionJob
                            {
                                SelectionList = entitiesToAdd,
                                Select = true,
                                ParallelWriter = _ecbSystem.CreateCommandBuffer()
                                                           .AsParallelWriter()
                            }.Schedule(entitiesToAdd, 1, getSelectionDiffDep);
            var unselectJob = new SetSelectionJob
                              {
                                  SelectionList = entitiesToRemove,
                                  Select = false,
                                  ParallelWriter = _ecbSystem.CreateCommandBuffer()
                                                             .AsParallelWriter()
                              }.Schedule(entitiesToRemove, 1, getSelectionDiffDep);
            var selectionJobDep = JobHandle.CombineDependencies(
                    getSelectionDiffDep,
                    selectJob,
                    unselectJob
                );

            var updateSelectionJob = new UpdateSelectionJob
                                     {
                                         SelectionList = _selectionList,
                                         EntitiesToAdd = entitiesToAdd,
                                         EntitiesToRemove = entitiesToRemove
                                     }.Schedule(selectionJobDep);
            var updateSelectionDep = JobHandle.CombineDependencies(
                    selectionJobDep,
                    updateSelectionJob
                );
            entitiesToAdd.Dispose(updateSelectionDep);
            entitiesToRemove.Dispose(updateSelectionDep);

            _selectionJobHandle = updateSelectionDep;
        }


        [BurstCompile]
        private struct SetSelectionJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> SelectionList;
            [ReadOnly]
            public bool Select;
            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter ParallelWriter;

            public void Execute(int index)
            {
                var entity = SelectionList[index];
                if (entity == Entity.Null) return;

                if (Select) ParallelWriter.AddComponent<SelectedTag>(index, entity);
                else ParallelWriter.RemoveComponent<SelectedTag>(index, entity);
            }
        }

        [BurstCompile]
        private struct GetSelectionDiffJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> TotalEntities;
            [ReadOnly]
            public NativeList<Entity> SelectionList;
            [ReadOnly]
            public NativeList<Entity> HitEntities;

            [WriteOnly]
            public NativeArray<Entity> EntitiesToAdd;
            [WriteOnly]
            public NativeArray<Entity> EntitiesToRemove;

            public void Execute(int index)
            {
                var entity = TotalEntities[index];

                if (HitEntities.Contains(entity) && !SelectionList.Contains(entity))
                    EntitiesToAdd[index] = entity;
                else if (!HitEntities.Contains(entity) && SelectionList.Contains(entity))
                    EntitiesToRemove[index] = entity;
            }
        }

        [BurstCompile]
        private struct ConvertHitsToEntitiesJob : IJob
        {
            [ReadOnly]
            public NativeList<ColliderCastHit> Hits;
            [ReadOnly]
            public NativeList<Entity> SelectionList;
            [WriteOnly]
            public NativeList<Entity> HitEntities;
            [WriteOnly]
            public NativeList<Entity> TotalEntities;

            public void Execute()
            {
                foreach (var hit in Hits)
                {
                    HitEntities.Add(hit.Entity);
                    TotalEntities.Add(hit.Entity);
                }

                TotalEntities.AddRange(SelectionList.AsArray());
            }
        }

        [BurstCompile]
        private struct UpdateSelectionJob : IJob
        {
            public NativeList<Entity> SelectionList;
            [ReadOnly]
            public NativeArray<Entity> EntitiesToAdd;
            [ReadOnly]
            public NativeArray<Entity> EntitiesToRemove;

            public void Execute()
            {
                foreach (var entity in EntitiesToRemove)
                    if (entity != Entity.Null)
                        SelectionList.RemoveAt(SelectionList.IndexOf(entity));
                foreach (var entity in EntitiesToAdd)
                    if (entity != Entity.Null)
                        SelectionList.Add(entity);
            }
        }

        [BurstCompile]
        private struct ClearSelectionJob : IJob
        {
            [WriteOnly]
            public NativeList<Entity> SelectionList;

            public void Execute() { SelectionList.Clear(); }
        }
    }
}