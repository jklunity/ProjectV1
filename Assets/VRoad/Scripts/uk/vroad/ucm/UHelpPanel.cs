using uk.vroad.api.str;
using uk.vroad.apk;
using UnityEngine;
using UnityEngine.UI;

namespace uk.vroad.ucm
{
    public class UHelpPanel : MonoBehaviour
    {
        public GameObject menuIconButtonPrefab;
        public GameObject menuTextButtonPrefab;
        public Text titleBar;
        public Button[] menuButtons;
        public Sprite spriteOK;

        public void Resume()
        {
            if (menuButtons.Length > 0) menuButtons[0].onClick.Invoke();
        }

    }
}