using UnityEngine;

namespace PrehistoricSurvival.Crafting
{
    /// <summary>
    /// Simple component to tag a GameObject as a crafting station.
    /// When the player enters the trigger, CraftingSystem.SetNearbyStation is called.
    ///
    /// NOTE: this must live in a RUNTIME assembly. It used to be defined inside
    /// Assets/Editor/ProjectSetup.cs, so the Campfire prefab baked in a reference
    /// to an editor-only script — which Unity reports as "The referenced script on
    /// this Behaviour (Game Object 'Campfire') is missing!" when the prefab is
    /// loaded at runtime.
    /// </summary>
    public class CraftingStationTrigger : MonoBehaviour
    {
        public string stationTag = "campfire";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var cs = CraftingSystem.Instance;
                if (cs != null) cs.SetNearbyStation(stationTag);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var cs = CraftingSystem.Instance;
                if (cs != null) cs.SetNearbyStation("");
            }
        }
    }
}
