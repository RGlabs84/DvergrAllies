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
    }
}

