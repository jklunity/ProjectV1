using System;
using uk.vroad.api;
using uk.vroad.api.etc;
using uk.vroad.api.sim;
using uk.vroad.api.str;
using uk.vroad.api.xmpl;
using uk.vroad.apk;
using uk.vroad.pac;
using uk.vroad.ucm;
using uk.vroad.uspc;
using uk.vroad.xmpl;
using UnityEditor;
using UnityEngine;

namespace uk.vroad.Editor
{
    public class DrivingExampleWindow : VRoadProWindow
    {
        #region STRING_CONSTANTS
        private const string WINDOW_TITLE = "Driving Example";
        private const string MENU_TITLE = "Tools/Global Roads and Traffic Pro/"+WINDOW_TITLE;
        private const string SCENE_DRIVING_EXAMPLE = "DrivingExample";
        private const string URL_LABEL = WINDOW_TITLE;
        private const string URL_PAGE = "https://vroad.uk/doc/driving/";
        
        private const string REF_PLAYERCAR = "UPlayerCar";

        private const string KEY_HELP_1 = "Use arrow keys to drive car, or use GamePad";
        private const string KEY_HELP_2 = "Use enter and space keys to re-start car";
        
        #endregion
        
        [MenuItem(MENU_TITLE, priority = 13)]
        static void Init()
        {
            KEnv.Awake();
           
            DrivingExampleWindow window = HasOpenInstances<VRoadWindow>()?
                GetWindow<DrivingExampleWindow>(WINDOW_TITLE, true, typeof(VRoadWindow)):
                HasOpenInstances<VRoadEditorWindow>()?
                    GetWindow<DrivingExampleWindow>(WINDOW_TITLE, true, GetWindow<VRoadEditorWindow>().GetType()):
                    GetWindow<DrivingExampleWindow>(WINDOW_TITLE);
            window.SetupReferencesInEditMode();
            window.ClearRunTraffic();
            window.Show();
            MeshTools.VRoadRoot(); // Check VRoad package location relative to Assets
            InitScenes();
        }
        
       
        private UPlayerCar playerCar;
        private URunTraffic runTraffic;
        private UMapMeshExample mapMesh;
        private UPlaySimExample playSim;
        private App app;
        
        private bool isPlaying;
        private int traffic;
        private bool showMeshOptions; // if you change this, close window/tab and re-open
        private bool createMultipleMeshes;
        private bool playActionEnabled;
        private bool validVRoadFileSelected;
        private string vroadFilePath;
        private string vroadFileName;

        #region references

        
        void SetupReferencesInEditMode()
        {
            missingReferences = SC.N;

            UPlayerCar[] playerCars = Resources.FindObjectsOfTypeAll<UPlayerCar>();
            if (playerCars.Length == 1) playerCar = playerCars[0];
            else missingReferences += REF_PLAYERCAR;
            
            URunTraffic[] runTraffics = Resources.FindObjectsOfTypeAll<URunTraffic>();
            if (runTraffics.Length == 1) runTraffic = runTraffics[0];
            else missingReferences += EDSF.REF_RUNTRAFFIC;

            UMapMeshExample[] mapMeshes = Resources.FindObjectsOfTypeAll<UMapMeshExample>();
            if (mapMeshes.Length == 1) { mapMesh = mapMeshes[0]; }
            else missingReferences += EDSF.REF_MAPMESH;

            UaStateHandler[] stateHandlers = Resources.FindObjectsOfTypeAll<UaStateHandler>();
            if (stateHandlers.Length != 1) missingReferences += EDSF.REF_APPSTATE;

            UExitHandler[] exitHandlers = Resources.FindObjectsOfTypeAll<UExitHandler>();
            if (exitHandlers.Length != 1) missingReferences += EDSF.REF_EXIT;

            UaMouse[] mouseHandlers = Resources.FindObjectsOfTypeAll<UaMouse>();
            if (mouseHandlers.Length != 1) missingReferences += EDSF.REF_MOUSE;

            UaCamControllerMain[] camControllers = Resources.FindObjectsOfTypeAll<UaCamControllerMain>();
            if (camControllers.Length != 1) missingReferences += EDSF.REF_CAM;
          
            UPlaySimExample[] playSims = Resources.FindObjectsOfTypeAll<UPlaySimExample>();
            if (playSims.Length == 1) playSim = playSims[0];
        }

