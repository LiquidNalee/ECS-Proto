using System.Diagnostics.CodeAnalysis;
using Components.Movement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems
{
    [SuppressMessage("ReSharper", "TooManyDeclarations")]
    public class ActorSystem : SystemBase
    {
        protected override void OnUpdate() {
            var deltaTime = UnityEngine.Time.deltaTime;
            var query = GetEntityQuery(
                ComponentType.ReadOnly<Translation>(),
                ComponentType.ReadOnly<Rotation>(),
                ComponentType.ReadOnly<ActorComponent>()
            );


            Entities.WithAll<ActorComponent>()
                    .ForEach(
                        (
                            Entity entity,
                            ref Translation translation,
                            ref Rotation rotation,
                            ref ActorComponent actor
                        ) =>
                        {
                            var direction = actor.Destination - actor.Position;

                            if (math.length(direction) > .1f)
                            {
                                direction = math.normalize(direction);
                                rotation.Value = quaternion.LookRotation(direction, math.up());
                                translation.Value = translation.Value + direction * deltaTime;
                                actor.Position = translation.Value;
                            }
                            else
                            {
                                translation.Value = actor.Position;
                                actor.Position = translation.Value;
                            }
                        }
                    )
                    .Schedule();
        }
    }
}