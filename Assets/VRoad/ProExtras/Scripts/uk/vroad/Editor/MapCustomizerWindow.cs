using System;
using System.Collections.Generic;
using uk.vroad.api;
using uk.vroad.api.edit;
using uk.vroad.api.enums;
using uk.vroad.api.etc;
using uk.vroad.api.geom;
using uk.vroad.api.map;
using uk.vroad.api.str;
using uk.vroad.api.xmpl;
using uk.vroad.apk;
using uk.vroad.EditorObjects;
using uk.vroad.pac;
using uk.vroad.ucm;
using uk.vroad.xmpl;
using UnityEditor;
using UnityEngine;

namespace uk.vroad.Editor
{
    public class MapCustomizerWindow : VRoadProWindow, ISelections
    {
        #region STRING_CONSTANTS

        private const string WINDOW_TITLE = "Map Customization";
        private const string MENU_TITLE = "Tools/Global Roads and Traffic Pro/" + WINDOW_TITLE;
        private const string SCENE_CUSTOMIZE = "CustomizeMap"; // Scenes are shown in alphabetical order
        private const string URL_LABEL = WINDOW_TITLE;
        private const string URL_PAGE =  "https://vroad.uk/doc/customization/";

        private const string SELECTION = "Selected ";
        private const string OPERATION = "Customization";
        private const string HINT_SELECT = "Select Point, Road or Footpath to Customize";
        private const string HINT_SELECT_X = "Select Point, Road, Footpath, or Building";
        private const string ADD =   "Add New Customization";
        private const string APPLY =  "Modify Customization ";
        private const string DELETE = "Delete Customization ";
        
        private const string SELECTION_TT = "The currently selected map object";
        private const string OPERATION_TT = "Customization to be applied on selection";
        private const string ADD_TT = "Create a new Customization";
        private const string APPLY_TT = "Modify the parameters of the existing Customization";
        private const string DELETE_TT = "Delete the existing Customization";
        
        private const string WAIT = "Wait...";
        private const string STOP_REBUILD = "Stop and Rebuild";
        private const string STOP_REBUILD_TT = "Stop the Current Scene and Rebuild OSM to VRoad";
        private const string NO_PENDING_EDITS = "No Pending Customizations";
        private const string CURRENT_EDITS = "Total Customizations";
        private const string OBJECT_EDITS = "Customizations";
       
        private const string PAUSE_TRAFFIC = "Pause Traffic";
        private const string RUN_TRAFFIC = "Run Traffic";
            
        private static readonly IMapPoint[] NO_POINTS = new IMapPoint[0];
        private static readonly HashSet<IBaseLine> EMPTY_SET = new HashSet<IBaseLine>();
        private static readonly string _ANY_ = MapEditOperation._ANY_;
        private static readonly string[] PEDWAY_SET_OPTS = { SD.F_BRIDGE, SD.F_HIGHWAY, SD.F_LAYER, SD.F_WIDTH, _ANY_ };
        #endregion

        #region STATIC_METHODS
        
        [MenuItem(MENU_TITLE, priority = 12)]
        static void Init()
        {
            KEnv.Awake();
            MapCustomizerWindow window = HasOpenInstances<VRoadWindow>()?
                GetWindow<MapCustomizerWindow>(WINDOW_TITLE, true, typeof(VRoadWindow)):
                HasOpenInstances<VRoadEditorWindow>()?
                    GetWindow<MapCustomizerWindow>(WINDOW_TITLE, true, GetWindow<VRoadEditorWindow>().GetType()):
                    GetWindow<MapCustomizerWindow>(WINDOW_TITLE);
            window.SetupReferencesInEditMode();
            window.Show();
            SetupLayers(new int[] { UMapMeshCustomize.LAYER_EDITOR}, new string[] { "Editor" });
            
            MeshTools.VRoadRoot(); // Check VRoad package location relative to Assets
            InitScenes();

        }
        
        private static bool optionsChanged;

        #endregion

        #region Variables

        private UBuildMap buildMap;
        private UMapMeshCustomize mapMesh;
        private UPlaySimExample playSim;
        private App app;
        private bool isPlaying;
        private KFile osmFile;
        private string osmFilePath;
        private string osmFileName;
        private bool validOSMFileSelected;
        private bool mapboxTerrain = true;
        private bool buildings = true;
        private bool walkways = true;
        private bool pedestrians = true;
        private bool playActionEnabled;
        private bool selectionChanged;
        private bool resetParametersOnChange = true;
        private bool immediateRestart;
        private bool pendingEdits;
       
        private readonly KList<MapEdit> edits = new KList<MapEdit>();
        
        private string parameter0; // This is the name of the road, way or node
        private string parameter1;
        private string parameter2;
        private string parameter3;

        private MapEditOperation selectedOp;
        private int selectedOpIndex;
        private int editIndex;
        private double selectedGoX;
        private double selectedGoY;
        private double selectedGoZ;
        private MapEdit existingEdit;
        private Xyz existingMove = Xyz.ALLZERO;

        private GameObject currSelGo;
        private object prevSelMo;
        private object currSelMo;
        private Xyz currSelOriginPos;

        private IMapPoint[] currSelOtherPts = NO_POINTS;
        private IMapPoint[] prevSelOtherPts = NO_POINTS;
        
        private MapEditOperation autoSaveOp;
        private object[] autoSavePax;
        private MapEdit autoSaveEdit;
        private bool needPointRebuild;
        private bool editDeleted;
        private bool noMapBoxKey;
        private int aerialMapSelection;
        private bool pausedSimulationAfterStart;
        private bool foundPlaySim;
        
        #endregion
        
        #region References
      
        void SetupReferencesInEditMode()
        {
            missingReferences = SC.N;
            
            UBuildMap[] buildMaps = Resources.FindObjectsOfTypeAll<UBuildMap>();
            if (buildMaps.Length == 1) { buildMap = buildMaps[0]; }
            else missingReferences += EDSF.REF_BUILDMAP;
            
            UMapMeshCustomize[] mapMeshes = Resources.FindObjectsOfTypeAll<UMapMeshCustomize>();
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
            foundPlaySim = (playSims.Length == 1); // not a missing reference
            if (foundPlaySim) { playSim = playSims[0]; playSim.RunTraffic(true); }
        }
        
        void SetupReferencesInPlayMode()
        {
            missingReferences = SC.N;

            buildMap = UBuildMap.MostRecentInstance;
            if (buildMap == null) missingReferences += EDSF.REF_BUILDMAP;
            
           
            mapMesh = UMapMeshCustomize.MostRecentInstance;
            if (mapMesh == null) missingReferences += EDSF.REF_MAPMESH;
            else
            {
                mapMesh.MixedMesh = true;

                edits.Clear();
                ValidateOsmFile(osmFilePath);
                if (osmFile != null)
                {
                    MapEdit[] mea = VRoad.ReadEdits(osmFile);
                    foreach (MapEdit me in mea) edits.Add(me);
                }

                
            }

            if (UaStateHandler.MostRecentInstance == null) missingReferences += EDSF.REF_APPSTATE;
            if (UaMouse.MostRecentInstance == null) missingReferences += EDSF.REF_MOUSE;
            if (UaCamControllerMain.MostRecentInstance == null) missingReferences += EDSF.REF_CAM;

            playSim = UPlaySimExample.MostRecentInstance;
            foundPlaySim = playSim != null;
            if (foundPlaySim) playSim.RunTraffic(true);
        }
        
