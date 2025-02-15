using uk.vroad.api;
using uk.vroad.api.events;
using uk.vroad.api.input;
using uk.vroad.api.str;

using UnityEngine;
using UnityEngine.UI;

namespace uk.vroad.ucm
{
    public abstract class UaMenu : MonoBehaviour, LAppState, LAppInput, LBabyl
    {
        protected abstract App App();
       
        protected static readonly Button[] NO_BUTTONS = new Button[0];
        protected static readonly Image[] NO_IMAGES = new Image[0];

        public UHelpPanel menuHelpPanel;
        public Sprite[] buttonIcons;
        public GameObject exitPanel;

        private int prevMenuButtonSelected = -1;
        private bool exitArmed;
        private Button exitButton;
        
        private static bool helpPanelForceVisible;
        
        private AppInputHandler aih;
        protected IBabyl dictionary;
       
        protected int rebuildMenuInTicks;

        /// <summary> The state the game must be in to use this menu </summary>
        protected abstract AppState MenuState();

        protected virtual void Awake()
        {
            aih = App().Aih();
            App().AddEventConsumer(this);

            Button[] menuButtons = menuHelpPanel.menuButtons;
            
            for (int bi = 0; bi < menuButtons.Length; bi++)
            {
                int index = bi; // cannot use loop variable, this is equivalent of 'final int'
                menuButtons[bi].onClick.RemoveAllListeners(); // There might be multiple menus
                menuButtons[bi].onClick.AddListener(delegate { aih.SelectCurrentMenuElement(index); });
            }
            
            if (exitPanel) exitPanel.SetActive(false);
        }

        public bool DeregisterFireMapChange()  { return true; }

        public virtual void TranslationsReady(IBabyl dict)
        {
            dictionary = dict;
        }

        protected string Translation(string key)
        {
            if (dictionary != null) return dictionary.Translation(key);

            return key;
        }
        protected virtual void Start()
        {
            RebuildMenuLater();
        }


        protected virtual void Update()
        {
            if (--rebuildMenuInTicks == 0)  RebuildMenu();
           
            AppPauseMenu menu = aih.CurrentMenu();
            AppDigitalFn[] fna = menu.Functions();
            int currentlySelected = aih.CurrentMenuPosition();

            if (currentlySelected != prevMenuButtonSelected)
            {
                int nb = fna.Length;
                for (int bi = 0; bi < nb; bi++)
                {
                    bool isSel = bi == currentlySelected;

                    menuHelpPanel.menuButtons[bi].gameObject.GetComponent<Image>().fillCenter = isSel;

                    AppDigitalFn fn = fna[bi];

                    if (isSel) menuHelpPanel.titleBar.text = Translation(fn.ToString());

                    UpdateMenuFunction(fn, isSel);
                }

                prevMenuButtonSelected = currentlySelected;
            }

        }


        protected virtual void UpdateMenuFunction(AppDigitalFn fn, bool isSel)
        {
            if (fn == AppDigitalFn.MenuExit)
            {
                if (exitPanel) exitPanel.gameObject.SetActive(isSel);
                else if (exitButton) exitButton.gameObject.SetActive(isSel);
            }
        }

        public void AppStateChanged(AppStateTransition ast)
        {
            RebuildMenuLater();
        }

        protected void RebuildMenuLater()
        {
            rebuildMenuInTicks = 2;
        }

        public static void HelpPanelForceVisible(bool v) {  helpPanelForceVisible = v; }

