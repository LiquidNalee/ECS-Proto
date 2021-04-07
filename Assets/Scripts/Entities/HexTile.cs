using Components.HexGrid;
using Components.Tags.Selection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Entities
{
    public class HexTile : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(
            Entity entity,
            EntityManager entityManager,
            GameObjectConversionSystem conversionSystem
        )
        {
            var pos = transform.position;
            var gridPos = new float3(pos.x, pos.y - .3f, pos.z);

            entityManager.AddComponentData(
                entity,
                new HexTileComponent
                {
                    Position = gridPos
                }
            );
            entityManager.AddComponent<SelectableTag>(entity);
        }
    }
}