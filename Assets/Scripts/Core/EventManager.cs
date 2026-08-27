using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Lightweight event bus for decoupled communication between systems.
    /// Usage: EventManager.TriggerEvent("PlayerDied", payload);
    /// </summary>
    public static class EventManager
    {
        // eventName -> list of callbacks accepting an optional object payload
        private static readonly Dictionary<string, List<Action<object>>> _listeners
            = new Dictionary<string, List<Action<object>>>();

        /// <summary>Subscribe to an event by name.</summary>
        public static void Subscribe(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            if (!_listeners.TryGetValue(eventName, out var list))
            {
                list = new List<Action<object>>();
                _listeners[eventName] = list;
            }
            list.Add(callback);
        }

        /// <summary>Unsubscribe from an event.</summary>
        public static void Unsubscribe(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (_listeners.TryGetValue(eventName, out var list))
                list.Remove(callback);
        }

        /// <summary>Fire an event, passing an optional payload object.</summary>
        public static void TriggerEvent(string eventName, object payload = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (!_listeners.TryGetValue(eventName, out var list)) return;

            // Iterate over a copy so listeners can safely unsubscribe mid-fire.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i]?.Invoke(payload);
            }
        }

        /// <summary>Clear every listener (call on scene unload to prevent stale refs).</summary>
        public static void ClearAll()
        {
            _listeners.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Strongly-typed event names (avoids magic strings)
    // ------------------------------------------------------------------
    public static class GameEvents
    {
        public const string PlayerMoved        = "PlayerMoved";
        public const string PlayerDied         = "PlayerDied";
        public const string PlayerEnteredWater = "PlayerEnteredWater";
        public const string PlayerExitedWater  = "PlayerExitedWater";
        public const string SeasonChanged      = "SeasonChanged";
        public const string DayNightChanged    = "DayNightChanged";
        public const string ItemCollected      = "ItemCollected";
        public const string ItemConsumed       = "ItemConsumed";
        public const string ItemCrafted        = "ItemCrafted";
        public const string AnimalKilled       = "AnimalKilled";
        public const string TileDestroyed      = "TileDestroyed";
        public const string WaypointSet        = "WaypointSet";
        public const string WaypointCleared    = "WaypointCleared";
        public const string WeatherChanged     = "WeatherChanged";
    }
}
