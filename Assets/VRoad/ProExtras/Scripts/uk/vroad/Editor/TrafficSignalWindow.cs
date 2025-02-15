using System;
using uk.vroad.api;
using uk.vroad.api.etc;
using uk.vroad.api.geom;
using uk.vroad.api.map;
using uk.vroad.api.str;
using uk.vroad.api.xmpl;
using uk.vroad.apk;
using uk.vroad.pac;
using uk.vroad.ucm;
using uk.vroad.xmpl;
using UnityEditor;
using UnityEngine;

namespace uk.vroad.Editor
{
    public class TrafficSignalWindow : VRoadProWindow
    {
        #region STRING_CONSTANTS
        private const string WINDOW_TITLE = "Traffic Signal Controller";
        private const string MENU_TITLE = "Tools/Global Roads and Traffic Pro/"+WINDOW_TITLE;
        private const string SCENE_TRAFFIC_SIGNAL = "TrafficSignals";
        private const string URL_LABEL = WINDOW_TITLE;
        private const string URL_PAGE = "https://vroad.uk/doc/signals/";
        
        private static readonly string[] speedDisplay = {">>", "x4", "x2", "x1",};
        private static readonly int[] xrtValues =       { 10,    4,    2,    1,};
        private static readonly int[] tspsValues =      {  5,    5,   10,   20,};
        private static int tspsi = 1;

        #endregion
        
        [MenuItem(MENU_TITLE, priority = 13)]
        static void Init()
        {
            KEnv.Awake();
           
            TrafficSignalWindow window = HasOpenInstances<VRoadWindow>()?
                GetWindow<TrafficSignalWindow>(WINDOW_TITLE, true, typeof(VRoadWindow)):
                HasOpenInstances<VRoadEditorWindow>()?
                    GetWindow<TrafficSignalWindow>(WINDOW_TITLE, true, GetWindow<VRoadEditorWindow>().GetType()):
                    GetWindow<TrafficSignalWindow>(WINDOW_TITLE);
            window.SetupReferencesInEditMode();
            window.ClearRunTraffic();
            window.Show();
            MeshTools.VRoadRoot(); // Check VRoad package location relative to Assets
            InitScenes();
        }

        private URunTraffic runTraffic;
        private UMapMeshExample mapMesh;
        private UPlaySimExample playSim;
        private App app;
        
        private bool isPlaying;
        private int traffic;
        
        private bool createMultipleMeshes;
        private bool playActionEnabled;
        private bool validVRoadFileSelected;
        private string vroadFilePath;
        private string vroadFileName;
        
        
        #region references

        
        void SetupReferencesInEditMode()
        {
            missingReferences = SC.N;

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

            if (EditorApplication.isPlaying) Repaint();
        }

        private bool ShowSplits = true;
        private bool ShowOffset;
        private bool ShowCycle;
        private bool ShowAdaptive;
        private Vector2 scrollPos;
        private IJunctionControl selectedJNC;
        
        void OnGUI()
        {
            if (trySwitchScene) SwitchSceneTo(SCENE_TRAFFIC_SIGNAL, EDSF.SCENE_PATH_REL_PRO);

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
                            bool inSignalScene = SCENE_TRAFFIC_SIGNAL.Equals(currentScene);

                            if (inSignalScene) SetupRunTraffic(vroadFileName);
                            else SetupRunTraffic(SC.N);

                        }

