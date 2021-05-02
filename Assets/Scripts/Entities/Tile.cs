using Components.Events.Physics;
using Components.Grid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Entities
{
    public class Tile : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity,
                            EntityManager entityManager,
                            GameObjectConversionSystem conversionSystem)
        {
            var pos = transform.position;
            var gridPos = new float3(pos.x, pos.y - .3f, pos.z);
            var tileComponent = new TileComponent
                                {
                                    Position = gridPos,
                                    State = 0,
                                    AdjacentTiles = TileBuffer.Empty
                                };

            entityManager.AddComponentData(entity, tileComponent);
            entityManager.AddSharedComponentData(entity, GridGenerationComponent.ExpansionPhase);
            entityManager.AddBuffer<StatefulTriggerEvent>(entity);
        }
    }
}