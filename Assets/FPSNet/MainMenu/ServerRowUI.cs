using TMPro;
using UnityEngine;

namespace FPSNet.MainMenu
{
    public class ServerRowUI : MonoBehaviour
    {
        public TMP_Text serverNameText;
        public TMP_Text modeText;
        public TMP_Text playersText;
        public TMP_Text pingText;

        public void SetData(string name, string mode, string players, int ping)
        {
            serverNameText.text = name;
            modeText.text = mode;
            playersText.text = players;

            // Set ping text
            pingText.text = ping + "ms";

            // Color logic
            if (ping < 30)
            {
                pingText.color = Color.green;
            }
            else if (ping < 70)
            {
                pingText.color = new Color(1f, 0.64f, 0f); // orange
            }
            else
            {
                pingText.color = Color.red;
            }
        }
    }
}
