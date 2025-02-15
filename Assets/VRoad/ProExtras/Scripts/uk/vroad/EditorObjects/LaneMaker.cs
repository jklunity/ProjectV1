using System;
using uk.vroad.api.str;
using uk.vroad.ucm;
using UnityEngine;

namespace uk.vroad.EditorObjects
{
    public class LaneMaker: MonoBehaviour
    {
#if UNITY_EDITOR
        public Texture2D halfLaneBlank;
        public Texture2D halfLaneRoadEdge;
        public Texture2D halfLaneFlowWith;
        public Texture2D halfLaneFlowAgainst;

        public Material materialTemplate;

        public string prefix = "Style1_";
        
        private Material[] laneMaterials;

        public static bool IsFinished { get; private set; }

        // This class can be used in the editor only

        void Awake()
        {
            
            
            if (IsSuitableForLane(halfLaneBlank, SA.LANE_TEX_BLANK) && 
                IsSuitableForLane(halfLaneRoadEdge, SA.LANE_TEX_EDGE) &&
                IsSuitableForLane(halfLaneFlowWith, SA.LANE_TEX_FLOW_WITH) &&
                IsSuitableForLane(halfLaneFlowAgainst, SA.LANE_TEX_FLOW_AGAINST))
            {
                string[] labels = SA.MARKING_LABEL;
                int li = 0;
                
                Texture2D junc_junc = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D edge_edge = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D edge_with = new Texture2D(512, 512){ name = labels[li++] };  // actually edge_blank, to prevent dash duplication
                Texture2D edge_cntr = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D with_edge = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D with_with = new Texture2D(512, 512){ name = labels[li++] }; // actually with_blank, to prevent dash duplication
                Texture2D with_cntr = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D cntr_edge = new Texture2D(512, 512){ name = labels[li++] };
                Texture2D cntr_with = new Texture2D(512, 512){ name = labels[li++] }; // actually cntr_blank, to prevent dash duplication

                for (int w = 0; w < 256; w++)
                {
                    for (int h = 0; h < 512; h++)
                    {
                        Color junc = halfLaneBlank.GetPixel(w, h);
                        Color edge = halfLaneRoadEdge.GetPixel(w, h);
                        Color with = halfLaneFlowWith.GetPixel(w, h);
                        Color cntr = halfLaneFlowAgainst.GetPixel(w, h);
                        
                        junc_junc.SetPixel(h, 511-w, junc);
                        
                        edge_edge.SetPixel(h, 511-w, edge);
                        edge_with.SetPixel(h, 511-w, edge);
                        edge_cntr.SetPixel(h, 511-w, edge);
                        
                        with_edge.SetPixel(h, 511-w, with);
                        with_with.SetPixel(h, 511-w, with);
                        with_cntr.SetPixel(h, 511-w, with);
                        
                        cntr_edge.SetPixel(h, 511-w, cntr);
                        cntr_with.SetPixel(h, 511-w, cntr);
                    }
                }

                for (int w = 0; w < 256; w++)
                {
                    for (int h = 0; h < 512; h++)
                    {
                        Color junc = halfLaneBlank.GetPixel(w, h);
                        Color edge = halfLaneRoadEdge.GetPixel(w, h);
                        Color with = halfLaneBlank.GetPixel(w, h);     // actually blank
                        Color cntr = halfLaneFlowAgainst.GetPixel(w, h);
                        
                        junc_junc.SetPixel(h, w, junc);
                        
                        edge_edge.SetPixel(h, w, edge);
                        edge_with.SetPixel(h, w, with);
                        edge_cntr.SetPixel(h, w, cntr);
                        
                        with_edge.SetPixel(h, w, edge);
                        with_with.SetPixel(h, w, with);
                        with_cntr.SetPixel(h, w, cntr);
                        
                        cntr_edge.SetPixel(h, w, edge);
                        cntr_with.SetPixel(h, w, with);
                    }
                }
                
                junc_junc.Apply();
                
                edge_edge.Apply();
                edge_with.Apply();
                edge_cntr.Apply();
                
                with_edge.Apply();
                with_with.Apply();
                with_cntr.Apply();
                        
                cntr_edge.Apply();
                cntr_with.Apply();
                
                Texture[] laneTex = new Texture[]
                {
                    junc_junc,   
                    edge_edge,
                    edge_with,
                    edge_cntr,
                    with_edge,
                    with_with,
                    with_cntr,
                    cntr_edge,
                    cntr_with,
                };
                
                laneMaterials = new Material[laneTex.Length];
                
                for (int mi = 0; mi < laneTex.Length; mi++)
                {
                    string label = SA.MARKING_LABEL[mi];
                    Texture tex = laneTex[mi];
                    laneMaterials[mi] =  new Material(materialTemplate) { mainTexture = tex, name = tex.name };
                }
                
                try
                {
                    string matPath = MeshTools.VRoadRoot() + "/Materials/Lanes/";
                    string texPath = matPath + "Textures/";
                    
                    UnityEditor.AssetDatabase.StartAssetEditing();

                    for (int mi = 0; mi < laneTex.Length; mi++)
                    {
                        Texture tex = laneTex[mi];
                        
                        UnityEditor.AssetDatabase.CreateAsset(tex, texPath + prefix + tex.name+ SA.SUFFIX_TEX);
                    }

                    foreach (Material mat in laneMaterials)
                    {
                        UnityEditor.AssetDatabase.CreateAsset(mat, matPath + prefix + mat.name+ SA.SUFFIX_MAT);
                    }
                }
                finally
                {
                    UnityEditor.AssetDatabase.StopAssetEditing();
                    UnityEditor.AssetDatabase.SaveAssets();
                }

            }
            else
            {

                Debug.LogWarning(SA.LANE_TEX_WARNING);


            }

            IsFinished = true;

        }
        
        private bool IsSuitableForLane(Texture2D tex, string label)
        {
            if (tex != null && tex.width == 256 && tex.height == 512 && tex.isReadable) return true;
            

            Debug.LogWarning(SA.LANE_TEX_UNSUITABLE+label); 

            return false;
        }
#endif
    }
    
   

}