using UnityEngine;
using UnityEngine.UI;

namespace Vamsurlike.UI
{
    internal static class FilledImageUtility
    {
        public static void SetAmount(Image image, float amount)
        {
            if (image == null) return;
            image.fillAmount = Mathf.Clamp01(amount);
        }
    }
}
