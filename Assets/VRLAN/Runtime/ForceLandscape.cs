using UnityEngine;

namespace VRLAN.Network
{
    // Coloque num GameObject da cena do TABLET (TabletScene).
    // Força paisagem (Landscape Left) de forma DETERMINÍSTICA — aplica na hora,
    // sem depender do sensor (diferente de AutoRotation, que só reavalia ao girar o aparelho).
    // Não afeta o Quest (script só existe na cena do tablet).
    public class ForceLandscape : MonoBehaviour
    {
        private void Awake()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