        bool AllReferencesFound() { return missingReferences.Length == 0; }


        #endregion
        
        #region GUI

        void Awake()
        {
            string mapBoxKey = VRoad.MapBoxKeyQuery();
            noMapBoxKey = string.IsNullOrWhiteSpace(mapBoxKey) || 
                          SF.CFGVAL_TERRAIN_TOKEN_PROMPT.Equals(mapBoxKey);
            Time.timeScale = UaPlaySim.STD_RT;
        }

        // void OnFocus() { }

        // void OnLostFocus() {}
        
        void OnGUI()
        {
            // When Editor starts to Play, references are lost
            bool nowPlaying = EditorApplication.isPlaying;
            bool playStateChanged = false;
            if (nowPlaying != isPlaying)
            {
                isPlaying = nowPlaying;
                playStateChanged = true;
            }

            if (trySwitchScene) SwitchSceneTo(SCENE_CUSTOMIZE, EDSF.SCENE_PATH_REL_PRO);

            bool canSetOptions = false;

            if (IsWindowActiveInCurrentScene())
            {
                if (playStateChanged || hasSceneChanged || optionsChanged)
                {
                    if (isPlaying) SetupReferencesInPlayMode();
                    else           SetupReferencesInEditMode();
                }

                if (playStateChanged)
                {
                    pendingEdits = false;
                    pausedSimulationAfterStart = false;
                }

                if (playStateChanged && !isPlaying && immediateRestart)
                {
                    immediateRestart = false;
                    EditorApplication.isPlaying = true;
                }

                if (selectionChanged) CheckAllSelected();

                canSetOptions = missingReferences.Length == 0 && !isPlaying;
            }

            float line = 2f;
            float nl = canSetOptions? 6f: 1f;
            float cw = Math.Min(300, position.width - (2 * marginX));
            if (hasSceneChanged) ValidateOsmFile(osmFilePath);
                    
            bool isActive = IsWindowActiveInCurrentScene(); 
            
            // OSM File Name and total number of customizations (MapEdits) ////////////////////////////
            GUILayout.BeginArea(AreaRect(line, nl, cw), EditorStyles.helpBox);
            {
                bool validFileOSM = ! string.IsNullOrWhiteSpace(osmFileName);
                
                GUI.enabled = isActive;
                GUILayout.BeginHorizontal();
                {
                    NoEditTextField(validFileOSM ? osmFileName: EDSC.HINT_OSM, cw - 80);
                    GUI.enabled = isActive && !isPlaying;
                    if (GUILayout.Button(SC.ELLIPSIS, GUILayout.MinHeight(16))) SelectCachedOSMFile();
                    GUI.enabled = isActive;
                }
                GUILayout.EndHorizontal();
                
                //EditorGUILayout.Space(lineY);
                if (isPlaying && validFileOSM)
                {
                    NoEditTextField(CURRENT_EDITS, null, SC.N+edits.Count, cw/2, cw - 80);
                }

                if (canSetOptions)
                {
                    optionsChanged = false;
                    
                    GUI.changed = false;
                    if (noMapBoxKey) NoEditTextField(EDSC.AERIAL_IMAGES, EDSC.AERIAL_NO_TOKEN_TT, EDSC.MAPBOX_KEY_REQUIRED, cw/2, cw);
                    else AerialMapSelection(EditorGUILayout.Popup(new GUIContent(EDSC.AERIAL_IMAGES, EDSC.AERIAL_IMAGES_TT),
                        aerialMapSelection, EDSC.AERIAL_OPTIONS));
                    if (GUI.changed) optionsChanged = true;

                    if (noMapBoxKey)
                    {
                        NoEditTextField(EDSC.MAPBOX_TERRAIN, EDSC.MAPBOX_TERRAIN_TT, EDSC.MAPBOX_KEY_REQUIRED, cw/2, cw);
                    }
                    else
                    {
                        GUI.changed = false;
                        mapboxTerrain = EditorGUILayout.Toggle(new GUIContent(EDSC.MAPBOX_TERRAIN, EDSC.MAPBOX_TERRAIN_TT), mapboxTerrain);
                        if (GUI.changed) optionsChanged = true;
                    }
                   
                    EditorGUILayout.Space(0.5f*lineY);
                    
                    EditorGUILayout.LabelField(new GUIContent(EDSC.OPTIONS_ON_UBUILDMAP), GUILayout.MaxWidth(cw));
                  
                    
                    /*
                    GUI.changed = false;
                    buildings = EditorGUILayout.Toggle(new GUIContent(EDSC.BUILDINGS, EDSC.BUILDINGS_TT), buildings);
                    if (GUI.changed) optionsChanged = true;
                
                    GUI.changed = false;
                    walkways = EditorGUILayout.Toggle(new GUIContent(EDSC.WALKWAYS, EDSC.WALKWAYS_TT), walkways);
                    if (GUI.changed) optionsChanged = true;

                    GUI.enabled = walkways;
                    GUI.changed = false;
                    if (!walkways) pedestrians = false;
                    pedestrians = EditorGUILayout.Toggle(new GUIContent(EDSC.PEDS, EDSC.PEDS_TT), pedestrians);
                    if (GUI.changed) optionsChanged = true;
                    
                    GUI.enabled = true;
                   //*/

                }
                //*/
            }
            GUILayout.EndArea();
            playActionEnabled = validOSMFileSelected;

            line += nl + 2.3f; nl = 2f;
            GUILayout.BeginArea(AreaRect(line, nl, cw), GUIStyle.none);
            {
                if (ActionButtonOrReferenceWarning())
                {
                    SetupBuildMap(osmFileName);
                }
            }
            GUILayout.EndArea();

            if (IsWindowActiveInCurrentScene() && nowPlaying && AllReferencesFound())
            {
                bool extra = mapMesh.experimentalFunctions;

                // If there is a map object, get the set of operations available for it
                MapEditOperation[] ops = MapEditOperation.AvailableEditOperations(this, extra );

                line += nl + 0.3f;
                nl = 5f;
                GUILayout.BeginArea(AreaRect(line, nl, cw), GUIStyle.none);
                {
                    if (ops == null || ops.Length == 0)
                    {
                        if (app == null)
                        {
                            GUI.enabled = false;
                            GUILayout.Button(HintFormat(WAIT), GUILayout.Height(helpBoxHeight));
                        }
                        else
                        {
                            string hint = extra ? HINT_SELECT_X : HINT_SELECT;
                        
                            // No map object selected, or no ops available for it
                            EditorGUILayout.HelpBox(hint, MessageType.Info);
                        }

                        if (pendingEdits)
                        {
                            string buttonAction = STOP_REBUILD;
                            string buttonTooltip = STOP_REBUILD_TT;
                            GUIContent buttonContent = new GUIContent(buttonAction, buttonTooltip);
                            if (GUILayout.Button(buttonContent, GUILayout.Height(helpBoxHeight)))
                            {
                                immediateRestart = true;
                                pendingEdits = false;
                                EditorApplication.isPlaying = false;
                            }
                        }
                        else
                        {
                            GUI.enabled = false;
                            GUILayout.Button(HintFormat(NO_PENDING_EDITS), GUILayout.Height(helpBoxHeight));
                        }
                    }
                    else
                    {
                        if (selectedOpIndex < 0 || selectedOpIndex >= ops.Length) selectedOpIndex = 0;
                        selectedOp = ops[selectedOpIndex];
                        
                        string selDesc = SELECTION + selectedOp.KeyName();
                        if (currSelMo is ILane lane) selDesc += lane.GetRoad().Name().Contains("R") ? " (Back)" : "";
                       
                        GUIContent selectionGC = new GUIContent(selDesc, SELECTION_TT);
                        GUIContent operationGC = new GUIContent(OPERATION, OPERATION_TT);
                        
                        NoEditTextField(selectionGC, parameter0, cw/2, cw);

                        // Get an array of all edits for the current selection
                        MapEdit[] existingEdits = MapEdit.ExistingEdits(this, edits);
                        int editsN = existingEdits.Length; 
                        if (editIndex > editsN) editIndex = 1;
                        
                        // NEW from HERE //
                        string edDesc = editsN > 0? (editIndex > 0? SC.N + editIndex + SC.FS + editsN: SC.N + editsN):SC.Z1;

                        bool indexChanged = false;
                        GUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField(new GUIContent(OBJECT_EDITS), GUILayout.MaxWidth((cw/2) - 1));
                            Color std = GUI.contentColor;
                            GUI.contentColor = new Color(.3f, .7f, 1f);
                            GUILayout.Label(edDesc, EDSF.STYLE_TEXTFIELD, GUILayout.MaxWidth(cw/4));
                            GUI.contentColor = std;

                            GUI.enabled = editIndex > 1;
                            if (GUILayout.Button(new GUIContent("<"), GUILayout.Height(18))) { editIndex--; indexChanged = true; }

                            GUI.enabled = editIndex > 0 && editIndex < editsN;
                            if (GUILayout.Button(new GUIContent(">"), GUILayout.Height(18))) { editIndex++; indexChanged = true; }
                            GUI.enabled = true;
                        }
                        GUILayout.EndHorizontal();
                        // To HERE
                        // WAS // NoEditTextField(OBJECT_EDITS, null, SC.N+editsCount, cw/2, cw);
                       
                        int prevOpIndex = selectedOpIndex;
                        bool opChanged = false;

                        if (selectionChanged)
                        {
                            selectedOpIndex = 0;

                            // If 2 lanes selected and there is a streme between, make the default op Streme_Delete
                            if (ops.Length > 1 && TwoLanesConnectedByStremeSelected()) selectedOpIndex = 1;
                        }

                        selectedOpIndex = EditorGUILayout.Popup(operationGC, selectedOpIndex, MapEditOperation.OpNames(ops));
                        selectedOp = ops[selectedOpIndex];
                        string[] pna = selectedOp.ParameterNames();
                        int np = selectedOp.NParameters();

                        if (selectedOpIndex != prevOpIndex)
                        {
                            opChanged = true;
                            string[] opts1 = np < 2? null: selectedOp.ParameterOptions(1);
                            parameter1 = opts1 == null || opts1.Length == 0 ? SC.N: opts1[0];
                            parameter2 = SC.N;
                            parameter3 = SC.N;
                        }
                        bool modified = false;
                        bool resetParameters = false;
                        
                        parameter0 = MapEdit.TrimSelectionForOp(selectedOp, this);
                        
                        if (selectionChanged)
                        {
                            if (resetParametersOnChange) resetParameters = true;
                            resetParametersOnChange = true;
                            
                            existingEdit = ExistingEditOnChangeSelection(existingEdits);
                            
                            if (existingEdit != null)
                            {
                                // When the selection has changed, if there is an edit on the newly selected mapObject
                                // that has a different op, then use that. 
                                if (existingEdit.operation != selectedOp)
                                {
                                    selectedOp = existingEdit.operation;
                                    selectedOpIndex = Array.IndexOf(ops, selectedOp);
                                    if (selectedOpIndex < 0) { selectedOpIndex = 0; selectedOp = ops[0]; }
                                    pna = selectedOp.ParameterNames();
                                    np = selectedOp.NParameters();
                                    
                                    parameter0 = MapEdit.TrimSelectionForOp(selectedOp, this);
                                }

                                int xenp = existingEdit.parameters.Length;
                                
                                if (xenp > 1) parameter1 = existingEdit.parameters[1];
                                if (xenp > 2) parameter2 = existingEdit.parameters[2];
                                if (xenp > 3) parameter3 = existingEdit.parameters[3];
                                resetParameters = false;

                            }
                            selectionChanged = false;
                        }
                        else if (opChanged)
                        {
                            if (resetParametersOnChange) resetParameters = true;
                            resetParametersOnChange = true;
                            
                            existingEdit = ExistingEditOnChangeOp(existingEdits);

                            if (existingEdit != null)
                            {
                                int xenp = existingEdit.parameters.Length;
                                
                                if (xenp > 1) parameter1 = existingEdit.parameters[1];
                                if (xenp > 2) parameter2 = existingEdit.parameters[2];
                                if (xenp > 3) parameter3 = existingEdit.parameters[3];
                                resetParameters = false;
                            }
                        }
                        else if (indexChanged)
                        {
                            existingEdit = existingEdits[editIndex - 1]; // ALWAYS NON-NULL
                            
                            if (existingEdit.operation != selectedOp)
                            {
                                selectedOp = existingEdit.operation;
                                selectedOpIndex = Array.IndexOf(ops, selectedOp);
                                if (selectedOpIndex < 0) { selectedOpIndex = 0; selectedOp = ops[0]; }
                                pna = selectedOp.ParameterNames();
                                np = selectedOp.NParameters();
                            }

                            int xenp = existingEdit.parameters.Length;
                                
                            if (xenp > 1) parameter1 = existingEdit.parameters[1];
                            if (xenp > 2) parameter2 = existingEdit.parameters[2];
                            if (xenp > 3) parameter3 = existingEdit.parameters[3];
                        }
                        if (np >= 2)
                        {
                            string newParameter1 = SC.N;
                            
                            string[] opts1 = selectedOp.ParameterOptions(1);
                            bool showTextField1 = true;

                            if (selectedOp == MapEditOperation.Way_Set && currSelMo is IFootpath fp && fp.IsPedway())
                            {
                                opts1 = PEDWAY_SET_OPTS;
                                if (opChanged) parameter1 = opts1[0]; // reset to first option of available
                            }
                            
                            if (opts1 != null && opts1.Length > 0)
                            {
                                int optIndex = Array.IndexOf(opts1, parameter1);
                                if (optIndex >= 0 && ! MapEditOperation._ANY_.Equals(parameter1))
                                {
                                    showTextField1 = false;
                                    optIndex = EditorGUILayout.Popup(pna[1], optIndex, opts1);
                                    if (optIndex >= 0)  newParameter1 = opts1[optIndex]; 
                                }
                            }

                            if (showTextField1)
                            {
                                GUI.enabled = selectedOp != MapEditOperation.Point_Move;

                                if (selectedOp == MapEditOperation.Streme_Add && selectedLanes.Count == 2)
                                {
                                    GUI.enabled = false;

                                    ILane[] lanes = Lanes();
                                    ILane lane0 = lanes[0];
                                    ILane lane1 = lanes[1];
                                    ILane laneOut = null;
			
                                    if (lane0.GetRoad().GetJunctionB() == lane1.GetRoad().GetJunctionA())
                                    {
                                        laneOut = lane1;
                                    }
                                    else if (lane1.GetRoad().GetJunctionB() == lane0.GetRoad().GetJunctionA())
                                    {
                                        laneOut = lane0;
                                    }

                                    parameter1 = laneOut == null? SC.N: laneOut.ToString();
                                }
                                string resetStr = SC.N;
                                
                                if (resetParameters) parameter1 = resetStr;
                              
                                newParameter1 = EditorGUILayout.DelayedTextField(pna[1], parameter1);
                            }

                            bool param1Changed = !newParameter1.Equals(parameter1);
                            parameter1 = newParameter1;

                            if (existingEdit != null && existingEdit.parameters.Length > 1 &&
                                !parameter1.Equals(existingEdit.parameters[1]))
                            {
                                modified = true;
                            }

                            if (param1Changed && selectedOp.MatchParameter1())
                            {
                                MapEdit anotherEdit = ExistingEditOnChangeParameter1(existingEdits);

                                existingEdit = anotherEdit;
                                if (existingEdit != null && existingEdit.parameters.Length > 2)
                                {
                                    parameter2 = existingEdit.parameters[2];
                                    resetParameters = false;
                                }
                            }

                            
                            if (np >= 3)
                            {
                                bool showTextField2 = true;
                                string newP2 = parameter2;

                                if (pna[1].Equals(SX.ATTRIBUTE) && pna[2].Equals(SX.VALUE))
                                {
                                    string attribute = parameter1;

                                    string[] vopts = MapEditOperation.ValueOptions(selectedOp, attribute);

                                    if (vopts != null && vopts.Length > 0)
                                    {
                                       
                                        if ((param1Changed && existingEdit == null) || string.IsNullOrEmpty(newP2))
                                        //if (param1Changed || string.IsNullOrEmpty(newP2))
                                        {
                                            newP2 = vopts[0];
                                            resetParameters = false;
                                        }

                                        int valueIndex = _ANY_.Equals(newP2)? -1: Array.IndexOf(vopts, newP2);
                                        if (valueIndex >= 0)
                                        {
                                            showTextField2 = false;
                                            valueIndex = EditorGUILayout.Popup(SX.VALUE, valueIndex, vopts);
                                            if (valueIndex >= 0) newP2 = vopts[valueIndex];
                                        }
                                    }
                                    else
                                    {
                                        if (param1Changed) newP2 = SC.N;
                                    }

                                }

                                if (showTextField2) // show text field only if combo has not already been shown
                                {
                                    if (resetParameters) newP2 = SC.N;
                                    if (_ANY_.Equals(parameter1)) newP2 = SC.N;

                                    newP2 = EditorGUILayout.DelayedTextField(pna[2], newP2);
                                }

                                bool param2Changed = !newP2.Equals(parameter2);
                                parameter2 = newP2;

                                if (existingEdit != null && existingEdit.parameters.Length > 2 && 
                                    !parameter2.Equals(existingEdit.parameters[2]))
                                {
                                    modified = true;
                                }


                                if (np >= 4) // Parameter3 is used for Z-value of *_Move
                                {
                                    if (resetParameters) parameter3 = SC.N;
                                    parameter3 = EditorGUILayout.DelayedTextField(pna[3], parameter3);

                                    if (existingEdit != null && existingEdit.parameters.Length > 3 && 
                                        !parameter3.Equals(existingEdit.parameters[3]))
                                    {
                                        modified = true;
                                    }
                                }
                            }
                        }

                        GUI.enabled = true;
                        
                        if (existingEdit == null)
                        {
                            object[] pax = new object[np];
                            
                            GUI.enabled = ValidForEdit(pax, true);
                            if (GUILayout.Button(new GUIContent(ADD, ADD_TT), GUILayout.Height(helpBoxHeight)))
                            {
                                CreateNewEdit(selectedOp, pax, true);
                            }
                        }
                        if (existingEdit != null) // Show buttons Modify and Delete
                        {
                            object[] pax = new object[np];
                            bool canCommitMods = modified && ValidForEdit(pax, false);

                            //GUI.enabled = canCommitMods;
                            if (canCommitMods)
                            {
                                if (GUILayout.Button(new GUIContent(APPLY+edDesc, APPLY_TT), GUILayout.Height(helpBoxHeight)))
                                {
                                    ModifyEdit(existingEdit, pax, true);
                                }
                            }

                            else
                            {
                                //GUI.enabled = true;
                                if (GUILayout.Button(new GUIContent(DELETE+edDesc, DELETE_TT),
                                    GUILayout.Height(helpBoxHeight)))
                                {
                                    DeleteEdit();
                                }
                            }
                        }
                        
                    }
                   
                }
                GUILayout.EndArea();
            }
            
