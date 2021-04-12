using Components.HexGrid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Systems.Utils.HexUtils;

namespace Systems.HexGrid
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
    public class HexGridSystem : SystemBase
    {
        private NativeArray<Entity> _adjTilesArray;
        private NativeQueue<Entity> _adjTilesJobEntryQueue;
        private NativeQueue<Entity> _adjTilesJobResultQueue;
        private EndInitializationEntityCommandBufferSystem _ecbSystem;
        private NativeArray<float3> _hexPositions;
        private NativeArray<Entity> _originHexTileArray;

        protected override void OnCreate()
        {
            _adjTilesJobEntryQueue = new NativeQueue<Entity>(Allocator.Persistent);
            _adjTilesJobResultQueue = new NativeQueue<Entity>(Allocator.Persistent);

            _hexPositions = new NativeArray<float3>(6, Allocator.Persistent);
            _hexPositions[(int) HexDirection.Top] = new float3(0f, 0f, -2.1f);
            _hexPositions[(int) HexDirection.TopRight] = new float3(-1.81865f, 0f, -1.05f);
            _hexPositions[(int) HexDirection.BottomRight] = new float3(-1.81865f, 0f, 1.05f);
            _hexPositions[(int) HexDirection.Bottom] = new float3(0f, 0f, 2.1f);
            _hexPositions[(int) HexDirection.BottomLeft] = new float3(1.81865f, 0f, 1.05f);
            _hexPositions[(int) HexDirection.TopLeft] = new float3(1.81865f, 0f, -1.05f);
        }

        protected override void OnDestroy()
        {
            _hexPositions.Dispose();
            if (_adjTilesJobEntryQueue.IsCreated) _adjTilesJobEntryQueue.Dispose();
            if (_adjTilesJobResultQueue.IsCreated) _adjTilesJobResultQueue.Dispose();
            if (_adjTilesArray.IsCreated) _adjTilesArray.Dispose();
            if (_originHexTileArray.IsCreated) _originHexTileArray.Dispose();
        }

        protected override void OnStartRunning()
        {
            _ecbSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();

            var hexTileQueueWriter = _adjTilesJobEntryQueue.AsParallelWriter();
            Entities.WithAll<HexTileComponent, AdjacentTileBufferElement>()
                    .ForEach((Entity entity) => { hexTileQueueWriter.Enqueue(entity); })
                    .Run();
        }

        protected override void OnUpdate()
        {
            if ((!_originHexTileArray.IsCreated || _originHexTileArray.Length == 0) &&
                _adjTilesJobEntryQueue.IsEmpty() && _adjTilesJobResultQueue.IsEmpty())
                return;

            _ecbSystem.AddJobHandleForProducer(Dependency);
            var adjTileBufferLookup = GetBufferFromEntity<AdjacentTileBufferElement>(true);
            var hexTileCmpntLookup = GetComponentDataFromEntity<HexTileComponent>(true);

            // SetAdjacentTileValues(adjTileBufferLookup, hexTileCmpntLookup);
            InstantiateAdjacentTiles(adjTileBufferLookup);
            // TransferQueueContent();
        }

        private void SetAdjacentTileValues(
            BufferFromEntity<AdjacentTileBufferElement> adjTileBufferLookup,
            ComponentDataFromEntity<HexTileComponent> hexTileCmpntLookup
        )
        {
            if (!_originHexTileArray.IsCreated || _originHexTileArray.Length <= 0) return;

            var setAdjTileValuesJob = new SetAdjacentTileValuesJob
                                      {
                                          OriginHexTileArray = _originHexTileArray,
                                          AdjacentTilesArray = _adjTilesArray,
                                          HexPositions = _hexPositions,
                                          AdjacentTileBufferLookup = adjTileBufferLookup,
                                          HexTileComponentLookup = hexTileCmpntLookup,
                                          EcbWriter = _ecbSystem.CreateCommandBuffer()
                                              .AsParallelWriter()
                                      };
            var setAdjTileValuesJobHandle =
                setAdjTileValuesJob.Schedule(_originHexTileArray.Length, 1);
            _ecbSystem.AddJobHandleForProducer(setAdjTileValuesJobHandle);
        }

        private void InstantiateAdjacentTiles(
            BufferFromEntity<AdjacentTileBufferElement> adjTileBufferLookup
        )
        {
            if (_adjTilesJobEntryQueue.IsEmpty()) return;

            _originHexTileArray = _adjTilesJobEntryQueue.ToArray(Allocator.TempJob);
            _adjTilesArray = new NativeArray<Entity>(
                _adjTilesJobEntryQueue.Count * 6,
                Allocator.TempJob
            );

            var adjTilesJob = new InstantiateAdjacentTilesJob
                              {
                                  OriginHexTileArray = _originHexTileArray,
                                  AdjacentTilesArray = _adjTilesArray,
                                  AdjacentTileBufferLookup = adjTileBufferLookup,
                                  EcbWriter = _ecbSystem.CreateCommandBuffer()
                                                        .AsParallelWriter()
                              };
            var adjTilesJobHandle = adjTilesJob.Schedule(_adjTilesJobEntryQueue.Count, 1);
            _ecbSystem.AddJobHandleForProducer(adjTilesJobHandle);

            _adjTilesJobEntryQueue.Clear();
        }

        private void TransferQueueContent()
        {
            if (_adjTilesJobResultQueue.IsEmpty()) return;

            var transferJob = new TransferQueueContentJob<Entity>
                              {
                                  From = _adjTilesJobResultQueue.ToArray(Allocator.TempJob),
                                  To = _adjTilesJobEntryQueue.AsParallelWriter()
                              };
            var transferJobHandle = transferJob.Schedule(_adjTilesJobResultQueue.Count, 1);
            _ecbSystem.AddJobHandleForProducer(transferJobHandle);
        }

        [BurstCompile]
        private struct InstantiateAdjacentTilesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> OriginHexTileArray;

            [WriteOnly] [NativeDisableParallelForRestriction]
            public NativeArray<Entity> AdjacentTilesArray;

            [ReadOnly]
            public BufferFromEntity<AdjacentTileBufferElement> AdjacentTileBufferLookup;

            [WriteOnly] public EntityCommandBuffer.ParallelWriter EcbWriter;

            public void Execute(int index)
            {
                var originHexTile = OriginHexTileArray[index];
                var originAdjacentTileBuffer = AdjacentTileBufferLookup[originHexTile];

                for (var i = 0; i < 6; ++i)
                {
                    if (originAdjacentTileBuffer[i] != Entity.Null) continue;

                    AdjacentTilesArray[i + index * 6] =
                        EcbWriter.Instantiate(i + index * 6, originHexTile);
                }
            }
        }

        [BurstCompile]
        private struct SetAdjacentTileValuesJob : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion]
            public NativeArray<Entity> OriginHexTileArray;

            [ReadOnly] [DeallocateOnJobCompletion]
            public NativeArray<Entity> AdjacentTilesArray;

            [ReadOnly] public NativeArray<float3> HexPositions;

            [ReadOnly] public ComponentDataFromEntity<HexTileComponent> HexTileComponentLookup;

            [ReadOnly]
            public BufferFromEntity<AdjacentTileBufferElement> AdjacentTileBufferLookup;

            [WriteOnly] public EntityCommandBuffer.ParallelWriter EcbWriter;

            public void Execute(int index)
            {
                var originHexTile = OriginHexTileArray[index];

                for (var i = 0; i < 6; ++i)
                {
                    var adjacentTile = AdjacentTilesArray[i + index * 6];
                    if (adjacentTile == Entity.Null) continue;

                    var pos = HexTileComponentLookup[originHexTile]
                                  .Position + HexPositions[index];
                    EcbWriter.SetComponent(
                        i + index * 6,
                        adjacentTile,
                        new HexTileComponent {Position = pos}
                    );

                    var originAdjacentTileBuffer = AdjacentTileBufferLookup[originHexTile];
                    var destAdjancentTileBuffer = AdjacentTileBufferLookup[adjacentTile];

                    if (i == index + 3 % 6)
                        destAdjancentTileBuffer.Add(
                            new AdjacentTileBufferElement {Value = originHexTile}
                        );
                    destAdjancentTileBuffer.Add(
                        new AdjacentTileBufferElement {Value = Entity.Null}
                    );

                    originAdjacentTileBuffer.RemoveAt(index);
                    originAdjacentTileBuffer.Insert(
                        index,
                        new AdjacentTileBufferElement {Value = adjacentTile}
                    );
                }
            }
        }

        [BurstCompile]
        private struct TransferQueueContentJob<T> : IJobParallelFor where T : struct
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<T> From;
            [WriteOnly] public NativeQueue<T>.ParallelWriter To;

            public void Execute(int index) { To.Enqueue(From[index]); }
        }
    }
}