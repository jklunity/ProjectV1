
using uk.vroad.api;
using uk.vroad.api.input;
using uk.vroad.api.xmpl;
using uk.vroad.apk;

using uk.vroad.spc;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace uk.vroad.uspc
{
    public class UDrivingKeys : MonoBehaviour
    {
        private KHash<KeyControl, AppButton> keyPressToButtonOn = new KHash<KeyControl, AppButton>();
        private KHash<AppAnalogFn, KeyPair> functionToKeyPair =  new KHash<AppAnalogFn, KeyPair>();

        private App app;
        void Awake()
        {
            app = ExampleApp.AwakeInstance();
        }

        protected App App() { return app; }
        void Start()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;


            functionToKeyPair.Put(DrivingAnalogFn.SpecSpeed, new KeyPair(kb.upArrowKey, kb.downArrowKey));
            functionToKeyPair.Put(DrivingAnalogFn.SpecLane, new KeyPair(kb.rightArrowKey, kb.leftArrowKey));

            keyPressToButtonOn.Put(kb.spaceKey, GamePadButtons.JoyL);
            keyPressToButtonOn.Put(kb.enterKey, GamePadButtons.JoyR);
            keyPressToButtonOn.Put(kb.numpadEnterKey, GamePadButtons.JoyR);
            keyPressToButtonOn.Put(kb.backspaceKey, GamePadButtons.ShoulderR);
        }

        void Update()
        {

            Keyboard kb = Keyboard.current;
            if (kb != null) HandleKeyboard(kb);
        }

        private void HandleKeyboard(Keyboard kb)
        {
            AppInputHandler aih = App().Aih();

            foreach (AppAnalogFn afn in functionToKeyPair.Keys)
            {
                KeyPair keyPair = functionToKeyPair.Get(afn);

                // This sets the value to zero if no keys pressed, overriding any value from gamepad
                // gplay.FireAnalogEvent(afn, keyPair.posKey.isPressed ? 1.0 : keyPair.negKey.isPressed ? -1.0 : 0);

                if (keyPair.posKey.isPressed) aih.FireAnalogEvent(afn, 1.0);
                else if (keyPair.negKey.isPressed) aih.FireAnalogEvent(afn, -1.0);
                //else if (Gamepad.current == null) gplay.FireAnalogEvent(afn, 0);
                else
                {
                    if (keyPair.posKey.wasReleasedThisFrame) aih.FireAnalogEvent(afn, 0);
                    if (keyPair.negKey.wasReleasedThisFrame) aih.FireAnalogEvent(afn, 0);
                }
            }

            foreach (KeyControl kc in keyPressToButtonOn.Keys)
            {
                if (kc.wasPressedThisFrame)
                {
                    AppButton button = keyPressToButtonOn.Get(kc);

                    aih.FireDigitalEvent(button, true);
                }
            }

        }

        public class KeyPair
        {
            public readonly KeyControl posKey;
            public readonly KeyControl negKey;

            internal KeyPair(KeyControl p, KeyControl n)
            {
                posKey = p;
                negKey = n;
            }
        }
    }


}