            Footer(GRAT_PRO_SUBTITLE, URL_LABEL, URL_PAGE);

            hasSceneChanged = false;
        }

        private MapEdit ExistingEditOnChangeParameter1(MapEdit[] existingEdits)
        {
            int editsCount = existingEdits.Length;
            for (int ei = 0; ei < editsCount; ei++)
            {
                MapEdit edit = existingEdits[ei];
                if (edit.operation == selectedOp && edit.parameters[1].Equals(parameter1))
                {
                    editIndex = ei + 1;
                    return edit;
                }
            }
            
            editIndex = 0;
            return null;
        }

        private MapEdit ExistingEditOnChangeSelection(MapEdit[] existingEdits)
        {
            int editsCount = existingEdits.Length;
            if (editsCount == 0) { editIndex = 0; return null; }

            // All the edits will match the selection, but might not match the op

            // Find the first that matches the op, and optionally parameter1
            bool matchParameter1 = selectedOp.MatchParameter1();
            for (int ei = 0; ei < editsCount; ei++)
            {
                MapEdit edit = existingEdits[ei];
                if (edit.operation == selectedOp && (!matchParameter1 || edit.parameters[1].Equals(parameter1)))
                {
                    editIndex = ei + 1;
                    return edit;
                }
            }

            if (matchParameter1)
            {
                for (int ei = 0; ei < editsCount; ei++)
                {
                    MapEdit edit = existingEdits[ei];
                    if (edit.operation == selectedOp)
                    {
                        editIndex = ei + 1;
                        return edit;
                    }
                }
            }
           
            // Return the first that matches the selection, but another op
            editIndex = 1;
            return existingEdits[0];
        }
        private MapEdit ExistingEditOnChangeOp(MapEdit[] existingEdits)
        {
            int editsCount = existingEdits.Length;
            if (editsCount == 0) { editIndex = 0; return null; }

            // All the edits will match the selection, but might not match the op

            // Find the first that matches the new op; ignore parameter1
            for (int ei = 0; ei < editsCount; ei++)
            {
                MapEdit edit = existingEdits[ei];
                if (edit.operation == selectedOp)
                {
                    editIndex = ei + 1;
                    return edit;
                }
            }

            editIndex = 0;
            return null;
        }

