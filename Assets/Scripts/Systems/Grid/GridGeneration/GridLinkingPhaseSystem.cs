using Systems.Grid.GridGeneration.Utils;
using Systems.Utils.Jobs.HashMapUtilityJobs;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Systems.Grid.GridGeneration
{
    public class GridLinkingPhaseSystem : GridGenerationSystemBase
    {
        private int _ite;
        private EntityQuery _linkingTilesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            TilesBaseQuery.None = new ComponentType[] {typeof(TileLinkUpdate)};
            _linkingTilesQuery = GetEntityQuery(TilesBaseQuery);
            _linkingTilesQuery.SetSharedComponentFilter(
                    GridGenerationComponent.OuterNodeLinkingPhase
                );

            _ite = 0;
        }

        protected override void OnUpdate()
        {
            if (_linkingTilesQuery.IsEmpty) return;

            #region Initialization

            var outerNodesMaxCount = _linkingTilesQuery.CalculateEntityCount();
            var centerNodesMaxCount = outerNodesMaxCount;
            var centerNodesMaxLinksCount = centerNodesMaxCount * 6;
            var outerNodesMaxLinkCount = outerNodesMaxCount * 6 * 2;
            var modifiedTilesMaxCount = centerNodesMaxCount + outerNodesMaxCount * 3;

            ComponentDataFromEntity<TileComponent> tileComponentLookup =
                GetComponentDataFromEntity<TileComponent>(true);
            NativeArray<Entity> linkingTilesArray =
                _linkingTilesQuery.ToEntityArray(Allocator.TempJob);

            var centerNodesMap = new NativeMultiHashMap<Entity, TileLink>(
                    centerNodesMaxLinksCount,
                    Allocator.TempJob
                );

            var outerNodesMap = new NativeMultiHashMap<Entity, TileLink>(
                    outerNodesMaxLinkCount,
                    Allocator.TempJob
                );

            var tileBufferMap = new NativeHashMap<Entity, TileBuffer>(
                    modifiedTilesMaxCount,
                    Allocator.TempJob
                );

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

            foreach (Entity tile in linkingTilesArray)
                ecb.SetSharedComponent(
                        tile,
                        _ite < 4
                            ? GridGenerationComponent.ExpansionPhase
                            : GridGenerationComponent.ReadyPhase
                    );

            _ite++;

            #endregion
            #region ProcessCenterNodes

            Dependency =
                new ProcessInitialTileLinksJob
                {
                    entityTypeHandle = GetEntityTypeHandle(),
                    tileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    centerNodesMapWriter = centerNodesMap.AsParallelWriter()
                }.Schedule(_linkingTilesQuery);

            var centerNodes = new NativeList<Entity>(centerNodesMaxCount, Allocator.TempJob);
            Dependency = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                         {
                             multiHashMap = centerNodesMap, keys = centerNodes
                         }.Schedule(Dependency);

            Dependency = new ProcessNodesTileLinksJob
                         {
                             nodes = centerNodes,
                             nodesTileLinksMap = centerNodesMap,
                             tileComponentLookup = tileComponentLookup,
                             tileBufferMapWriter = tileBufferMap.AsParallelWriter()
                         }.Schedule(centerNodes, 1, Dependency);

            centerNodesMap.Dispose(Dependency);

            #endregion
            #region ProcessOuterNodes

            Dependency =
                new ComputeTriangularLinkPropagationJob
                {
                    centerNodes = centerNodes,
                    linkingTilesArray = linkingTilesArray,
                    tileBufferMap = tileBufferMap,
                    outerNodesMapWriter = outerNodesMap.AsParallelWriter()
                }.Schedule(centerNodes, 1, Dependency);

            centerNodes.Dispose(Dependency);
            linkingTilesArray.Dispose(Dependency);

            var outerNodes = new NativeList<Entity>(outerNodesMaxCount, Allocator.TempJob);
            Dependency = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                         {
                             multiHashMap = outerNodesMap, keys = outerNodes
                         }.Schedule(Dependency);

            Dependency = new ProcessNodesTileLinksJob
                         {
                             nodes = outerNodes,
                             nodesTileLinksMap = outerNodesMap,
                             tileComponentLookup = tileComponentLookup,
                             tileBufferMapWriter = tileBufferMap.AsParallelWriter()
                         }.Schedule(outerNodes, 1, Dependency);

            outerNodes.Dispose(Dependency);
            outerNodesMap.Dispose(Dependency);

            #endregion
            #region SetLinkedTilesBuffers

            var totalTiles = new NativeList<Entity>(modifiedTilesMaxCount, Allocator.TempJob);
            Dependency = new GetHMapKeysJob<Entity, TileBuffer>
                         {
                             hashMap = tileBufferMap, keys = totalTiles
                         }.Schedule(Dependency);

            Dependency = new SetLinkedTilesBuffersJob
                         {
                             tileBufferMapKeys = totalTiles,
                             tileComponentLookup = tileComponentLookup,
                             tileBufferMap = tileBufferMap,
                             ecbWriter = ecbSystem.CreateCommandBuffer()
                                                  .AsParallelWriter()
                         }.Schedule(totalTiles, 1, Dependency);

            totalTiles.Dispose(Dependency);
            tileBufferMap.Dispose(Dependency);

            #endregion

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ProcessInitialTileLinksJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> tileComponentTypeHandle;

            [WriteOnly]
            public NativeMultiHashMap<Entity, TileLink>.ParallelWriter centerNodesMapWriter;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                NativeArray<Entity> linkingTilesArray = chunk.GetNativeArray(entityTypeHandle);
                NativeArray<TileComponent> tileComponentArray =
                    chunk.GetNativeArray(tileComponentTypeHandle);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    Entity tile = linkingTilesArray[i];
                    TileComponent tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j)
                    {
                        Entity adjTile = tileComponent.AdjacentTiles[j];

                        if (adjTile != Entity.Null)
                        {
                            var tileLink = new TileLink
                                           {
                                               tile = adjTile, index = (j + 3) % 6, adjTile = tile
                                           };
                            centerNodesMapWriter.Add(adjTile, tileLink);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct ProcessNodesTileLinksJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> nodes;
            [ReadOnly]
            public NativeMultiHashMap<Entity, TileLink> nodesTileLinksMap;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> tileComponentLookup;

            [WriteOnly]
            public NativeHashMap<Entity, TileBuffer>.ParallelWriter tileBufferMapWriter;

            public void Execute(int i)
            {
                Entity tileKey = nodes[i];
                NativeMultiHashMap<Entity, TileLink>.Enumerator tileLinksEnumerator =
                    nodesTileLinksMap.GetValuesForKey(tileKey);

                TileBuffer tileBuffer = tileComponentLookup[tileKey]
                                        .AdjacentTiles.Clone();

                while (tileLinksEnumerator.MoveNext())
                {
                    TileLink curTilelink = tileLinksEnumerator.Current;
                    tileBuffer[curTilelink.index] = curTilelink.adjTile;
                }

                tileBufferMapWriter.TryAdd(tileKey, tileBuffer);
            }
        }

        [BurstCompile]
        private struct ComputeTriangularLinkPropagationJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> centerNodes;
            [ReadOnly]
            public NativeArray<Entity> linkingTilesArray;
            [ReadOnly]
            public NativeHashMap<Entity, TileBuffer> tileBufferMap;

            [WriteOnly]
            public NativeMultiHashMap<Entity, TileLink>.ParallelWriter outerNodesMapWriter;

            public void Execute(int i)
            {
                Entity tile = centerNodes[i];
                TileBuffer tileBuffer = tileBufferMap[tile];

                for (var j = 0; j < 6; ++j)
                {
                    Entity adjTileN0 = tileBuffer[j];
                    Entity adjTileN1 = tileBuffer[(j + 1) % 6];

                    if (adjTileN1 == Entity.Null) { ++j; }
                    else if (adjTileN0 == Entity.Null) { }
                    else if (linkingTilesArray.Contains(adjTileN0) ||
                             linkingTilesArray.Contains(adjTileN1))
                    {
                        var adjTileLinkN0 = new TileLink
                                            {
                                                tile = adjTileN0,
                                                index = (j + 2) % 6,
                                                adjTile = adjTileN1
                                            };
                        var adjTileLinkN1 = new TileLink
                                            {
                                                tile = adjTileN1,
                                                index = (j + 5) % 6,
                                                adjTile = adjTileN0
                                            };
                        outerNodesMapWriter.Add(adjTileN0, adjTileLinkN0);
                        outerNodesMapWriter.Add(adjTileN1, adjTileLinkN1);
                    }
                }
            }
        }

        [BurstCompile]
        private struct SetLinkedTilesBuffersJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> tileBufferMapKeys;
            [ReadOnly]
            public NativeHashMap<Entity, TileBuffer> tileBufferMap;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> tileComponentLookup;

            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter ecbWriter;

            public void Execute(int i)
            {
                Entity tile = tileBufferMapKeys[i];
                TileBuffer updatedTileBuffer = tileBufferMap[tile];
                TileComponent curTileComponent = tileComponentLookup[tile];
                var updatedTileComponent = new TileComponent
                                           {
                                               Position = curTileComponent.Position,
                                               State = curTileComponent.State,
                                               AdjacentTiles = updatedTileBuffer
                                           };
                ecbWriter.SetComponent(i, tile, updatedTileComponent);
            }
        }
    }
}