        void SetupReferencesInPlayMode()
        {
            missingReferences = SC.N;

            playerCar = UPlayerCar.MostRecentInstance;
            if (playerCar != null)
            {
                UaCamControllerMain cam = UaCamControllerMain.MostRecentInstance;
                cam.TrackThis(playerCar.gameObject);
            }
            else missingReferences += REF_PLAYERCAR;

            runTraffic = URunTraffic.MostRecentInstance;
            if (runTraffic == null) missingReferences += EDSF.REF_RUNTRAFFIC;

            mapMesh = UMapMeshExample.MostRecentInstance;
            if (mapMesh == null) missingReferences += EDSF.REF_MAPMESH;
            else
            {
                mapMesh.CreateMultipleMeshes = this.createMultipleMeshes;
            }

            if (UaStateHandler.MostRecentInstance == null) missingReferences += EDSF.REF_APPSTATE;
            if (UaMouse.MostRecentInstance == null) missingReferences += EDSF.REF_MOUSE;
            if (UaCamControllerMain.MostRecentInstance == null) missingReferences += EDSF.REF_CAM;


            playSim = UPlaySimExample.MostRecentInstance;

            if (playSim != null)
            {
                playSim.SetTargetRealTimeMultiplier(2);
                playSim.SetFfwd(false);
            }

            traffic = 0;
        }

        bool AllReferencesFound() { return missingReferences.Length == 0; }

        #endregion
        
        void ClearRunTraffic()
        {
            if (runTraffic != null) runTraffic.SetupTraffic(null);
        }
        
        private void SetupRunTraffic(string path)
        {
            if (AllReferencesFound())
            {
                if (runTraffic == null) missingReferences += EDSF.REF_RUNTRAFFIC;

                else runTraffic.SetupTraffic(path);
            }
        }

        void Update()
        {
            if (! IsWindowActiveInCurrentScene()) return;
            
            // When build is complete (Meshes ready) set vroad file path so that it shows in browse box on traffic tab
            if (EditorApplication.isPlaying && app == null && AllReferencesFound() && mapMesh.MeshesReady())
            {
                app = ExampleApp.AwakeInstance(); // this does not go in Awake, because this is an EditorWindow
                app.Sim().SetTimeStepsPerSecond(10);
            }

            if (!EditorApplication.isPlaying && app != null)
            {
                app = null;
            }

        }

