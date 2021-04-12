﻿using Components.Events.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Systems.Events.Physics
{
    [UpdateInGroup(typeof(EventProducerSystemGroup))]
    public class TriggerEventsSystem : SystemBase
    {
        private BuildPhysicsWorld _buildPhysicsWorldSystem;
        private StepPhysicsWorld _stepPhysicsWorldSystem;
        private EntityQuery _triggerEventsBufferEntityQuery;

        protected override void OnCreate()
        {
            _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();

            var queryDesc = new EntityQueryDesc
                            {
                                All = new ComponentType[]
                                      {
                                          typeof(PhysicsCollider),
                                          typeof(StatefulTriggerEvent)
                                      }
                            };

            _triggerEventsBufferEntityQuery = GetEntityQuery(queryDesc);
        }

        protected override void OnUpdate()
        {
            Dependency = new TriggerEventsPreProcessJob
                         {
                             TriggerEventBufferType =
                                 GetBufferTypeHandle<StatefulTriggerEvent>()
                         }.ScheduleParallel(_triggerEventsBufferEntityQuery, Dependency);

            Dependency = new TriggerEventsJob
                         {
                             TriggerEventBufferFromEntity =
                                 GetBufferFromEntity<StatefulTriggerEvent>()
                         }.Schedule(
                    _stepPhysicsWorldSystem.Simulation,
                    ref _buildPhysicsWorldSystem.PhysicsWorld,
                    Dependency
                );

            Dependency = new TriggerEventsPostProcessJob
                         {
                             TriggerEventBufferType =
                                 GetBufferTypeHandle<StatefulTriggerEvent>()
                         }.ScheduleParallel(_triggerEventsBufferEntityQuery, Dependency);
        }

        // todo; can maybe optimize by checking if chunk has changed?
        [BurstCompile]
        private struct TriggerEventsPreProcessJob : IJobChunk
        {
            public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var triggerEventsBufferAccessor =
                    chunk.GetBufferAccessor(TriggerEventBufferType);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var triggerEventsBuffer =
                        triggerEventsBufferAccessor[i];

                    for (var j = triggerEventsBuffer.Length - 1; j >= 0; j--)
                    {
                        var triggerEventElement = triggerEventsBuffer[j];
                        triggerEventElement._isStale = true;
                        triggerEventsBuffer[j] = triggerEventElement;
                    }
                }
            }
        }

        [BurstCompile]
        private struct TriggerEventsJob : ITriggerEventsJob
        {
            public BufferFromEntity<StatefulTriggerEvent> TriggerEventBufferFromEntity;

            public void Execute(TriggerEvent triggerEvent)
            {
                ProcessForEntity(triggerEvent.EntityA, triggerEvent.EntityB);
                ProcessForEntity(triggerEvent.EntityB, triggerEvent.EntityA);
            }

            private void ProcessForEntity(Entity entity, Entity otherEntity)
            {
                if (TriggerEventBufferFromEntity.HasComponent(entity))
                {
                    var triggerEventBuffer = TriggerEventBufferFromEntity[entity];

                    var foundMatch = false;

                    for (var i = 0; i < triggerEventBuffer.Length; i++)
                    {
                        var triggerEvent = triggerEventBuffer[i];

                        // If entity is already there, update to Stay
                        if (triggerEvent.Entity == otherEntity)
                        {
                            foundMatch = true;
                            triggerEvent.State = PhysicsEventState.Stay;
                            triggerEvent._isStale = false;
                            triggerEventBuffer[i] = triggerEvent;

                            break;
                        }
                    }

                    // If it's a new entity, add as Enter
                    if (!foundMatch)
                        triggerEventBuffer.Add(
                                new StatefulTriggerEvent
                                {
                                    Entity = otherEntity,
                                    State = PhysicsEventState.Enter,
                                    _isStale = false
                                }
                            );
                }
            }
        }

        [BurstCompile]
        private struct TriggerEventsPostProcessJob : IJobChunk
        {
            public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var triggerEventsBufferAccessor =
                    chunk.GetBufferAccessor(TriggerEventBufferType);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var triggerEventsBuffer = triggerEventsBufferAccessor[i];

                    for (var j = triggerEventsBuffer.Length - 1; j >= 0; j--)
                    {
                        var triggerEvent = triggerEventsBuffer[j];

                        if (triggerEvent._isStale)
                        {
                            if (triggerEvent.State == PhysicsEventState.Exit)
                            {
                                triggerEventsBuffer.RemoveAt(j);
                            }
                            else
                            {
                                triggerEvent.State = PhysicsEventState.Exit;
                                triggerEventsBuffer[j] = triggerEvent;
                            }
                        }
                    }
                }
            }
        }
    }
}