                        if (playSim != null) playSim.RunTraffic(true);
                    }
                }
            }

            float line = 2f;
            float nl = 0.5f;
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
            }
            GUILayout.EndArea();

            GUILayoutOption optBW = GUILayout.MaxWidth(24); // columnWidth / 4);
            GUILayoutOption optH = GUILayout.Height(20);
            GUILayoutOption optSBW = GUILayout.MaxWidth(120);
            line += nl + 2.5f;
            nl = 5f;
            GUILayout.BeginArea(VRoadWindow.AreaRect(line, nl, columnWidth), GUIStyle.none);
            {
                ActionButtonOrReferenceWarning();

                if (isPlaying && app != null)
                {
                    EditorGUILayout.Space(lineY);


                    GUI.enabled = true;
                    GUILayout.BeginHorizontal();
                    {

                        int ntv = tspsValues.Length;
                        bool change = false;

                        GUILayout.Label(EDSC.SIM_SPEED, optH);
                        GUI.enabled = tspsi < ntv - 1;
                        if (GUILayout.Button(SC.MI, optBW, optH))
                        {
                            tspsi = Math.Min(tspsi + 1, ntv - 1);
                            change = true;
                        }

                        GUI.enabled = true;
                        GUILayout.Label(speedDisplay[tspsi], EDSF.STYLE_TEXTFIELD, optBW, optH);
                        GUI.enabled = tspsi > 0;
                        if (GUILayout.Button(SC.PL, optBW, optH))
                        {
                            tspsi = Math.Max(0, tspsi - 1);
                            change = true;
                        }

                        if (change)
                        {
                            int tsps = tspsValues[tspsi];
                            app.Sim().SetTimeStepsPerSecond(tsps);
                            playSim.SetFfwd(tspsi == 0);
                            playSim.SetTargetRealTimeMultiplier(xrtValues[tspsi]);
                        }
                    }
                    GUILayout.EndHorizontal();


                    GUI.enabled = true;
                    GUILayout.BeginHorizontal();
                    {
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

            if (isPlaying && app != null)
            {
                IMap map = app.Map();
                //IJunction[] jna = map.Junctions();
                IJunctionControl[] jnca = map.JunctionControls();
                GUI.enabled = true;
                
                line += nl + 2.5f;
                nl = jnca.Length + 2f;
                GUILayout.BeginArea(VRoadWindow.AreaRect(line, nl, columnWidth), GUIStyle.none);
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUIStyle radio = "radio";
                        ShowSplits = GUILayout.Toggle(ShowSplits, new GUIContent("Splits"), radio);
                        if (ShowSplits) { ShowOffset = false; ShowCycle = false; ShowAdaptive = false; }
                        ShowOffset = GUILayout.Toggle(ShowOffset, new GUIContent("Offset"), radio);
                        if (ShowOffset) { ShowSplits = false; ShowCycle = false; ShowAdaptive = false; }
                        ShowCycle  = GUILayout.Toggle(ShowCycle, new GUIContent("Cycle"), radio);
                        if (ShowCycle) { ShowSplits = false; ShowOffset = false; ShowAdaptive = false; }
                        ShowAdaptive = GUILayout.Toggle(ShowAdaptive, new GUIContent("Adaptive"), radio);
                        if (ShowAdaptive) { ShowSplits = false; ShowOffset = false; ShowCycle = false; }
                    }
                    GUILayout.EndHorizontal();
                    
                    Color stdCC = GUI.contentColor;

                    GUIStyle stopStyle = new GUIStyle(GUI.skin.textField);
                    stopStyle.normal.textColor = Color.red;
                    GUIStyle goStyle = new GUIStyle(GUI.skin.textField);
                    goStyle.normal.textColor = Color.green;

                    GUILayout.BeginArea(new Rect(0, 20, columnWidth, position.height - 250));
                    GUILayout.BeginVertical();
                    scrollPos = GUILayout.BeginScrollView(scrollPos,false,false,GUILayout.ExpandHeight(true));
                    foreach (IJunctionControl jnc in jnca)
                    {
                        ITimingPlan tp = jnc.GetTimingPlan();
                        int ns = tp.GetNumberOfStages();
                        int cti = (int) Math.Round(tp.GetCycleTime());
                        int oti  = (int) Math.Round(tp.GetOffsetTime());
                        
                        GUILayout.BeginHorizontal();
                        {
                            if (jnc == selectedJNC) GUI.contentColor = new Color(.3f, .7f, 1f);
                            
                            if (GUILayout.Button(jnc.ToString(), optSBW, optH))
                            {
                                selectedJNC = jnc;
                                Xyz loc = jnc.GetJunctions()[0].Location();
                                
                                UaCamControllerMain cam = UaCamControllerMain.MostRecentInstance;

                                loc.Z(cam.transform.position.y);

                                cam.SetFocus((float)loc.X(), (float)loc.Y());
                            }

                            GUI.contentColor = stdCC;
                            
                            double MAX_CYCLE = 300;
                            GUI.changed = false;

                            if (ShowCycle)
                            {
                                string cts = EditorGUILayout.DelayedTextField(SC.N + cti, optBW, optH);
                                if (GUI.changed)
                                {
                                    double ctd2 = double.Parse(cts);
                                    if (ctd2 >= 0 && ctd2 <= MAX_CYCLE) tp.SetCycleTime(ctd2);
                                }
                            }
                            else if (ShowOffset)
                            {
                                string ots = EditorGUILayout.DelayedTextField(SC.N + oti, optBW, optH);

                                if (GUI.changed)
                                {
                                    double otd2 = double.Parse(ots);
                                    if (otd2 >= 0 && otd2 <= MAX_CYCLE) tp.SetOffsetTime(otd2);
                                }
                            }
                            else if (ShowSplits)
                            {
                                IStage cst = jnc.GetCurrentStage();
                                for (int sti = 0; sti < ns; sti++)
                                {
                                    bool isGreen = tp.GetStage(sti) == cst;
                                    GUIStyle style = isGreen ? goStyle : stopStyle;
                                    GUI.changed = false;
                                    int gti = (int) Math.Round(tp.GetGreenTime(sti));
                                    string gts = EditorGUILayout.DelayedTextField(SC.N + gti, style, optBW, optH);

                                    if (GUI.changed)
                                    {
                                        double gtd2 = double.Parse(gts);
                                        if (gtd2 > 0) tp.SetGreenTime(sti, gtd2); 
                                    }
                                }
                            }
                            else if (ShowAdaptive)
                            {
                                bool adaptive = tp.IsAdaptive();
                                adaptive = GUILayout.Toggle(adaptive, "");
                                if (GUI.changed) tp.SetAdaptive(adaptive);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    
                    GUILayout.EndScrollView ();
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                }
                GUILayout.EndArea();
            }

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

            string expectedScene = SCENE_TRAFFIC_SIGNAL;
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
            return SCENE_TRAFFIC_SIGNAL.Equals(currentScene);
        }

        void OnSelectionChange()
        {
            if (! IsWindowActiveInCurrentScene()) return;
            
            //UaCamControllerMain cam = UaCamControllerMain.MostRecentInstance;
            //if (cam == null) return;
            
        }

    }
}