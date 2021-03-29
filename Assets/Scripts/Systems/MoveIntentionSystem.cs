using Components;
using Components.Inputs;
using Components.Movement;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Systems
{
    public class MoveIntentionSystem : SystemBase
    {
        protected override void OnUpdate() {
            var rClickedTiles = GetEntityQuery(
                    ComponentType.ReadOnly<RightClickEvent>(),
                    ComponentType.ReadOnly<HexTileComponent>()
                )
                .ToEntityArray(Allocator.TempJob);

            if (rClickedTiles.Length > 0)
            {
                if (rClickedTiles.Length > 1) Debug.Log(rClickedTiles.Length);

                ComponentDataFromEntity<HexTileComponent> hexTileEntityDict =
                    GetComponentDataFromEntity<HexTileComponent>(true);
                var hexTileComponent = hexTileEntityDict[rClickedTiles[0]];

                Entities.WithAll<ActorComponent>()
                        .ForEach(
                            (Entity id, ref ActorComponent actor) =>
                            {
                                actor.Destination = hexTileComponent.Position;
                            }
                        )
                        .Schedule();
            }

            rClickedTiles.Dispose();
        }
    }
}