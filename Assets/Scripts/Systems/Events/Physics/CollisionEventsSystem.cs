using Components.Events.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Systems.Events.Physics
{
    [UpdateInGroup(typeof(EventProducerSystemGroup))]
    public class CollisionEventsSystem : SystemBase
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private EntityQuery _collisionEventsBufferEntityQuery;
        private StepPhysicsWorld _stepPhysicsWorldSystem;

        protected override void OnCreate()
        {
            _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();

            var queryDesc = new EntityQueryDesc
                            {
                                All = new ComponentType[]
                                      {
                                          typeof(PhysicsCollider),
                                          typeof(StatefulCollisionEvent),
                                          typeof(
                                              CollisionEventsReceiverProperties)
                                      }
                            };

            _collisionEventsBufferEntityQuery = GetEntityQuery(queryDesc);
        }

        protected override void OnUpdate()
        {
            Dependency = new CollisionEventsPreProcessJob
                         {
                             CollisionEventBufferType =
                                 GetBufferTypeHandle<StatefulCollisionEvent>()
                         }.ScheduleParallel(_collisionEventsBufferEntityQuery, Dependency);

            Dependency = new CollisionEventsJob
                         {
                             PhysicsWorld = _buildPhysicsWorldSystem.PhysicsWorld,
                             CollisionEventBufferFromEntity =
                                 GetBufferFromEntity<StatefulCollisionEvent>(),
                             CollisionEventsReceiverPropertiesFromEntity =
                                 GetComponentDataFromEntity<CollisionEventsReceiverProperties>(
                                         true
                                     )
                         }.Schedule(
                    _stepPhysicsWorldSystem.Simulation,
                    ref _buildPhysicsWorldSystem.PhysicsWorld,
                    Dependency
                );

            Dependency = new CollisionEventsPostProcessJob
                         {
                             CollisionEventBufferType =
                                 GetBufferTypeHandle<StatefulCollisionEvent>()
                         }.ScheduleParallel(_collisionEventsBufferEntityQuery, Dependency);
        }

        // todo; can maybe optimize by checking if chunk has changed?
        [BurstCompile]
        private struct CollisionEventsPreProcessJob : IJobChunk
        {
            public BufferTypeHandle<StatefulCollisionEvent>
                CollisionEventBufferType;

            public void Execute(ArchetypeChunk chunk,
                                int chunkIndex,
                                int firstEntityIndex)
            {
                var collisionEventsBufferAccessor =
                    chunk.GetBufferAccessor(CollisionEventBufferType);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var collisionEventsBuffer = collisionEventsBufferAccessor[i];

                    for (var j = collisionEventsBuffer.Length - 1; j >= 0; j--)
                    {
                        var collisionEventElement = collisionEventsBuffer[j];
                        collisionEventElement._isStale = true;
                        collisionEventsBuffer[j] = collisionEventElement;
                    }
                }
            }
        }

        [BurstCompile]
        private struct CollisionEventsJob : ICollisionEventsJob
        {
            [ReadOnly]
            public PhysicsWorld PhysicsWorld;
            public BufferFromEntity<StatefulCollisionEvent>
                CollisionEventBufferFromEntity;
            [ReadOnly]
            public ComponentDataFromEntity<CollisionEventsReceiverProperties>
                CollisionEventsReceiverPropertiesFromEntity;

            public void Execute(CollisionEvent collisionEvent)
            {
                CollisionEvent.Details collisionEventDetails = default;

                var aHasDetails = false;
                var bHasDetails = false;

                if (CollisionEventsReceiverPropertiesFromEntity
                    .HasComponent(collisionEvent.EntityA))
                    aHasDetails =
                        CollisionEventsReceiverPropertiesFromEntity[collisionEvent.EntityA]
                            .UsesCollisionDetails;

                if (CollisionEventsReceiverPropertiesFromEntity
                    .HasComponent(collisionEvent.EntityB))
                    bHasDetails =
                        CollisionEventsReceiverPropertiesFromEntity[collisionEvent.EntityB]
                            .UsesCollisionDetails;

                if (aHasDetails || bHasDetails)
                    collisionEventDetails = collisionEvent.CalculateDetails(ref PhysicsWorld);

                if (CollisionEventBufferFromEntity.HasComponent(collisionEvent.EntityA))
                    ProcessForEntity(
                            collisionEvent.EntityA,
                            collisionEvent.EntityB,
                            collisionEvent.Normal,
                            aHasDetails,
                            collisionEventDetails
                        );

                if (CollisionEventBufferFromEntity.HasComponent(collisionEvent.EntityB))
                    ProcessForEntity(
                            collisionEvent.EntityB,
                            collisionEvent.EntityA,
                            collisionEvent.Normal,
                            bHasDetails,
                            collisionEventDetails
                        );
            }

            private void ProcessForEntity(Entity entity,
                                          Entity otherEntity,
                                          float3 normal,
                                          bool hasDetails,
                                          CollisionEvent.Details collisionEventDetails)
            {
                var collisionEventBuffer =
                    CollisionEventBufferFromEntity[entity];

                var foundMatch = false;

                for (var i = 0; i < collisionEventBuffer.Length; i++)
                {
                    var collisionEvent = collisionEventBuffer[i];

                    // If entity is already there, update to Stay
                    if (collisionEvent.Entity == otherEntity)
                    {
                        foundMatch = true;
                        collisionEvent.Normal = normal;
                        collisionEvent.HasCollisionDetails = hasDetails;
                        collisionEvent.AverageContactPointPosition =
                            collisionEventDetails.AverageContactPointPosition;
                        collisionEvent.EstimatedImpulse =
                            collisionEventDetails.EstimatedImpulse;
                        collisionEvent.State = PhysicsEventState.Stay;
                        collisionEvent._isStale = false;
                        collisionEventBuffer[i] = collisionEvent;

                        break;
                    }
                }

                // If it's a new entity, add as Enter
                if (!foundMatch)
                    collisionEventBuffer.Add(
                            new StatefulCollisionEvent
                            {
                                Entity = otherEntity,
                                Normal = normal,
                                HasCollisionDetails = hasDetails,
                                AverageContactPointPosition =
                                    collisionEventDetails.AverageContactPointPosition,
                                EstimatedImpulse = collisionEventDetails.EstimatedImpulse,
                                State = PhysicsEventState.Enter,
                                _isStale = false
                            }
                        );
            }
        }

        [BurstCompile]
        private struct CollisionEventsPostProcessJob : IJobChunk
        {
            public BufferTypeHandle<StatefulCollisionEvent>
                CollisionEventBufferType;

            public void Execute(ArchetypeChunk chunk,
                                int chunkIndex,
                                int firstEntityIndex)
            {
                if (chunk.Has(CollisionEventBufferType))
                {
                    var collisionEventsBufferAccessor =
                        chunk.GetBufferAccessor(CollisionEventBufferType);

                    for (var i = 0; i < chunk.Count; i++)
                    {
                        var collisionEventsBuffer = collisionEventsBufferAccessor[i];

                        for (var j = collisionEventsBuffer.Length - 1; j >= 0; j--)
                        {
                            var collisionEvent = collisionEventsBuffer[j];

                            if (collisionEvent._isStale)
                            {
                                if (collisionEvent.State == PhysicsEventState.Exit)
                                {
                                    collisionEventsBuffer.RemoveAt(j);
                                }
                                else
                                {
                                    collisionEvent.State = PhysicsEventState.Exit;
                                    collisionEventsBuffer[j] = collisionEvent;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}