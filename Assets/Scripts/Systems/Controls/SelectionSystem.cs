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

            JobHandle unselectJob = new SetSelectionJob
                                    {
                                        selectionList = _selectionList,
                                        select = false,
                                        parallelWriter = _ecbSystem.CreateCommandBuffer()
                                            .AsParallelWriter()
                                    }.Schedule(_selectionList, 1);

            JobHandle clearSelectionJob =
                new ClearSelectionJob {selectionList = _selectionList}.Schedule(unselectJob);

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
            BlobAssetReference<Collider> collider = RaycastUtils.GetBoxCollider(
                    upperBound,
                    lowerBound,
                    new CollisionFilter
                    {
                        BelongsTo = ~0u, CollidesWith = (uint) CollisionLayer.Grid, GroupIndex = 0
                    }
                );
            var maxNewElements = _selectionList.Length * 2 + 1;

            var hits = new NativeList<ColliderCastHit>(Allocator.TempJob);
            var hitEntities = new NativeList<Entity>(Allocator.TempJob);
            var entitiesToAdd =
                new NativeList<Entity>(maxNewElements, Allocator.TempJob) {Length = maxNewElements};
            var entitiesToRemove =
                new NativeList<Entity>(maxNewElements, Allocator.TempJob) {Length = maxNewElements};
            var totalEntities = new NativeList<Entity>(Allocator.TempJob);

            JobHandle boxCastJob = new RaycastUtils.ColliderCastJob
                                   {
                                       physicsWorld = _physicsWorld,
                                       hits = hits,
                                       origin = upperBound / 2f + lowerBound / 2f,
                                       collider = collider
                                   }.Schedule(_physicsSystem.GetOutputDependency());
            var boxCastDep = JobHandle.CombineDependencies(
                    Dependency,
                    _physicsSystem.GetOutputDependency(),
                    boxCastJob
                );

            JobHandle convertJob = new ConvertHitsToEntitiesJob
                                   {
                                       hits = hits,
                                       hitEntities = hitEntities,
                                       selectionList = _selectionList,
                                       totalEntities = totalEntities
                                   }.Schedule(boxCastDep);
            var convertDep = JobHandle.CombineDependencies(boxCastDep, convertJob);
            hits.Dispose(convertDep);

            JobHandle getSelectionDiffJob = new GetSelectionDiffJob
                                            {
                                                totalEntities = totalEntities,
                                                selectionList = _selectionList,
                                                hitEntities = hitEntities,
                                                entitiesToAdd = entitiesToAdd,
                                                entitiesToRemove = entitiesToRemove
                                            }.Schedule(totalEntities, 1, convertDep);
            var getSelectionDiffDep = JobHandle.CombineDependencies(
                    convertDep,
                    getSelectionDiffJob
                );
            hitEntities.Dispose(getSelectionDiffDep);
            totalEntities.Dispose(getSelectionDiffDep);

            JobHandle selectJob = new SetSelectionJob
                                  {
                                      selectionList = entitiesToAdd,
                                      select = true,
                                      parallelWriter = _ecbSystem.CreateCommandBuffer()
                                                                 .AsParallelWriter()
                                  }.Schedule(entitiesToAdd, 1, getSelectionDiffDep);
            JobHandle unselectJob = new SetSelectionJob
                                    {
                                        selectionList = entitiesToRemove,
                                        select = false,
                                        parallelWriter = _ecbSystem.CreateCommandBuffer()
                                            .AsParallelWriter()
                                    }.Schedule(entitiesToRemove, 1, getSelectionDiffDep);
            var selectionJobDep = JobHandle.CombineDependencies(
                    getSelectionDiffDep,
                    selectJob,
                    unselectJob
                );

            JobHandle updateSelectionJob = new UpdateSelectionJob
                                           {
                                               selectionList = _selectionList,
                                               entitiesToAdd = entitiesToAdd,
                                               entitiesToRemove = entitiesToRemove
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
            public NativeList<Entity> selectionList;
            [ReadOnly]
            public bool select;
            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter parallelWriter;

            public void Execute(int index)
            {
                Entity entity = selectionList[index];
                if (entity == Entity.Null) return;

                if (select) parallelWriter.AddComponent<SelectedTag>(index, entity);
                else parallelWriter.RemoveComponent<SelectedTag>(index, entity);
            }
        }

        [BurstCompile]
        private struct GetSelectionDiffJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> totalEntities;
            [ReadOnly]
            public NativeList<Entity> selectionList;
            [ReadOnly]
            public NativeList<Entity> hitEntities;

            [WriteOnly]
            public NativeArray<Entity> entitiesToAdd;
            [WriteOnly]
            public NativeArray<Entity> entitiesToRemove;

            public void Execute(int index)
            {
                Entity entity = totalEntities[index];

                if (hitEntities.Contains(entity) && !selectionList.Contains(entity))
                    entitiesToAdd[index] = entity;
                else if (!hitEntities.Contains(entity) && selectionList.Contains(entity))
                    entitiesToRemove[index] = entity;
            }
        }

        [BurstCompile]
        private struct ConvertHitsToEntitiesJob : IJob
        {
            [ReadOnly]
            public NativeList<ColliderCastHit> hits;
            [ReadOnly]
            public NativeList<Entity> selectionList;
            [WriteOnly]
            public NativeList<Entity> hitEntities;
            [WriteOnly]
            public NativeList<Entity> totalEntities;

            public void Execute()
            {
                foreach (ColliderCastHit hit in hits)
                {
                    hitEntities.Add(hit.Entity);
                    totalEntities.Add(hit.Entity);
                }

                totalEntities.AddRange(selectionList.AsArray());
            }
        }

        [BurstCompile]
        private struct UpdateSelectionJob : IJob
        {
            public NativeList<Entity> selectionList;
            [ReadOnly]
            public NativeArray<Entity> entitiesToAdd;
            [ReadOnly]
            public NativeArray<Entity> entitiesToRemove;

            public void Execute()
            {
                foreach (Entity entity in entitiesToRemove)
                    if (entity != Entity.Null)
                        selectionList.RemoveAt(selectionList.IndexOf(entity));
                foreach (Entity entity in entitiesToAdd)
                    if (entity != Entity.Null)
                        selectionList.Add(entity);
            }
        }

        [BurstCompile]
        private struct ClearSelectionJob : IJob
        {
            [WriteOnly]
            public NativeList<Entity> selectionList;

            public void Execute() { selectionList.Clear(); }
        }
    }
}