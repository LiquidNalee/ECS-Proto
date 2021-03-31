using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Tags.Selection;
using Unity.Entities;
using UnityEngine;

namespace Systems.Controls
{
    public class SelectionSystem : ConsumeSingleEventSystemBase<LeftClickEvent>
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnStartRunning() {
            _ecbSystem =
                World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnEvent(LeftClickEvent e) {
            Debug.Log("Event Captured");
            Debug.Log(EntityManager.GetName(e.Entity));
            if (EntityManager.HasComponent<SelectableTag>(e.Entity))
            {
                Debug.Log("Has SelectableTag");
                _ecbSystem.CreateCommandBuffer()
                          .AddComponent<SelectedTag>(e.Entity);
            }
        }
    }
}