        protected virtual void RebuildMenu()
        {
            prevMenuButtonSelected = -1;

            if (!exitPanel)
            {
                if (exitButton) Destroy(exitButton.gameObject);
                exitButton = null;
            }

            AppPauseMenu menu = aih.CurrentMenu();
            AppDigitalFn[] fna  = menu.Functions();
            int nf = fna.Length;
            if (nf == 0) menuHelpPanel.gameObject.SetActive(helpPanelForceVisible); // No menu buttons. Perhaps progress bar wants help panel
            
            bool menuActive = App().Asm().CurrentState() == MenuState();
            
            if (!menuActive || nf == 0) return;

            menuHelpPanel.gameObject.SetActive(true); // 
            Button[] menuButtons = menuHelpPanel.menuButtons;
            int ni = buttonIcons.Length;

            bool iconsOnButtons = !dbg_TextOnMenuButtons;

            if (iconsOnButtons)
            {
                for (int fi = 0; fi < nf; fi++)
                {
                    AppDigitalFn fn = fna[fi];
                    Button button = menuButtons[fi];
                    GameObject bgo = button.gameObject;

                    bgo.SetActive(true);

                    Image[] images = bgo.GetComponentsInChildren<Image>();
                    Image iconImage = images.Length > 1 ? images[1] : null;

                    if (iconImage)
                    {
                        for (int ii = 0; ii < ni; ii++)
                        {
                            if (fn.ToString().Equals(buttonIcons[ii].name))
                            {
                                iconImage.gameObject.SetActive(true);
                                iconImage.sprite = buttonIcons[ii];
                                break;
                            }
                        }
                    }
                    
                    BuildSubMenu(fn, button);
                }
            }

            if (dbg_TextOnMenuButtons)
            {
                string[] translatedLabels = new string[nf];

                int longest = 0;
                for (int fi = 0; fi < nf; fi++)
                {
                    translatedLabels[fi] = Translation(fna[fi].ToString());
                    if (translatedLabels[fi].Length > longest) longest = translatedLabels[fi].Length;
                }
                // Don't want best fit, as we want all buttons to have text of the same size
                //
                // A wide 8-character string fits into the button at 36-point on screen width of 1920
                int buttonFontSize = 36 * 8 * Screen.width / (1920 * longest);

                for (int fi = 0; fi < nf; fi++)
                {
                    AppDigitalFn fn = fna[fi];
                    Button button = menuButtons[fi];
                    GameObject bgo = button.gameObject;

                    bgo.SetActive(true);


                    Text label =
                        bgo.GetComponentInChildren<Text>(true); // false / default: works only if component is active
                    if (label)
                    {
                        label.fontSize = buttonFontSize;
                        label.resizeTextForBestFit = false; // Switch off "Best Fit";
                        label.horizontalOverflow = HorizontalWrapMode.Overflow;
                        label.text = translatedLabels[fi];
                    }
                    //*/

                    BuildSubMenu(fn, button);
                }
            }

            for (int fi = nf; fi < menuButtons.Length; fi++)
            {
                menuButtons[fi].gameObject.SetActive(false);
            }
            
        }

        private bool dbg_TextOnMenuButtons;
        
        // When a menu button is built on the main vertical menu, this call allows a secondary
        // horizontal menu to be created

        protected virtual void BuildSubMenu(AppDigitalFn fn, Button button)
        {
            if (fn == AppDigitalFn.MenuExit)
            {
                BuildExitButtons(button);
            }
        }
       
        private void BuildExitButtons(Button button)
        {
            if (exitPanel)
            {
                exitButton = exitPanel.GetComponentInChildren<Button>();
                exitPanel.gameObject.SetActive(false);
            }


            if (!exitButton)
            {
                Sprite[] sprites = { menuHelpPanel.spriteOK, };
                exitButton = BuildSubMenuButtons(button, sprites)[0];

            }
           
            exitButton.onClick.AddListener(delegate { ExitClick(); });
            ExitDisarm();
        }

        // This flag relates to mouse-clicks only
        // When it is true, a  mouse-click on the menu will show a confirm button which then
        // needs another single click to exit.  When it is false, two clicks are needed on the confirm button,
        // the first to arm, then second to fire
        //
        // When using gamepad or keyboard, this flag has no effect - a right move is required to arm, then a trigger to fire.
        protected bool singleClickToFire = true;
        
        private void ExitClick()
        {
            if (singleClickToFire || exitArmed) { ExitConfirm(); return; }

            ExitArm();  // two clicks to exit
        }

        private bool ExitArm()
        {
            exitArmed = true;
            if (exitButton) exitButton.gameObject.GetComponent<Image>().color = Color.red;
            return true;
        }
        private bool ExitDisarm()
        {
            exitArmed = false;
            if (exitButton) exitButton.gameObject.GetComponent<Image>().color = Color.grey;
            return true;
        }
        protected virtual bool MenuItemPressed(AppDigitalFn fn) // Called by keyboard-return or gamepad-trigger
        {
            if (fn == AppDigitalFn.MenuExit && exitArmed) return ExitConfirm();
            
            ExitDisarm();
            return false;
        }

        protected virtual bool HandleMenuLeftRight(AppDigitalFn selFn, bool menuRight)
        {
            bool menuLeft = !menuRight;

            if (selFn == AppDigitalFn.MenuExit)
            {
                if (menuLeft  &&  exitArmed) { return ExitDisarm(); }
                if (menuRight && !exitArmed) { return ExitArm(); }
            }

            return false;
        }