        void OnGUI()
        {
            if (trySwitchScene) SwitchSceneTo(SCENE_DRIVING_EXAMPLE, EDSF.SCENE_PATH_REL_PRO);

            if (IsWindowActiveInCurrentScene())
            {
                bool nowPlaying = EditorApplication.isPlaying;
                bool playStateChanged = false;
                if (nowPlaying != isPlaying)
                {
                    isPlaying = nowPlaying;
                    playStateChanged = true;

                    // if (!isPlaying) VRoad.FreeTilesClearCache();
                }

                if (playStateChanged || hasSceneChanged)
                {
                    if (isPlaying) { SetupReferencesInPlayMode(); }
                    else
                    {
                        SetupReferencesInEditMode();

                        if (AllReferencesFound())
                        {
                            bool inDrivingScene = SCENE_DRIVING_EXAMPLE.Equals(currentScene);

                            if (inDrivingScene) SetupRunTraffic(vroadFileName);
                            else SetupRunTraffic(SC.N);

                        }

                        if (playSim != null) playSim.RunTraffic(true);
                    }
                }
            }
            

            float line = 2f;
            float nl = showMeshOptions? 2.5f: 0.5f;
            float columnWidth = Math.Min(300, position.width - (2 * marginX));
            playActionEnabled = false;
            bool isActive = IsWindowActiveInCurrentScene(); 
            if (hasSceneChanged) ValidateVRoadFile(vroadFilePath);

            GUILayout.BeginArea(AreaRect(line, nl, columnWidth), EditorStyles.helpBox);
            {
                playActionEnabled = validVRoadFileSelected;

                GUI.enabled = isActive;
                GUILayout.BeginHorizontal();
                bool validFile = string.IsNullOrWhiteSpace(vroadFileName);
                string displayedLabel = validFile ? EDSC.HINT_VROAD : vroadFileName;
                NoEditTextField(displayedLabel, columnWidth - 60);
                GUI.enabled = isActive && !isPlaying;
                if (GUILayout.Button(SC.ELLIPSIS, GUILayout.MinHeight(16))) SelectVRoadFile();
                GUI.enabled = isActive;
                GUILayout.EndHorizontal();

                if (showMeshOptions)
                {
                    var contentMM = new GUIContent(EDSC.CREATE_MULTIPLE_MESHES, EDSC.CREATE_MULTIPLE_MESHES_TT);
                    createMultipleMeshes = EditorGUILayout.Toggle(contentMM, createMultipleMeshes);
                }
            }
            GUILayout.EndArea();

            line += nl + 2.5f;
            nl = 3f;
            GUILayout.BeginArea(VRoadWindow.AreaRect(line, nl, columnWidth), GUIStyle.none);
            {
                ActionButtonOrReferenceWarning();

                if (isPlaying && app != null)
                {
                   GUI.enabled = true;
                    
                    EditorGUILayout.Space(0.4f*lineY);
                    EditorGUILayout.LabelField(new GUIContent(KEY_HELP_1), GUILayout.MaxWidth(columnWidth));
                    EditorGUILayout.Space(0.4f*lineY);
                    EditorGUILayout.LabelField(new GUIContent(KEY_HELP_2), GUILayout.MaxWidth(columnWidth));
                    EditorGUILayout.Space(0.4f*lineY);

                    GUILayout.BeginHorizontal();
                    {
                        GUILayoutOption optBW = GUILayout.MaxWidth(24); // columnWidth / 4);
                        GUILayoutOption optH = GUILayout.Height(20);

                        GUILayout.Label(EDSC.TRAFFIC_VAR, optH);
                        if (GUILayout.Button(SC.MI, optBW, optH))
                        {
                            traffic--;
                            VRoad.AdvanceReleaseTime(-60);
                        }

                        GUILayout.Label(SC.N + traffic, EDSF.STYLE_TEXTFIELD, optBW, optH);
                        if (GUILayout.Button(SC.PL, optBW, optH))
                        {
                            traffic++;
                            VRoad.AdvanceReleaseTime(+60);
                        }
                    }
                    GUILayout.EndHorizontal();

                   
                }
            }
            GUILayout.EndArea();
            
            Footer(GRAT_PRO_SUBTITLE, URL_LABEL, URL_PAGE);
            
            hasSceneChanged = false;
        }
        
        void SelectVRoadFile()
        {
            validVRoadFileSelected = false;
            string dir = KEnv.VroadWriteDir();
            string suffix = EDSF.SUFFIX_VROAD + SC.CM + EDSF.SUFFIX_VROADM;
            string path = EditorUtility.OpenFilePanel(EDSC.VROAD_FILE_DESC, dir, suffix); // desktop or mobile
            if (string.IsNullOrEmpty(path)) return;

            ValidateVRoadFile(path);
        }

        void ValidateVRoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            
            KFile file = new KFile(path);
            
