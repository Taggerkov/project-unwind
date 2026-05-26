using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace Systems.Common
{
    /// <summary>
    /// ScriptableObject that registers all playable combatants available in the game.
    /// Create via <c>Unwind Database → Combatant Database</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantDatabaseSO", menuName = "Unwind Database/Combatant Database")]
    public class CombatantDatabaseSO : ScriptableObject
    {
    }
}