        protected Button[] BuildSubMenuButtons(Button button, Sprite[] icons)
        {
            int nb = icons.Length;
            Button[] subButtons = new Button[nb];
            GameObject buttonGO = button.gameObject;
            RectTransform rt = buttonGO.GetComponent<RectTransform>();

            float bx = rt.position.x;
            float by = rt.position.y;
            float buttonW = rt.rect.width;
            float buttonH = rt.rect.height;
            float buttonWS = buttonW * 0.2f;

            float x = bx + buttonW + buttonWS;
            float y = by;

            for (int qi = 0; qi < nb; qi++)
            {
                Vector3 position = new Vector3(x, y, 0);
                GameObject graphButtonGO = Instantiate(menuHelpPanel.menuIconButtonPrefab, 
                    position, Quaternion.identity, menuHelpPanel.transform);
                RectTransform grrt = graphButtonGO.GetComponentInChildren<RectTransform>();
                grrt.sizeDelta = new Vector2(buttonW, buttonH);
                
                Image[] images = graphButtonGO.GetComponentsInChildren<Image>();
                Image image = images[1];
                image.sprite = icons[qi];
                
                subButtons[qi] = graphButtonGO.GetComponent<Button>();

                x += buttonW + buttonWS;
            }

            return subButtons;
        }
        
        protected Button[] BuildSubMenuButtons(Button button, string[] labels)
        {
            int nb = labels.Length;
            Button[] subButtons = new Button[nb];
            GameObject buttonGO = button.gameObject;
            RectTransform rt = buttonGO.GetComponent<RectTransform>();

            float bx = rt.position.x;
            float by = rt.position.y;
            float buttonW = rt.rect.width;
            float buttonH = rt.rect.height;
            float buttonWS = buttonW * 0.2f;

            float x = bx + buttonW + buttonWS;
            float y = by;

            int longest = 8;
            for (int bi = 0; bi < nb; bi++)
            {
                if (labels[bi].Length > longest) longest = labels[bi].Length;
            }
            // Don't want best fit, as we want all buttons to have text of the same size
            //
            // A wide 8-character string fits into the button at 36-point on screen width of 1920
            int buttonFontSize = 36 * 8 * Screen.width / (1920 * longest);

            
            for (int bi = 0; bi < nb; bi++)
            {
                Vector3 position = new Vector3(x, y, 0);
                GameObject subButtonGO = Instantiate(menuHelpPanel.menuTextButtonPrefab, 
                    position, Quaternion.identity, menuHelpPanel.transform);
                Button subButton = subButtonGO.GetComponent<Button>();
                subButtons[bi] = subButton;
                
                RectTransform grrt = subButtonGO.GetComponentInChildren<RectTransform>();
                grrt.sizeDelta = new Vector2(buttonW, buttonH);
                
                Text buttonLabel = subButtonGO.GetComponentInChildren<Text>();
                buttonLabel.text = labels[bi];

                buttonLabel.fontSize = buttonFontSize;
                buttonLabel.resizeTextForBestFit = false; // Switch off "Best Fit";
                buttonLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

               

                x += buttonW + buttonWS;
            }

            return subButtons;
        }

        public bool AppInputAnalogEvent(AppAnalogFn afn, double value) { return false; }

        public bool AppInputDigitalEvent(AppDigitalFn fn, bool isPressed)
        {
            if (!isPressed) return false;

            AppState a = App().Asm().CurrentState();
            AppState b = MenuState();
            
            if (App().Asm().CurrentState() != MenuState()) return false;

            // Resume is handled according to which menu is active
            // For MenuMap it is handled in LevelManager, for Trip and Play in GameSimControl.Resume()
            // which calls through to GameStateMachine.Resume
            
            // Mission, Controls and Graphics are 'passive' - see Fixed Update
            bool menuLeft = fn == AppDigitalFn.MenuLeft;
            bool menuRight = fn == AppDigitalFn.MenuRight;
            if (menuLeft || menuRight)
            {
                AppPauseMenu menu = aih.CurrentMenu();
                AppDigitalFn[] fna = menu.Functions();
                int selFnI = aih.CurrentMenuPosition();
                if (selFnI >= 0 && selFnI < fna.Length)
                {
                    AppDigitalFn menuFn = fna[selFnI];
                    bool handled = HandleMenuLeftRight(menuFn, menuRight);
                    UpdateMenuFunction(menuFn, true);
                    return handled;
                }
            }

            // The menu select action will come through to here, but it is handled in GamePlay
            // GamePlay will push through the action for the currently selected button immediately afterwards
            if (fn == AppDigitalFn.MenuSelect) return true;
            
           
            return MenuItemPressed(fn);
        }

        private bool ExitConfirm()
        {
            UExitHandler.AppExitStatic(App());
                
            // App().Asc().Exit();

            // In some modes, we need to resume the simulation engine for a single
            // step so that it can kill all the simulation workers and the master thread cleanly
            //
            // otherwise UExitHandler.FixedUpdate will stick waiting for simulation threads to die
            //
            // This is almost the same as pressing the resume button after the exit button
            App().Ew().FireAppDigitalEvent(AppDigitalFn.MenuResume, true);
     
            return true;
        }

    }
}
