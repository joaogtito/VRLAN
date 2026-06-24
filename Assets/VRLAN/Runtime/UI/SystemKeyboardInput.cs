using TMPro;
using UnityEngine;

namespace VRLAN.Network.UI
{
    // No Quest/Android em XR (Canvas World Space), um TMP_InputField NÃO abre o teclado do
    // sistema sozinho (o XRI não tem o "require system keyboard" da Meta). Este componente abre
    // o teclado nativo via TouchScreenKeyboard do Unity quando o campo é selecionado, e espelha
    // o texto digitado de volta pro campo.
    //
    // Uso: adicione no MESMO GameObject do TMP_InputField (ex.: o campo de nome da QuestLobby).
    // Também dá pra abrir manualmente ligando um Button → OpenKeyboard().
    //
    // Portátil (não depende do Meta XR SDK). Se em algum firmware o teclado nativo não subir,
    // a alternativa é o OVRVirtualKeyboard do Meta XR Core SDK (aí precisa importar o SDK).
    [RequireComponent(typeof(TMP_InputField))]
    public class SystemKeyboardInput : MonoBehaviour
    {
        [SerializeField] private TouchScreenKeyboardType keyboardType = TouchScreenKeyboardType.Default;
        [SerializeField] private string placeholder = "Digite seu nome";

        private TMP_InputField _input;
        private TouchScreenKeyboard _keyboard;

        private void Awake()
        {
            _input = GetComponent<TMP_InputField>();
            _input.onSelect.AddListener(OnFieldSelected);
        }

        private void OnDestroy()
        {
            if (_input != null) _input.onSelect.RemoveListener(OnFieldSelected);
        }

        private void OnFieldSelected(string _) => OpenKeyboard();

        // Público pra também poder ser disparado por um Button no Inspector.
        public void OpenKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                Debug.LogWarning("[SystemKeyboardInput] TouchScreenKeyboard não suportado nesta plataforma.");
                return;
            }
            if (_keyboard != null && _keyboard.active) return;

            _keyboard = TouchScreenKeyboard.Open(
                text: _input.text,
                keyboardType: keyboardType,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false,
                textPlaceholder: placeholder);
        }

        private void Update()
        {
            if (_keyboard == null) return;

            // Enquanto o teclado está aberto, espelha o texto no campo em tempo real.
            if (_keyboard.active)
            {
                _input.text = _keyboard.text;
                return;
            }

            // Teclado fechou: aplica o resultado em "Done", mantém o atual nos demais casos.
            if (_keyboard.status == TouchScreenKeyboard.Status.Done)
                _input.text = _keyboard.text;

            _keyboard = null;
        }
    }
}