            if (file.Exists())
            {
                string[] ccsa = FilenameWrapper.VROAD.CountryCitySuburb(file.FileName());
                if (ccsa.Length == 4)
                {
                    int ha = KTools.ParseInt(ccsa[3]);
                    validVRoadFileSelected = ha > 0;
                }
            }

            if (validVRoadFileSelected)
            {
                vroadFileName = file.FileName();
                vroadFilePath = path;
                SetupRunTraffic(vroadFileName);
            }
            else
            {
                ClearRunTraffic();
                vroadFileName = SC.N;
                vroadFilePath = SC.N;
            }
        }
        
        private void ActionButtonOrReferenceWarning()
        {
            GUI.enabled = true;

            string expectedScene = SCENE_DRIVING_EXAMPLE;
            bool correctScene = currentScene.Equals(expectedScene);

            if (!correctScene)
            {
                EditorGUILayout.HelpBox(KFormat.Sprintf(EDSC.OPEN_SCENE_TO_ACTIVATE, expectedScene), MessageType.Info);
                
                if (GUILayout.Button(KFormat.Sprintf(EDSC.OPEN_SCENE, expectedScene), GUILayout.Height(helpBoxHeight)))
                {
                    trySwitchScene = true;
                }
            }
            else if (missingReferences.Length > 0)
            {
                EditorGUILayout.HelpBox(KFormat.Sprintf(EDSC.UI_MISSING, missingReferences), MessageType.Warning);
            }
            else if (isPlaying)
            {
                if (app == null)
                {
                    string msg = runTraffic.ProgressActivity();
                    EditorGUILayout.HelpBox(msg, MessageType.Info);
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button(VRoadWindow.HintFormat(EDSC.RUNNING), GUILayout.Height(helpBoxHeight));
                }
            }
            else
            {
                GUI.enabled = playActionEnabled;

                string hint = EDSC.HINT_TRAFFIC;
                string action = EDSC.ACTION_TRAFFIC;
                
                string buttonAction = KFormat.Sprintf(EDSC.PLAY_SCENE, expectedScene, action);

                string buttonTooltip = EDSC.TOOLTIP_TRAFFIC;
                GUIContent buttonContent = playActionEnabled ? new GUIContent(buttonAction, buttonTooltip)
                    : new GUIContent(VRoadWindow.HintFormat(hint));

                if (GUILayout.Button(buttonContent, GUILayout.Height(helpBoxHeight)))
                {
                    EditorApplication.isPlaying = true;
                }
            }

            GUI.enabled = true;
        }

        private void OnFocus()
        {
            if (string.IsNullOrEmpty(vroadFileName))
            {
                string mruFilePath = KEnv.VroadWriteDir() + VRoadWindow.mruFileRoot + SC.SUFFIX_DOT_VROAD;
                ValidateVRoadFile(mruFilePath);
            }
        }

        public override bool IsWindowActiveInCurrentScene()
        {
            return SCENE_DRIVING_EXAMPLE.Equals(currentScene);
        }

        void OnSelectionChange()
        {
            if (! IsWindowActiveInCurrentScene()) return;
            
            UaCamControllerMain cam = UaCamControllerMain.MostRecentInstance;
            UaBotHandler bh = UaBotHandler.Instance;
            if (cam == null || bh == null) return;
            
            cam.autoTrack = false;
            GameObject[] goa = Selection.gameObjects;
            if (goa == null || goa.Length == 0)
            {
                // stop tracking any previous Bot
                cam.TrackThis(null);
            }
            else // there is at least one selection
            {
                GameObject primarySelection = goa[0];

                // Is this a vehicle / pedestrian?
                IBit bit = bh.LookupBit(primarySelection);
                if (bit != null) cam.TrackThis(primarySelection);
                
                // If the primary selection is something else (e.g. the camera)
                // then do not stop tracking the previous bot, so that it is possible
                // to adjust public test variables on the camera while tracking
            }
        }

    }

}