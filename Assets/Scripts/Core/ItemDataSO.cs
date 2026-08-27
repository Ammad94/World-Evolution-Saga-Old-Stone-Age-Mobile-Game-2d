using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// ScriptableObject wrapper for ItemData, allowing item creation via the Unity Editor.
    /// Create via: Assets → Create → PrehistoricSurvival → Item Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "PrehistoricSurvival/Item Data")]
    public class ItemDataSO : ScriptableObject
    {
        public ItemData data;
    }
}