        private void SelectCachedOSMFile()
        {
            validOSMFileSelected = false;
            string dir = KEnv.OsmDir();
            string path = EditorUtility.OpenFilePanel(EDSC.OSM_FILE_DESC, dir, EDSF.SUFFIX_OSM);
            if (string.IsNullOrEmpty(path)) return;

            ValidateOsmFile(path);
            
            if (validOSMFileSelected)
            {
                int prevZoomLevel = VRoad.MostRecentAerials(osmFileName);
                if (prevZoomLevel > 0)  // Change only if a previous zoom level is found 
                {
                    AerialMapSelection(prevZoomLevel - (GlobeMath.AERO_ZOOM_MIN - 1)); 
                    Debug.Log(KFormat.Sprintf(EDSC.MSG_FOUND_PREV_AERIAL_ZOOM, prevZoomLevel));
                }
            }
        }

        void ValidateOsmFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            
            KFile file = new KFile(path);
            if (file.Exists())
            {
                string[] ccsa = FilenameWrapper.OSMJ.CountryCitySuburb(file.FileName());
                if (ccsa.Length == 4)
                {
                    int ha = KTools.ParseInt(ccsa[3]);
                    validOSMFileSelected = ha > 0;
                }
            }

            if (validOSMFileSelected)
            {
                osmFile = file;
                osmFilePath = path;
                osmFileName = file.FileName();
            }
            else
            {
                osmFile = null;
                osmFileName = SC.N;
                osmFilePath = SC.N;
            }
        }

        
        private void AerialMapSelection(int x) { aerialMapSelection = x; }

        private bool ActionButtonOrReferenceWarning()
        {
            bool calledAction = false;
            GUI.enabled = true;

            string expectedScene = SCENE_CUSTOMIZE;
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
                    string msg = buildMap.ProgressActivity();
                    EditorGUILayout.HelpBox(msg, MessageType.Info);
                }
                else
                {
                    if (foundPlaySim)
                    {
                        bool wasRunning = playSim.runTraffic;
                        string label = wasRunning ? PAUSE_TRAFFIC : RUN_TRAFFIC;
                        if (GUILayout.Button(label, GUILayout.Height(helpBoxHeight)))
                        {
                            bool runningNow = !wasRunning;
                            playSim.SetFfwd(runningNow);
                            playSim.RunTraffic(runningNow);
                            Time.timeScale = runningNow ? UaPlaySim.STD_RT : 0;
                        }
                    }
                    else
                    {
                        GUI.enabled = false; // was EDSC.RUNNING
                        GUILayout.Button(HintFormat(SC.N), GUILayout.Height(helpBoxHeight));
                    }
                }
                
            }
            else
            {
                GUI.enabled = playActionEnabled;

                string hint = EDSC.HINT_REBUILD;

                string buttonAction = KFormat.Sprintf(EDSC.PLAY_SCENE, expectedScene, SC.N);
                
                string buttonTooltip = EDSC.TOOLTIP_REBUILD;
                GUIContent buttonContent = playActionEnabled ? new GUIContent(buttonAction, buttonTooltip)
                    : new GUIContent(HintFormat(hint));

                if (GUILayout.Button(buttonContent, GUILayout.Height(helpBoxHeight)))
                {
                    EditorApplication.isPlaying = true;
                    calledAction = true;
                }
            }

            GUI.enabled = true;

            return calledAction;
        }

        private void SetupBuildMap(string osmFileName)
        {
            if (buildMap == null) missingReferences += EDSF.REF_BUILDMAP;

            else SetParametersAndBuild(osmFileName);
        }

        
        private void SetParametersAndBuild(string path)
        {
            BuildParameter.InitAll();
            
            BuildParameter.FilePath.Set(path);
            BuildParameter.AerialSelection.Set(aerialMapSelection);

            BuildFlag.NoTerrain.Set(!mapboxTerrain);
            BuildFlag.NoBuildings.Set(!buildings);
            BuildFlag.NoWalks.Set(!walkways);
            BuildFlag.NoPeds.Set(!pedestrians);
            
            buildMap.SetupBuildParameters();
        }
        #endregion

        #region MapEdits

        private void FillPax(object[] pax)
        {
            int np = pax.Length;
            if (np >= 1) pax[0] = parameter0;
            if (np >= 2) pax[1] = parameter1;
            if (np >= 3) pax[2] = parameter2;
            if (np >= 4) pax[3] = parameter3;
        }
        private bool ValidForEdit(object[] pax, bool isNew)
        {
            FillPax(pax);

            ILane[] lanes = TwoConnectableLanesSelected();
            if (lanes != null)
            {
                if (!isNew) return false; // You cannot modify streme_add or streme_del
                
                IStreme streme = lanes[0].GetStremeOutTo(lanes[1]);
                bool stremeExists = streme != null;
                
                if (selectedOp == MapEditOperation.Streme_Add)    return !stremeExists;
                if (selectedOp == MapEditOperation.Streme_Delete) return  stremeExists;
            }

            bool valid = true;
           
            for (int pi = 0; pi < pax.Length; pi++)
            {
                if (pax[pi] == null) { valid = false; break; }

                string s = pax[pi].ToString().Trim();
                if (s.Length == 0)  { valid = false; break; }
                if (s.Equals(_ANY_)) { valid = false; break; }
                
                pax[pi] = s;
            }

            return valid;
        }

        private void AutoSaveClear()
        {
            autoSaveOp = null;
            autoSavePax = null;
            autoSaveEdit = null;
           
            selectionChanged = true;
        }
        private void EditStateChanged()
        {
            AutoSaveClear();

            needPointRebuild = true;
            
            bool nowPending = !pendingEdits;
            pendingEdits = true;
            if (nowPending) Repaint(); // to show the rebuild button
        }

        private bool CreateNewEditSingle(MapEditOperation op, object[] pax)
        {
            MapEdit me = MapEdit.NewMapEdit(op, pax);
            if (me == null) return false;
            
            edits.Add(me);
            foreach (object mo in me.MapObjects(app)) mapMesh.SetHighlightMaterial(mo);
            return true;
        }

        private void CreateOrModifyMultiple(MapEditOperation op, object[] pax)
        {
            if (op.KeyName().Equals(SX.ROAD) && selectedRoads.Count > 1)
            {
                string primaryP0 = pax[0].ToString();
                string primaryP1 = pax.Length > 1 ? pax[1].ToString() : null;

                foreach (IRoad road in selectedRoads)
                {
                    string roadName = road.Name();
                    if (roadName.Equals(primaryP0)) continue;

                    pax[0] = roadName;
                    
                    MapEdit xe = MapEdit.ExistingEditPrimary(road.GetKerbLane(), edits, op, primaryP1);

                    if (xe != null) ModifyEditSingle(xe, pax);
                    else CreateNewEditSingle(op, pax);
                }
            }

            else if (op.KeyName().Equals(SX.WAY) && selectedWays.Count > 1)
            {
                string primaryP0 = pax[0].ToString();
                string primaryP1 = pax.Length > 1 ? pax[1].ToString() : null;
                
                foreach (string wayName in selectedWays)
                {
                    if (wayName.Equals(primaryP0)) continue;
                    
                    pax[0] = wayName;
                    
                    MapEdit xe = MapEdit.ExistingEditPrimary(wayName, edits, op, primaryP1);

                    if (xe != null) ModifyEditSingle(xe, pax);
                    else CreateNewEditSingle(op, pax);
                }
            }
        }
        
        private void CreateNewEdit(MapEditOperation op, object[] pax, bool save)
        {
            bool anyChange = CreateNewEditSingle(op, pax);

            if (anyChange) CreateOrModifyMultiple(op, pax);
           
            if (save && anyChange) VRoad.SaveEdits(osmFile, edits.ToArray());

            EditStateChanged();
        }

        private bool ModifyEditSingle(MapEdit me, object[] pax)
        {
            int np = me?.operation.NParameters() ?? 0;

            if (np < 2 || pax == null || pax.Length != np) return false;

            if (np >= 2) me.parameters[1] = pax[1].ToString();
            if (np >= 3) me.parameters[2] = pax[2].ToString();
            if (np >= 4) me.parameters[3] = pax[3].ToString();

            return true;
        }
        
        private void ModifyEdit(MapEdit me, object[] pax, bool save)
        {
            bool anyChange = ModifyEditSingle(me, pax);
            
            if (anyChange) CreateOrModifyMultiple(me.operation, pax);

            if (save && anyChange) VRoad.SaveEdits(osmFile, edits.ToArray());
            
            EditStateChanged();
        }

        private void DeletePointEdit(MapEdit me)
        {
            if (prevSelMo is IMapPoint mp)
            {
                double mx = -GetD(me.parameters[1]);
                double my = -GetD(me.parameters[2]);
                double mz = -GetD(me.parameters[3]);

                mp.MoveBy(mx, my, mz);

                foreach (IMapPoint somp in currSelOtherPts)
                {
                    MapEdit xe = MapEdit.ExistingEditPrimary(somp, edits, me.operation, null);
                    if (xe != null)
                    {
                        mx = -GetD(xe.parameters[1]);
                        my = -GetD(xe.parameters[2]);
                        mz = -GetD(xe.parameters[3]);

                        somp.MoveBy(mx, my, mz);

                        mapMesh.SetDeletedMaterial(somp);
                        edits.Remove(xe);
                    }
                }
            }

            DeleteSingle(me);
        }

        private void DeleteSingle(MapEdit me)
        {
            if (me == null) return;
            
            foreach (object mo in me.MapObjects(app)) mapMesh.SetDeletedMaterial(mo);

            edits.Remove(me);
        }

        private void DeleteMultiple(MapEditOperation op, object[] pax)
        {
            if (op.KeyName().Equals(SX.ROAD) && selectedRoads.Count > 1)
            {
                string primaryP0 = pax[0].ToString();
                string primaryP1 = pax.Length > 1 ? pax[1].ToString() : null;

                foreach (IRoad road in selectedRoads)
                {
                    string roadName = road.Name();
                    if (roadName.Equals(primaryP0)) continue;

                    MapEdit xe = MapEdit.ExistingEditPrimary(roadName, edits, op, primaryP1);
                    if (xe != null) DeleteSingle(xe);
                }
            }

            else if (op.KeyName().Equals(SX.WAY) && selectedWays.Count > 1)
            {
                string primaryP0 = pax[0].ToString();
                string primaryP1 = pax.Length > 1 ? pax[1].ToString() : null;
                
                foreach (string wayName in selectedWays)
                {
                    if (wayName.Equals(primaryP0)) continue;
                    
                    MapEdit xe = MapEdit.ExistingEditPrimary(wayName, edits, op, primaryP1);
                    if (xe != null) DeleteSingle(xe);
                }
            }
        }
        private void DeleteEdit()
        {
            MapEdit me = existingEdit;
            if (me == null) return; // never happens: button is visible only if this is non-null

            if (me.operation.AllowsMove()) DeletePointEdit(me);
            else
            {
                DeleteMultiple(me.operation, me.parameters);
                DeleteSingle(me);
            }
           
            VRoad.SaveEdits(osmFile, edits.ToArray());

            editDeleted = true;

            EditStateChanged();
        }

        private void CreateNewPointMove(IMapPoint mp, Xyz delta)
        {
            MapEditOperation op = MapEditOperation.Point_Move;
            object[] pax = new object[4];
            pax[0] = mp.ToString();
            pax[1] = delta.X().ToString();
            pax[2] = delta.Y().ToString();
            pax[3] = delta.Z().ToString();
            
            MapEdit me = MapEdit.NewMapEdit(op, pax);
            if (me != null) { edits.Add(me); }
            
            mapMesh.SetHighlightMaterial(mp);
        }
        
        private void ModifyPointMove(MapEdit me, Xyz delta)
        {
            if (me == null || me.operation != MapEditOperation.Point_Move) return;

            me.parameters[1] = (KTools.GetDouble(me.parameters[1]) + delta.X()).ToString();
            me.parameters[2] = (KTools.GetDouble(me.parameters[2]) + delta.Y()).ToString();
            me.parameters[3] = (KTools.GetDouble(me.parameters[3]) + delta.Z()).ToString();
        }

        #endregion

        #region AutoSave

        private double GetD(object param) { return KTools.GetDouble(param.ToString()); }
        Xyz DeltaMove(object[] pax)
        {
            if (pax == null || pax.Length != 4) return Xyz.ALLZERO;
            
            return new Xyz(GetD(pax[1]), GetD(pax[2]), GetD(pax[3]));
        }

        Xyz DeltaMove(MapEdit xe, object[] pax)
        {
            Xyz prev = new Xyz(GetD(xe.parameters[1]), GetD(xe.parameters[2]), GetD(xe.parameters[3]));
            Xyz curr = new Xyz(GetD(pax[1]), GetD(pax[2]), GetD(pax[3]));
            return curr.Minus(prev);
        }
        
        private void AutoSaveCheck()
        {
            if (autoSaveOp == MapEditOperation.Point_Move) // op is set for point_move only
            {
                if (prevSelOtherPts.Length > 0)
                {
                    Xyz delta = autoSaveEdit != null ? DeltaMove(autoSaveEdit, autoSavePax) : DeltaMove(autoSavePax);
                    foreach (IMapPoint somp in prevSelOtherPts)
                    {
                        MapEdit xe = MapEdit.ExistingEditPrimary(somp, edits, autoSaveOp, null);
                        if (xe != null) ModifyPointMove(xe, delta);
                        else CreateNewPointMove(somp, delta);
                    }
                }

                if (autoSaveEdit != null) ModifyEdit(autoSaveEdit, autoSavePax, false);
                else                      CreateNewEdit(autoSaveOp, autoSavePax, false);

                VRoad.SaveEdits(osmFile, edits.ToArray());
            }

            else AutoSaveClear();
        }

        #endregion
  
        #region Update

        private void OnFocus()
        {
            if (string.IsNullOrEmpty(osmFileName))
            {
                string mruFilePath = KEnv.OsmDir() + VRoadWindow.mruFileRoot + SC.SUFFIX_DOT_JSON;
                ValidateOsmFile(mruFilePath);
            }
        }

        private KHash<IMapPoint, Xyz> preDragPointPositions = new KHash<IMapPoint, Xyz>();
        
        private IMapPoint[] Combine(object sel, IMapPoint[] others)
        {
            if (!(sel is IMapPoint mpSel)) return others;

            int no = others?.Length ?? 0;
            if (no == 0) return new IMapPoint[] { mpSel,};

            IMapPoint[] combined = new IMapPoint[1 + no];
            combined[0] = mpSel;
            for (int pi = 0; pi < no; pi++) combined[1 + pi] = others[pi];
            return combined;
        }
        void Update()
        {
            if (! IsWindowActiveInCurrentScene()) return;

            if (isPlaying && app == null && AllReferencesFound() && mapMesh.MeshesReady())
            {
                app = ExampleApp.AwakeInstance(); // this does not go in Awake, because this is an EditorWindow

                foreach (MapEdit me in edits)
                {
                    foreach (object mo in me.MapObjects(app)) mapMesh.SetHighlightMaterial(mo);
                }

                if (!pausedSimulationAfterStart)
                {
                    if (foundPlaySim) playSim.RunTraffic(false);
                    pausedSimulationAfterStart = true;
                }

                Repaint();

                FocusWindowIfItsOpen(typeof(SceneView));

                // SceneView.lastActiveSceneView.LookAt(app.Map().GetCentrePoint().ToVector3(), 
                //        Quaternion.Euler(90,0,0), 0.25f * (float) app.Map().GetWidth(), false);  
            }

            if (!isPlaying && app != null)
            {
                app = null;
            }

            if (mapMesh == null || app == null) return;
            
            
            if (currSelGo == null || currSelMo == null) // nothing is selected
            {
                if (prevSelMo != null)
                {
                    AutoSaveCheck();
                    
                    CheckPointRebuild();

                    IMapPoint[] selPts = Combine(prevSelMo, prevSelOtherPts);
                    
                    prevSelMo = null;
                    prevSelOtherPts = NO_POINTS;
                   
                    mapMesh.UpdateModifiedLines(EMPTY_SET, edits, selPts, preDragPointPositions);
                    mapMesh.RebuildModifiedLines();
                    
                }
                needPointRebuild = false;
            }
            else
            {
                bool extra = mapMesh.experimentalFunctions;
                MapEditOperation[] ops = MapEditOperation.AvailableEditOperations(this, extra);
                if (ops != null && selectedOpIndex >= 0 && selectedOpIndex < ops.Length)
                {
                    selectedOp = ops[selectedOpIndex];
                }

                if (currSelMo == prevSelMo && currSelOtherPts.Length == prevSelOtherPts.Length)
                {
                    // Same object(s) selected, this is called repeatedly when dragging point(s) 
                    
                    if (editDeleted)
                    {
                        CheckPointRebuild();
                        mapMesh.RebuildModifiedLines();
                        editDeleted = false;
                    }
                }
                else // change of selection, without going through no-selection state
                {
                    AutoSaveCheck();

                    CheckPointRebuild();

                    IMapPoint[] selPts = Combine(prevSelMo, prevSelOtherPts);
                    prevSelMo = currSelMo;
                    prevSelOtherPts = currSelOtherPts;
                    
                    HashSet<IBaseLine> currentlyModifiedLines = EMPTY_SET;
                    //mapMesh.ClearModifiedCentreLines();
                    selectedGoX = 0;
                    selectedGoY = 0;
                    selectedGoZ = 0;

                    if (currSelMo is IMapPoint csmp)
                    {
                        // Change op to one that allows a point to be moved
                        if (selectedOp == null || !selectedOp.AllowsMove())
                        {
                            // For the object that is currently selected, is there an operation that allows a move
                            selectedOp = MapEditOperation.Point_Move;
                            selectedOpIndex = Array.IndexOf(ops, selectedOp);
                        }

                        currentlyModifiedLines = mapMesh.LinesThrough(csmp, currSelOtherPts);

                        existingMove = Xyz.ALLZERO;
                        MapEdit moveEdit = MapEdit.ExistingEditPrimary(csmp, edits, selectedOp, null);
                        if (moveEdit != null)
                        {
                            int nmvP = moveEdit.parameters.Length;
                            double exMvX = nmvP > 1 ? KTools.ParseDoubleX0(moveEdit.parameters[1]) : 0;
                            double exMvY = nmvP > 2 ? KTools.ParseDoubleX0(moveEdit.parameters[2]) : 0;
                            double exMvZ = nmvP > 3 ? KTools.ParseDoubleX0(moveEdit.parameters[3]) : 0;
                            existingMove = new Xyz(exMvX, exMvY, exMvZ);
                        }

                        currSelOriginPos = (new Xyz(csmp.X(), csmp.Y(), csmp.Z()));
                    }

                    if (currSelMo is IOutline outline && outline.IsBuilding() && ops != null)
                    {
                        if (selectedOp == null || !selectedOp.AllowsMove())
                        {
                            selectedOp = MapEditOperation.Outline_Move;
                            selectedOpIndex = Array.IndexOf(ops, selectedOp);
                        }
                        existingMove = Xyz.ALLZERO;
                        MapEdit moveEdit = MapEdit.ExistingEditPrimary(outline, edits, selectedOp, null);
                        if (moveEdit != null)
                        {
                            int nmvP = moveEdit.parameters.Length;
                            double exMvX = nmvP > 1 ? KTools.ParseDoubleX0(moveEdit.parameters[1]) : 0;
                            double exMvY = nmvP > 2 ? KTools.ParseDoubleX0(moveEdit.parameters[2]) : 0;
                            double exMvZ = nmvP > 3 ? KTools.ParseDoubleX0(moveEdit.parameters[3]) : 0;
                            existingMove = new Xyz(exMvX, exMvY, exMvZ);
                        }

                    }
                    mapMesh.UpdateModifiedLines(currentlyModifiedLines, edits, selPts, preDragPointPositions);
                    mapMesh.RebuildModifiedLines();
                    
                   
                    needPointRebuild = false;
                }

                // This clause is called on each Update() 
                if (selectedOp != null && selectedOp.AllowsMove())
                {
                    Vector3 selGoPos = currSelGo.transform.position;
                    // This clause is called only if the point has been dragged
                    if (selectedGoX != selGoPos.x || selectedGoY != selGoPos.y || selectedGoZ != selGoPos.z)
                    {
                        selectedGoX = selGoPos.x;
                        selectedGoY = selGoPos.y;
                        selectedGoZ = selGoPos.z;

                        if (currSelMo is IMapPoint mp && currSelOriginPos != null)
                        {
                            resetParametersOnChange = false;
                            parameter1 = KFormat.Sprintf(SC.SF2F, existingMove.X() + selectedGoX);
                            parameter2 = KFormat.Sprintf(SC.SF2F, existingMove.Y() + selectedGoZ);
                            parameter3 = KFormat.Sprintf(SC.SF2F, existingMove.Z() + selectedGoY);

                            autoSavePax = new object[4];
                            FillPax(autoSavePax);
                            autoSaveOp = selectedOp;
                            autoSaveEdit = existingEdit;

                            double mx = (currSelOriginPos.X() + selectedGoX) - mp.X();
                            double my = (currSelOriginPos.Y() + selectedGoZ) - mp.Y();
                            double mz = (currSelOriginPos.Z() + selectedGoY) - mp.Z();

                            mp.MoveBy(mx, my, mz);

                            foreach (IMapPoint somp in currSelOtherPts) { somp.MoveBy(mx, my, mz); }

                            mapMesh.RebuildModifiedLines();
                        }

                        if (currSelMo is IOutline building)
                        {
                            resetParametersOnChange = false;
                            parameter1 = KFormat.Sprintf(SC.SF2F, existingMove.X() + selectedGoX);
                            parameter2 = KFormat.Sprintf(SC.SF2F, existingMove.Y() + selectedGoZ);
                            parameter3 = KFormat.Sprintf(SC.SF2F, existingMove.Z() + selectedGoY);

                        }
                        Repaint(); // Forces OnGUI so that the Editor window fields update with the new values 
                    }
                }

                if (currSelMo != null && !(currSelMo is IMapPoint) && !(currSelMo is IOutline))
                {
                    currSelGo.transform.position = Vector3.zero;
                }
            }
        }

       
        void CheckPointRebuild()
        {
            if (needPointRebuild && prevSelMo is IMapPoint psmp)
            {
                mapMesh.RebuildPoint(psmp);
                        
                foreach (IMapPoint psomp in prevSelOtherPts) mapMesh.RebuildPoint(psomp);
            }
        }
        #endregion

        #region Selection

        private readonly List<UnityEngine.Object> selected = new List<UnityEngine.Object>();
        
        // Currently there is no way to deselect a road once selected, because as soon as you 
        // deselect a lane, if any other part is still selected then the lane will be re-selected 
        //
        // It would be better to have another drawn object for the road, but not a centreline
        // because there needs to be one in each direction
        private readonly KHashSet<IRoad> selectedRoads = new KHashSet<IRoad>();
        private readonly KHashSet<string> selectedWays = new KHashSet<string>();
        private readonly KHashSet<ILane> selectedLanes = new KHashSet<ILane>();

        private void AddToSelection(Transform t)
        {
            if (t != null)
            {
                GameObject go = t.gameObject;
                if (! selected.Contains(go)) selected.Add(go);
            }
        }
        private void SelectAllRoad(IRoad road)
        {
            bool extra = mapMesh.experimentalFunctions;
            if (extra)
            {
                
            }
            else
            {
                foreach (ILane lane in road.GetLanes()) AddToSelection(mapMesh.GetGoTransform(lane));

                IFootpath fpk = road.GetSideWalkK();
                IFootpath fpm = road.GetSideWalkM();
                if (fpk != null) AddToSelection(mapMesh.GetGoTransform(fpk));
                if (fpm != null) AddToSelection(mapMesh.GetGoTransform(fpm));

                UnityEngine.Object[] sa = selected.ToArray();
                Selection.objects = sa;
            }

            selectedRoads.Add(road);
            
            Repaint();
        }
        
        private void CheckAllSelected()
        {
            if (currSelMo is ILane laneSel)
            {
                IRoad road = laneSel.GetRoad();
                if (!AllRoadSelected(road, laneSel, null))  SelectAllRoad(road);
            }
            if (currSelMo is IFootpath fpSel && fpSel.IsSidewalk())
            {
                IRoad road = fpSel.GetRunningLane().GetRoad();
                if (!AllRoadSelected(road, null, fpSel))  SelectAllRoad(road);
            }

        }
        private bool IsSelected(Transform selT)
        {
            if (selT == null) 
                return true; // If no sidewalk,  return true to say it is not waiting to be selected
            foreach (Transform t in Selection.transforms) if (t == selT) return true;
            return false;
        }
        private bool AllRoadSelected(IRoad road, ILane laneSel, IFootpath fpSel)
        {
            foreach (ILane lane in road.GetLanes())
            {
                if (lane == laneSel) continue;
                if (!IsSelected(mapMesh.GetGoTransform(lane))) return false;
            }

            IFootpath fpk = road.GetSideWalkK();
            IFootpath fpm = road.GetSideWalkM();
            if (fpk != null && fpk != fpSel && !IsSelected(mapMesh.GetGoTransform(fpk))) return false;
            if (fpm != null && fpm != fpSel && !IsSelected(mapMesh.GetGoTransform(fpm))) return false;
          
            return true;
        }

        
        public override bool IsWindowActiveInCurrentScene()
        {
            return SCENE_CUSTOMIZE.Equals(currentScene);
        }

        void OnSelectionChange()
        {
            if (! IsWindowActiveInCurrentScene()) return;

            selectionChanged = true;
            
            selected.Clear();    // More items may be added in SelectAllRoad
            foreach (UnityEngine.Object obj in Selection.objects) selected.Add(obj);
          
            currSelGo = Selection.activeGameObject;

            // Get any map object associated with the selected game object, could be null
            currSelMo = mapMesh.GetMapObject(currSelGo);

            
            currSelOtherPts = NO_POINTS;
            bool allSelectionsAreMovable = false;
            Transform[] ta = Selection.transforms;
            
            if (currSelMo is IMapPoint csmp)
            {
                allSelectionsAreMovable = true;

                preDragPointPositions.Clear();
                preDragPointPositions.Put(csmp, new Xyz(csmp.Location()));
                
                if (ta.Length > 1)
                {
                    List<IMapPoint> mpList = new List<IMapPoint>();
                    foreach (Transform st in ta)
                    {
                        GameObject go = st.gameObject;
                        if (go == currSelGo) continue;

                        if (mapMesh.GetMapObject(go) is IMapPoint smp)
                        {
                            mpList.Add(smp);
                            preDragPointPositions.Put(smp, new Xyz(smp.Location()));
                        }

                        else allSelectionsAreMovable = false;
                    }

                    currSelOtherPts = mpList.ToArray();
                }
            }
            else if (currSelMo is IOutline building) allSelectionsAreMovable = true;
            
            selectedLanes.Clear();
            selectedRoads.Clear();
            selectedWays.Clear();
            
            foreach (Transform st in ta)
            {
                object mo = mapMesh.GetMapObject(st.gameObject);
                
                if (mo is ILane ln)
                {
                    selectedLanes.Add(ln);
                    IRoad rd = ln.GetRoad(); 
                    selectedRoads.Add(rd);
                    selectedWays.Add(rd.WayName());
                }
                else if (mo is IFootpath fp)
                {
                    selectedWays.Add(fp.WayName());
                    if (fp.IsSidewalk()) selectedRoads.Add(fp.GetRunningLane().GetRoad());
                }
                else if (mo is ICentreLine cl)
                {
                    String cls = cl.ToString();
                    if (cls.EndsWith(SX._IGNORED))
                    {
                        string wayName = cls.Substring(0, cls.Length - SX._IGNORED.Length);
                        selectedWays.Add(wayName);
                    }
                }
            }
            
            // Hide the RGB/XYZ handle so that picked object(s) cannot be dragged, unless they are all points
            // This also prevents the Transform from being edited
            //
            // See also SceneVisibilityManager.instance.DisablePicking in UMapMeshCustomize
            Tools.hidden = ! allSelectionsAreMovable;
            
            Repaint();
        }

       
        #endregion

        public object PrimaryMapObject()
        {
            return currSelMo;
        }

        public ILane[] Lanes()
        {
            return selectedLanes.ToArray();
        }

        /** This returns two lanes if they could be connected: one ends and the other starts at the same junction */
        private ILane[] TwoConnectableLanesSelected()
        {
            if (selectedLanes.Count != 2) return null;
            
            ILane[] lanes = Lanes();
            
            if (lanes[0].GetRoad().GetJunctionB() == lanes[1].GetRoad().GetJunctionA()) return lanes;
            
            if (lanes[1].GetRoad().GetJunctionB() == lanes[0].GetRoad().GetJunctionA())
            {
                ILane tmp = lanes[0];
                lanes[0] = lanes[1];
                lanes[1] = tmp;
                return lanes;
            }

            return null;
        }

        private bool TwoLanesConnectedByStremeSelected()
        {
            if (selectedLanes.Count != 2) return false;
            
            ILane[] lanes = Lanes();

            if (lanes[0].GetStremeOutTo(lanes[1]) != null) return true;
            if (lanes[1].GetStremeOutTo(lanes[0]) != null) return true;

            return false;
        }
    }
}