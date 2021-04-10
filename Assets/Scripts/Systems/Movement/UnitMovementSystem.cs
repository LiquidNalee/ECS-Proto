using System.Diagnostics.CodeAnalysis;
using Components.Movement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems.Movement
{
    [SuppressMessage("ReSharper", "TooManyDeclarations")]
    public class UnitMovementSystem : SystemBase
    {
        private const float Speed = 10;

        protected override void OnUpdate()
        {
            var deltaTime = UnityEngine.Time.deltaTime;

            Entities.WithAll<UnitComponent, Translation, Rotation>()
                    .ForEach(
                        (
                            Entity entity, ref Translation translation, ref Rotation rotation,
                            ref UnitComponent actor
                        ) =>
                        {
                            var direction = actor.Destination - actor.Position;

                            if (math.length(direction) > .1f)
                            {
                                direction = math.normalize(direction) * Speed;
                                var lookDirection = new float3(direction.x, 0, direction.z);
                                rotation.Value = quaternion.LookRotation(
                                    lookDirection,
                                    math.up()
                                );
                                translation.Value += direction * deltaTime;
                                actor.Position = translation.Value;
                            }
                            else
                            {
                                translation.Value = actor.Position;
                                actor.Position = translation.Value;
                            }
                        }
                    )
                    .ScheduleParallel();
        }
    }
}