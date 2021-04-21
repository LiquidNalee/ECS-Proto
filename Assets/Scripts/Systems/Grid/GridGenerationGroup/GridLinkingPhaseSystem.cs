using Systems.Grid.GridGenerationGroup.Utils;
using Systems.Utils.Jobs.HashMapUtilityJobs;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Systems.Grid.GridGenerationGroup
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    [UpdateAfter(typeof(GridExpansionPhaseSystem))]
    public class GridLinkingPhaseSystem : SystemBase
    {
        private EndInitializationEntityCommandBufferSystem _ecbSystem;
        private EntityQuery _linkingTilesQuery;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();

            _linkingTilesQuery = GetEntityQuery(
                    ComponentType.ReadOnly<TileComponent>(),
                    ComponentType.ReadWrite<GridGenerationComponent>()
                );
            _linkingTilesQuery.SetSharedComponentFilter(
                    new GridGenerationComponent(GridGenerationPhase.Linking)
                );
        }

        protected override void OnUpdate()
        {
            if (_linkingTilesQuery.IsEmpty) return;

            #region ComputeMaxCounts

            var outerNodesMaxCount = _linkingTilesQuery.CalculateEntityCount();
            var centerNodesMaxCount = outerNodesMaxCount;
            var totalTilesMaxCount = centerNodesMaxCount + outerNodesMaxCount;

            #endregion
            #region InstantiateContainers

            var tileComponentLookup = GetComponentDataFromEntity<TileComponent>(true);
            var linkingTilesArray = _linkingTilesQuery.ToEntityArray(Allocator.TempJob);
            var centerNodesMap = new NativeMultiHashMap<Entity, TileLink>(
                    centerNodesMaxCount * 6,
                    Allocator.TempJob
                );
            var outerNodesMap = new NativeMultiHashMap<Entity, TileLink>(
                    outerNodesMaxCount * 6,
                    Allocator.TempJob
                );
            var tileBufferMap = new NativeHashMap<Entity, TileBuffer>(
                    totalTilesMaxCount,
                    Allocator.TempJob
                );

            #endregion
            #region ProcessCenterNodes

            var processInitialTileLinksJob =
                new ProcessInitialTileLinksJob
                {
                    LinkingTilesArray = linkingTilesArray,
                    TileComponentLookup = tileComponentLookup,
                    CenterNodesMapWriter = centerNodesMap.AsParallelWriter()
                }.Schedule(centerNodesMaxCount, 1);
            Dependency = JobHandle.CombineDependencies(Dependency, processInitialTileLinksJob);

            var centerNodes = new NativeList<Entity>(centerNodesMaxCount, Allocator.TempJob);
            var getUniqueMultHMapKeysJob = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                                           {
                                               MultiHashMap = centerNodesMap,
                                               Keys = centerNodes
                                           }.Schedule(processInitialTileLinksJob);

            var processCenterNodesTileLinksJob =
                new ProcessNodesTileLinksJob
                {
                    Nodes = centerNodes,
                    NodesTileLinksMap = centerNodesMap,
                    TileComponentLookup = tileComponentLookup,
                    TileBufferMapWriter = tileBufferMap.AsParallelWriter()
                }.Schedule(centerNodes, 1, getUniqueMultHMapKeysJob);

            #endregion
            #region ProcessOuterNodes

            var computeTriangularLinkPropagationJob =
                new ComputeTriangularLinkPropagationJob
                {
                    CenterNodes = centerNodes,
                    TileBufferMap = tileBufferMap,
                    OuterNodesMapWriter = outerNodesMap.AsParallelWriter()
                }.Schedule(centerNodes, 1, processCenterNodesTileLinksJob);

            var outerNodes = new NativeList<Entity>(outerNodesMaxCount, Allocator.TempJob);
            getUniqueMultHMapKeysJob = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                                       {
                                           MultiHashMap = outerNodesMap, Keys = outerNodes
                                       }.Schedule(computeTriangularLinkPropagationJob);

            var processOuterNodesTileLinksJob =
                new ProcessNodesTileLinksJob
                {
                    Nodes = outerNodes,
                    NodesTileLinksMap = outerNodesMap,
                    TileComponentLookup = tileComponentLookup,
                    TileBufferMapWriter = tileBufferMap.AsParallelWriter()
                }.Schedule(outerNodes, 1, getUniqueMultHMapKeysJob);

            #endregion
            #region SetLinkedTileBuffers

            var totalTiles = new NativeList<Entity>(totalTilesMaxCount, Allocator.TempJob);
            var getHMapKeysJob = new GetHMapKeysJob<Entity, TileBuffer>
                                 {
                                     HashMap = tileBufferMap, Keys = totalTiles
                                 }.Schedule(processOuterNodesTileLinksJob);

            var setLinkedTileBuffersJob =
                new SetLinkedTileBuffersJob
                {
                    TileBufferMapKeys = totalTiles,
                    TileComponentLookup = tileComponentLookup,
                    TileBufferMap = tileBufferMap,
                    EcbWriter = _ecbSystem.CreateCommandBuffer()
                                          .AsParallelWriter()
                }.Schedule(totalTiles, 1, getHMapKeysJob);

            Dependency = JobHandle.CombineDependencies(
                    Dependency,
                    setLinkedTileBuffersJob
                );

            #endregion
            #region CleanUp

            var ecb = _ecbSystem.CreateCommandBuffer();
            foreach (var entity in linkingTilesArray)
                ecb.SetSharedComponent(
                        entity,
                        new GridGenerationComponent(GridGenerationPhase.End)
                    );

            centerNodesMap.Dispose(processCenterNodesTileLinksJob);
            centerNodes.Dispose(computeTriangularLinkPropagationJob);
            outerNodes.Dispose(processOuterNodesTileLinksJob);
            outerNodesMap.Dispose(processOuterNodesTileLinksJob);
            totalTiles.Dispose(setLinkedTileBuffersJob);
            tileBufferMap.Dispose(setLinkedTileBuffersJob);
            linkingTilesArray.Dispose(Dependency);

            #endregion

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ProcessInitialTileLinksJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Entity> LinkingTilesArray;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> TileComponentLookup;

            [WriteOnly]
            public NativeMultiHashMap<Entity, TileLink>.ParallelWriter CenterNodesMapWriter;

            public void Execute(int i)
            {
                var tile = LinkingTilesArray[i];
                var tileComponent = TileComponentLookup[tile];

                for (var j = 0; j < 6; ++j)
                {
                    var adjTile = tileComponent.AdjacentTiles[j];
                    if (adjTile != Entity.Null)
                        CenterNodesMapWriter.Add(
                                adjTile,
                                new TileLink
                                {Tile = adjTile, Index = (j + 3) % 6, AdjTile = tile}
                            );
                }
            }
        }

        [BurstCompile]
        private struct ProcessNodesTileLinksJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> Nodes;
            [ReadOnly]
            public NativeMultiHashMap<Entity, TileLink> NodesTileLinksMap;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> TileComponentLookup;

            [WriteOnly]
            public NativeHashMap<Entity, TileBuffer>.ParallelWriter TileBufferMapWriter;

            public void Execute(int i)
            {
                var tileKey = Nodes[i];
                var tileLinksEnumerator = NodesTileLinksMap.GetValuesForKey(tileKey);
                var tileBuffer = TileComponentLookup[tileKey]
                                 .AdjacentTiles.Clone();

                while (tileLinksEnumerator.MoveNext())
                {
                    var curTilelink = tileLinksEnumerator.Current;
                    tileBuffer[curTilelink.Index] = curTilelink.AdjTile;
                }

                TileBufferMapWriter.TryAdd(tileKey, tileBuffer);
            }
        }

        [BurstCompile]
        private struct ComputeTriangularLinkPropagationJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> CenterNodes;
            [ReadOnly]
            public NativeHashMap<Entity, TileBuffer> TileBufferMap;

            [WriteOnly]
            public NativeMultiHashMap<Entity, TileLink>.ParallelWriter OuterNodesMapWriter;

            public void Execute(int i)
            {
                var tile = CenterNodes[i];
                var tileBuffer = TileBufferMap[tile];

                for (var j = 0; j < 6; ++j)
                {
                    var adjTileN0 = tileBuffer[j];
                    var adjTileN1 = tileBuffer[(j + 1) % 6];

                    if (adjTileN1 == Entity.Null) { ++j; }
                    else if (adjTileN0 != Entity.Null)
                    {
                        OuterNodesMapWriter.Add(
                                adjTileN0,
                                new TileLink
                                {
                                    Tile = adjTileN0,
                                    Index = (j + 2) % 6,
                                    AdjTile = adjTileN1
                                }
                            );
                        OuterNodesMapWriter.Add(
                                adjTileN1,
                                new TileLink
                                {
                                    Tile = adjTileN1,
                                    Index = (j + 5) % 6,
                                    AdjTile = adjTileN0
                                }
                            );
                    }
                }
            }
        }

        [BurstCompile]
        private struct SetLinkedTileBuffersJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> TileBufferMapKeys;
            [ReadOnly]
            public NativeHashMap<Entity, TileBuffer> TileBufferMap;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> TileComponentLookup;

            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter EcbWriter;

            [BurstDiscard]
            private void Log(Entity e,
                             TileBuffer tb,
                             TileComponent tc)
            {
                var log = e + ": (" + tb + ")";
                for (var i = 0; i < 6; ++i) log += "[" + i + "] " + tb[i] + "; ";
                log += "tc -> (" + tc.AdjacentTiles + ")";
                Debug.Log(log);
            }

            public void Execute(int i)
            {
                var tile = TileBufferMapKeys[i];
                var tileBuffer = TileBufferMap[tile];
                var tileComponent = TileComponentLookup[tile];
                var tmp = new TileComponent
                          {
                              Position = tileComponent.Position,
                              State = tileComponent.State,
                              AdjacentTiles = tileBuffer
                          };
                Log(tile, tileBuffer, tmp);

                EcbWriter.SetComponent(
                        i,
                        tile,
                        tmp
                    );
            }
        }
    }
}