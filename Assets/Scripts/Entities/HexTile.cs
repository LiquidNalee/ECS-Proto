using Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Entities
{
    public class HexTile: MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(
            Entity entity,
            EntityManager entityManager,
            GameObjectConversionSystem conversionSystem
        ) {
            var pos = transform.position;
            var gridPos = new int3(
                (int) math.floor(pos.x),
                (int) math.floor(pos.y),
                (int) math.floor(pos.z)
            );
            
            entityManager.AddComponentData(
                entity,
                new HexTileComponent{
                    Position = gridPos
                }
            );
        }
    }
}