using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Combat.RoundCounter
{
    public class CombatUIRoundCounter : MonoBehaviour
    {
        
        [SerializeField] private Image round1Indicator;
        [SerializeField] private Image round2Indicator;
        
        public void SetRound1Won()
        {
            round1Indicator.CrossFadeAlpha(1f, 0.5f, false);
        }
        
        public void SetRound2Won()
        {
            round2Indicator.CrossFadeAlpha(1f, 0.5f, false);
        }

        public void ResetRounds()
        {
            round1Indicator.CrossFadeAlpha(0f, 0f, false);
            round2Indicator.CrossFadeAlpha(0f, 0f, false);
        }
    }
}
