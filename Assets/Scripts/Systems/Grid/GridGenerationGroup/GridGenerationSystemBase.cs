using System.Diagnostics.CodeAnalysis;
using Components.Grid;
using Unity.Entities;

namespace Systems.Grid.GridGenerationGroup
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public abstract class GridGenerationSystemBase : SystemBase
    {
        protected EndInitializationEntityCommandBufferSystem _ecbSystem;
        protected EntityQuery _systemTilesQuery;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();

            _systemTilesQuery = GetEntityQuery(
                    ComponentType.ReadOnly<TileComponent>(),
                    ComponentType.ReadWrite<GridGenerationComponent>()
                );
        }
    }
}