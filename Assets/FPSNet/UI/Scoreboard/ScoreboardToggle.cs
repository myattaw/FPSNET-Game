using UnityEngine;

namespace FPSNet.UI.Scoreboard
{
    public class ScoreboardToggle : MonoBehaviour
    {
        public GameObject scoreboardPanel;

        void Start()
        {
            scoreboardPanel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                scoreboardPanel.SetActive(true);
            }

            if (Input.GetKeyUp(KeyCode.Tab))
            {
                scoreboardPanel.SetActive(false);
            }
        }
    }
}
