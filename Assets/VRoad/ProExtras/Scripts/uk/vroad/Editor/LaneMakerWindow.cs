using System;
using uk.vroad.api.str;
using uk.vroad.apk;
using uk.vroad.EditorObjects;
using uk.vroad.ucm;
using UnityEditor;
using UnityEngine;

namespace uk.vroad.Editor
{
    public class LaneMakerWindow : VRoadProWindow
    {
        #region STRING_CONSTANTS

        private const string WINDOW_TITLE = "Lane Material Maker";
        private const string MENU_TITLE = "Tools/"+GRAT_PRO+"/"+WINDOW_TITLE;
        private const string URL_LABEL = WINDOW_TITLE;
        private const string URL_PAGE = "https://vroad.uk/doc/laneMaker/";
        private const string TOOLTIP_ACTION = "Make Lane Materials";
        private const string SCENE_LANEMAKER = "LaneMaker";
        
        #endregion


        [MenuItem(MENU_TITLE, priority = 15)]
        static void Init()
        {
            KEnv.Awake();
            LaneMakerWindow window = HasOpenInstances<VRoadWindow>()?
                GetWindow<LaneMakerWindow>(WINDOW_TITLE, true, typeof(VRoadWindow)):
                HasOpenInstances<VRoadEditorWindow>()?
                    GetWindow<LaneMakerWindow>(WINDOW_TITLE, true, GetWindow<VRoadEditorWindow>().GetType()):
                    GetWindow<LaneMakerWindow>(WINDOW_TITLE);
            window.Show();
            MeshTools.VRoadRoot(); // Check VRoad package location relative to Assets
            InitScenes();
        }
        
        private bool isPlaying;
        
        void OnGUI()
        {
            if (trySwitchScene) SwitchSceneTo(SCENE_LANEMAKER, EDSF.SCENE_PATH_REL_PRO);
            if (IsWindowActiveInCurrentScene())
            {
                isPlaying = EditorApplication.isPlaying;
               
            }
            
            float line = 2f;
            float nl = 5f;
            float columnWidth = Math.Min(300, position.width - (2 * marginX));
            GUILayout.BeginArea(AreaRect(line, nl, columnWidth), GUIStyle.none);
            {
                ActionButtonOrWarning();
            }
            GUILayout.EndArea();
            
            Footer(GRAT_PRO_SUBTITLE, URL_LABEL, URL_PAGE);

        }

        private void ActionButtonOrWarning()
        {
            bool correctScene = currentScene.Equals(SCENE_LANEMAKER);

            if (!correctScene)
            {
                EditorGUILayout.HelpBox(KFormat.Sprintf(EDSC.OPEN_SCENE_TO_ACTIVATE, SCENE_LANEMAKER), MessageType.Info);
                
                if (GUILayout.Button(KFormat.Sprintf(EDSC.OPEN_SCENE, SCENE_LANEMAKER), GUILayout.Height(helpBoxHeight)))
                {
                    trySwitchScene = true;
                }
                 
            }
            else
            {
                EditorGUILayout.HelpBox(EDSC.CORRECT_SCENE, MessageType.Info);

                if (isPlaying)
                {
                    GUI.enabled = false;
                    GUILayout.Button(VRoadWindow.HintFormat(EDSC.RUNNING), GUILayout.Height(helpBoxHeight));
                    GUI.enabled = true;
                }
                else
                {
                    string buttonAction = KFormat.Sprintf(EDSC.PLAY_SCENE, SCENE_LANEMAKER, SC.N);

                    string buttonTooltip = TOOLTIP_ACTION;
                    GUIContent buttonContent = new GUIContent(buttonAction, buttonTooltip);

                    if (GUILayout.Button(buttonContent, GUILayout.Height(helpBoxHeight)))
                    {
                        EditorApplication.isPlaying = true;
                    }
                }
            }

          
        }

        public override bool IsWindowActiveInCurrentScene()
        {
            return SCENE_LANEMAKER.Equals(currentScene);
        }

        void Update()
        {
            if (! IsWindowActiveInCurrentScene()) return;
            
            if (EditorApplication.isPlaying && LaneMaker.IsFinished )
            {
                Debug.Log("Finished, Stopping Scene");
                
                string path = MeshTools.VRoadRoot() + "/Materials/Lanes/Textures";
                
                // Load object
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object)); 
                // Select the object in the project folder
                Selection.activeObject = obj;
                // Also flash the folder yellow to highlight it
                EditorGUIUtility.PingObject(obj);
                
                
                EditorApplication.isPlaying = false;
            }
        }
    }
}