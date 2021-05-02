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

            var tileComponentLookup = GetComponentDataFromEntity<TileComponent>(true);
            var linkingTilesArray = _linkingTilesQuery.ToEntityArray(Allocator.TempJob);

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

            var commandBuffer = EcbSystem.CreateCommandBuffer();

            foreach (var entity in linkingTilesArray)
                commandBuffer.SetSharedComponent(
                        entity,
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
                    EntityTypeHandle = GetEntityTypeHandle(),
                    TileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    CenterNodesMapWriter = centerNodesMap.AsParallelWriter()
                }.Schedule(_linkingTilesQuery);

            var centerNodes = new NativeList<Entity>(centerNodesMaxCount, Allocator.TempJob);
            Dependency = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                         {
                             MultiHashMap = centerNodesMap,
                             Keys = centerNodes
                         }.Schedule(Dependency);

            Dependency = new ProcessNodesTileLinksJob
                         {
                             Nodes = centerNodes,
                             NodesTileLinksMap = centerNodesMap,
                             TileComponentLookup = tileComponentLookup,
                             TileBufferMapWriter = tileBufferMap.AsParallelWriter()
                         }.Schedule(centerNodes, 1, Dependency);

            centerNodesMap.Dispose(Dependency);

            #endregion
            #region ProcessOuterNodes

            Dependency =
                new ComputeTriangularLinkPropagationJob
                {
                    CenterNodes = centerNodes,
                    LinkingTilesArray = linkingTilesArray,
                    TileBufferMap = tileBufferMap,
                    OuterNodesMapWriter = outerNodesMap.AsParallelWriter()
                }.Schedule(centerNodes, 1, Dependency);

            centerNodes.Dispose(Dependency);
            linkingTilesArray.Dispose(Dependency);

            var outerNodes = new NativeList<Entity>(outerNodesMaxCount, Allocator.TempJob);
            Dependency = new GetUniqueMultHMapKeysJob<Entity, TileLink>
                         {
                             MultiHashMap = outerNodesMap,
                             Keys = outerNodes
                         }.Schedule(Dependency);

            Dependency = new ProcessNodesTileLinksJob
                         {
                             Nodes = outerNodes,
                             NodesTileLinksMap = outerNodesMap,
                             TileComponentLookup = tileComponentLookup,
                             TileBufferMapWriter = tileBufferMap.AsParallelWriter()
                         }.Schedule(outerNodes, 1, Dependency);

            outerNodes.Dispose(Dependency);
            outerNodesMap.Dispose(Dependency);

            #endregion
            #region SetLinkedTilesBuffers

            var totalTiles = new NativeList<Entity>(modifiedTilesMaxCount, Allocator.TempJob);
            Dependency = new GetHMapKeysJob<Entity, TileBuffer>
                         {
                             HashMap = tileBufferMap,
                             Keys = totalTiles
                         }.Schedule(Dependency);

            Dependency = new SetLinkedTilesBuffersJob
                         {
                             TileBufferMapKeys = totalTiles,
                             TileComponentLookup = tileComponentLookup,
                             TileBufferMap = tileBufferMap,
                             EcbWriter = EcbSystem.CreateCommandBuffer()
                                                  .AsParallelWriter()
                         }.Schedule(totalTiles, 1, Dependency);

            totalTiles.Dispose(Dependency);
            tileBufferMap.Dispose(Dependency);

            #endregion

            EcbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ProcessInitialTileLinksJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> TileComponentTypeHandle;

            [WriteOnly]
            public NativeMultiHashMap<Entity, TileLink>.ParallelWriter CenterNodesMapWriter;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                var linkingTilesArray = chunk.GetNativeArray(EntityTypeHandle);
                var tileComponentArray = chunk.GetNativeArray(TileComponentTypeHandle);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    var tile = linkingTilesArray[i];
                    var tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j)
                    {
                        var adjTile = tileComponent.AdjacentTiles[j];

                        if (adjTile != Entity.Null)
                        {
                            var tileLink = new TileLink
                                           {
                                               Tile = adjTile,
                                               Index = (j + 3) % 6,
                                               AdjTile = tile
                                           };
                            CenterNodesMapWriter.Add(adjTile, tileLink);
                        }
                    }
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
            public NativeArray<Entity> LinkingTilesArray;
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
                    else if (adjTileN0 == Entity.Null) { }
                    else if (LinkingTilesArray.Contains(adjTileN0) ||
                             LinkingTilesArray.Contains(adjTileN1))
                    {
                        var adjTileLinkN0 = new TileLink
                                            {
                                                Tile = adjTileN0,
                                                Index = (j + 2) % 6,
                                                AdjTile = adjTileN1
                                            };
                        var adjTileLinkN1 = new TileLink
                                            {
                                                Tile = adjTileN1,
                                                Index = (j + 5) % 6,
                                                AdjTile = adjTileN0
                                            };
                        OuterNodesMapWriter.Add(adjTileN0, adjTileLinkN0);
                        OuterNodesMapWriter.Add(adjTileN1, adjTileLinkN1);
                    }
                }
            }
        }

        [BurstCompile]
        private struct SetLinkedTilesBuffersJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<Entity> TileBufferMapKeys;
            [ReadOnly]
            public NativeHashMap<Entity, TileBuffer> TileBufferMap;
            [ReadOnly]
            public ComponentDataFromEntity<TileComponent> TileComponentLookup;

            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter EcbWriter;

            public void Execute(int i)
            {
                var tile = TileBufferMapKeys[i];
                var updatedTileBuffer = TileBufferMap[tile];
                var curTileComponent = TileComponentLookup[tile];
                var updatedTileComponent = new TileComponent
                                           {
                                               Position = curTileComponent.Position,
                                               State = curTileComponent.State,
                                               AdjacentTiles = updatedTileBuffer
                                           };
                EcbWriter.SetComponent(i, tile, updatedTileComponent);
            }
        }
    }
}