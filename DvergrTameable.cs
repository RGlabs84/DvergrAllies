using UnityEngine;

namespace DvergrAllies
{
    public class DvergrTameable : Tameable, Hoverable
    {
        public new string GetHoverText()
        {
            string text = base.GetHoverText();

            DvergrProcreation procreation = GetComponent<DvergrProcreation>();
            if (procreation != null && procreation.IsPregnant())
            {
                int timeLeft = procreation.GetPregnancyTimeLeft();
                string timeString = timeLeft > 0 ? $"{timeLeft}s" : "Imminent";
                text += $"\n<color=yellow>Pregnant: {timeString}</color>";
            }

            if (IsHungry())
            {
                text += "\n<color=red>Hungry</color>";
            }
            
            return text;
        }

        public new string GetHoverName()
        {
            return base.GetHoverName();
        }

        // Valheim 1.0 added GetHoverOffset to Hoverable: extra reach, in metres, that Player.FindHoverObject adds
        // to m_maxInteractDistance for this object. Tameable itself is not Hoverable (in any build) - this class
        // declares the interface so its own hover text is the one the cursor shows - so the offset is forwarded
        // to the Character on the same object, exactly as vanilla would read it if this component were absent.
        // Every vanilla implementer returns a serialized m_hoverOffset that defaults to 0.
        private Character _hoverCharacter;

        public float GetHoverOffset()
        {
            if (_hoverCharacter == null) _hoverCharacter = GetComponent<Character>();
            return _hoverCharacter != null ? _hoverCharacter.GetHoverOffset() : 0f;
        }
    }
}

