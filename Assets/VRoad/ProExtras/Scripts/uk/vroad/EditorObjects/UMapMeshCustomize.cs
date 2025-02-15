using System.Collections.Generic;
using uk.vroad.api;
using uk.vroad.api.edit;
using uk.vroad.api.enums;
using uk.vroad.api.geom;
using uk.vroad.api.map;
using uk.vroad.api.str;
using uk.vroad.api.xmpl;
using uk.vroad.apk;
using uk.vroad.ucm;
using UnityEditor;
using UnityEngine;
using MH = uk.vroad.api.enums.MaterialHint;

namespace uk.vroad.EditorObjects
{
    /// <summary>
    /// Add MapPoints, Ignored OSM Ways and Moved CentreLines to map, for customization. <br/>
    /// See [Scene CustomizeMap] / [GameObject VRoadMap]
    /// </summary>
    public class UMapMeshCustomize : UaMapMeshMod
    {
        private const string MESH_IGNORED_WAYS = "IgnoredWays";
        private const string MESH_HI_LANES = "CustomizedRoads";
        private const string MESH_TURNS_NO_TRAFFIC =  "Turns_"+SX.NO_TRAFFIC;
        private const string MESH_TURNS_NO_BUSES_TRUCKS =  "Turns_NoBusesOrTrucks"; // c.f. SF.NO_LONGS
        private const string MESH_TURNS_NO_TRUCKS =  "Turns_"+SX.NO_TRUCKS;
        private const string DELETED_MAT = "Deletia";
        
        private const string EXTRA_FNS_TT = "Show some additional customization functions.\n" + 
               "These are undocumented, unsupported and may be withdrawn in future releases.\n" +
               "Enable this before playing scene for all functionality";

        // The editor layer is used for displaying editor-only drag handles in the scene view
        // It is disabled in all camera culling masks, so that objects appear int eh scene view but not in the game view
        public const int LAYER_EDITOR = 6;
        private const double IGNORED_WAY_WIDTH = 0.5;
        private const double IGNORED_WAY_LIFT = 0.3;

        
        private static readonly Color NO_TRAFFIC_COL = new Color(0.9f, 0.1f, 0.1f, 0.5f);
        private static readonly Color NO_BUSES_COL  = new Color(0.2f, 0.7f, 0.2f, 0.5f);
        private static readonly Color NO_TRUCKS_COL = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public static UMapMeshCustomize MostRecentInstance { get; private set;  }

        [Tooltip(EXTRA_FNS_TT)]
        public bool experimentalFunctions;

        public bool highlightEdits = true;
        
       
       

        private GameObject goMeshIgnoredWays;
        private GameObject goMeshHiLanes;
        
        private GameObject goMeshTurnsNoTraffic;
        private GameObject goMeshTurnsNoBusesOrTrucks;
        private GameObject goMeshTurnsNoTrucks;

        private Material deletedMaterial;

        
#if UNITY_EDITOR   // The methods in this class can be used in Editor Only
        protected override void Awake()
        {
            MostRecentInstance = this;

           
            base.Awake();

            deletedMaterial = NewMaterialTransparent(DELETED_MAT, Color.clear);

            goMeshIgnoredWays = NewGoMesh(MESH_IGNORED_WAYS, materialTemplates.opaque, HI_COL_100);
            goMeshHiLanes = NewGoMesh(MESH_HI_LANES, materialTemplates.transparent, HI_COL_050);
            
            goMeshTurnsNoTraffic = NewGoMesh(MESH_TURNS_NO_TRAFFIC, materialTemplates.transparent, NO_TRAFFIC_COL);
            goMeshTurnsNoBusesOrTrucks = NewGoMesh(MESH_TURNS_NO_BUSES_TRUCKS, materialTemplates.transparent, NO_BUSES_COL);
            goMeshTurnsNoTrucks = NewGoMesh(MESH_TURNS_NO_TRUCKS, materialTemplates.transparent, NO_TRUCKS_COL);
            
            SceneVisibilityManager svm = SceneVisibilityManager.instance;
           
            svm.DisablePicking(goMeshJunctions, true);
            svm.DisablePicking(goMeshTurnArrows, true);
            svm.DisablePicking(goMeshSignalBoxes, true);
            svm.DisablePicking(goMeshSignalArrows, true);
            svm.DisablePicking(goMeshSignalWalks, true);
            svm.DisablePicking(goMeshBusStops, true);
            svm.DisablePicking(goMeshParking, true);
            svm.DisablePicking(goMeshDropOff, true);
            svm.DisablePicking(goMeshFootpaths, true);
            svm.DisablePicking(goMeshCrossings, true);
            svm.DisablePicking(goMeshZebras, true);

            svm.DisablePicking(goMeshWater, true);
            svm.DisablePicking(goMeshTerrainSimple, true);
            svm.DisablePicking(goMeshBridges, true);
            svm.DisablePicking(goMeshTunnels, true);
            svm.DisablePicking(goMeshShoulders, true);
            svm.DisablePicking(goMeshEmbankments, true);
            svm.DisablePicking(goMeshBarriers, true);
            svm.DisablePicking(goMeshMedians, true);
           
            // svm.DisablePicking(goMeshTurnsRestricted, true);
            
            // Other picking is handled in FireNewGoMesh, AFTER children added to parent
        }

        private Vector3 Mpv3(IMapPoint mp, float lift)
        {
            return new Vector3((float) mp.X(), (float) mp.Z() + 0.5f * lift, (float) mp.Y());
        }
        private void OnDrawGizmos()
        {
            if (EditorApplication.isPlaying)
            {
                //float zoom = SceneView.currentDrawingSceneView.size; // == 2 x cameraDistance
                //float sz = Math.Min(Math.Max(0.2f, 0.1f * zoom), 2.0f);

                float lift = 1.0f;
                float ht = 0.1f;
                float szE = 1.2f;
                float szS = 2.0f;
                Vector3 sizeEnd = new Vector3(szE,ht,szE);
                Vector3 sizeStretch = new Vector3(szS,ht,szS);
                Gizmos.color = STRETCH_COL;
                foreach (IMapPoint mp in roadEndPointsA)     Gizmos.DrawWireCube(Mpv3(mp, lift), sizeEnd); 
                foreach (IMapPoint mp in roadStretchPointsA) DrawStretchGizmo(mp, lift, szS);
                foreach (IMapPoint mp in roadStretchPointsB) DrawStretchGizmo(mp, lift, szS);
                foreach (IMapPoint mp in roadEndPointsB)     Gizmos.DrawWireCube(Mpv3(mp, lift), sizeEnd); 
                
                // A wire gizmo is not selectable
                // If a solid gizmo is used, clicking on the gizmo selects the whole map (UaMapMesh)
                // For selection to work, this code would need to be in a script attached to each cube game object
            }
        }

        private void DrawStretchGizmo(IMapPoint mp, double lift, double halfLen)
        {
            IMapPoint ep = lookupEndPoint.Get(mp);
            Xyz se = ep.Location().Minus(mp.Location());
            Xyz side = new Xyz(se.Y(), -se.X(), 0).Normalized().MultipliedBy(halfLen);
            Xyz centre = mp.Location().PlusZ(0.5*lift);
            Gizmos.DrawLine(centre.Plus(side).ToVector3(), centre.Minus(side).ToVector3());

            centre = centre.PlusZ(0.1);
            Gizmos.DrawLine(centre.Plus(side).ToVector3(), centre.Minus(side).ToVector3());
            
            //Gizmos.DrawWireCube(Mpv3(mp, lift), sizeStretch);
        }
        protected override void FireNewGoMesh(GameObject goMesh)
        {
            if (goMesh == goMeshLanes) return;
            if (goMesh == goMeshRoadPoints) return;
            if (goMesh == goMeshPedPoints) return;
            if (goMesh == goMeshBuildPoints) return;
            if (goMesh == goMeshIgnoredWays) return;
            if (goMesh == goMeshFootpaths) return;
            if (goMesh == goMeshTurnsNoTraffic) return;
            if (goMesh == goMeshTurnsNoBusesOrTrucks) return;
            if (goMesh == goMeshTurnsNoTrucks) return;
            
            if (experimentalFunctions)
            {
                if (goMesh == goMeshSolidBuildings) return;
                if (goMesh == goMeshGlassBuildings) return;
            }
            SceneVisibilityManager.instance.DisablePicking(goMesh, true); 
            
        }

        protected override void ExtraMeshes(int progress)
        {
            CategorisePoints();
            bool extra = experimentalFunctions;
           
            CreateNamedMeshObjects(progress-7, goMeshRoadPoints, PointsNamedSubMeshArray(roadCentreLinePoints));
            CreateNamedMeshObjects(progress-6, goMeshPedPoints, PointsNamedSubMeshArray(pedwayCentreLinePoints));
            CreateNamedMeshObjects(progress-5, goMeshBuildPoints, PointsNamedSubMeshArray(extra?buildingPoints:null));
            CreateNamedMeshObjects(progress-4, goMeshIgnoredWays, IgnoredWaysSubMeshArray());
            CreateNamedMeshObjects(progress-3, goMeshTurnsNoTraffic, TurnArrowsRestricted(SX.NO_TRAFFIC));
            CreateNamedMeshObjects(progress-2, goMeshTurnsNoBusesOrTrucks, TurnArrowsRestricted(SX.NO_LONGS));
            CreateNamedMeshObjects(progress-1, goMeshTurnsNoTrucks, TurnArrowsRestricted(SX.NO_TRUCKS));
        }

        NamedSubMesh TurnArrowsRestricted(ITurn turn, string resName)
        {
            IRestriction r = turn.GetRestriction();

            if (r != null && r.ToString().Equals(resName))
            {
                IStreme[] sa = turn.GetStremes();
                TriMesh[] tma = new TriMesh[sa.Length];
                for (int si = 0; si < sa.Length; si++) { tma[si] = sa[si].TurnArrowTriMesh(); }

                return TriangleNamedSubMesh(turn.ToString(), TriMesh.Combine(tma), 0.1f);
            }

            return null;
        }
        NamedSubMesh[] TurnArrowsRestricted(string resName)
        {
            List<NamedSubMesh> nsml = new List<NamedSubMesh>();
            
            foreach (ITurn turn in App().Map().Turns())
            {
                NamedSubMesh nsm = TurnArrowsRestricted(turn, resName);
                
                if (nsm != null) nsml.Add(nsm);
            }

            return nsml.ToArray();
        }

     
      
        NamedSubMesh[] IgnoredWaysSubMeshArray()
        {
            double w = IGNORED_WAY_WIDTH;
            double dz = IGNORED_WAY_LIFT;
            List<NamedSubMesh> sml = new List<NamedSubMesh>();

            ICentreLine[] cla = App().Map().CentreLines();
            foreach (ICentreLine cl in cla)
            {
                if (cl.ToString().EndsWith(SX._IGNORED)) { sml.Add(TriangleNamedSubMesh(cl, cl.LineTriMesh(w, dz))); }
            }

            return sml.ToArray();
        }
      
        protected override void OnMeshCreationFinish()
        {
            SetLayerWholeTree(goMeshRoadPoints, LAYER_EDITOR);
            SetLayerWholeTree(goMeshPedPoints, LAYER_EDITOR);
            SetLayerWholeTree(goMeshBuildPoints, LAYER_EDITOR);
            SetLayerWholeTree(goMeshIgnoredWays, LAYER_EDITOR);
            SetLayerWholeTree(goMeshMovedLines, LAYER_EDITOR);
            
            App().Map().PrepareForEditing();
        }

        protected override void HighlightLane(ILane lane) // 20230206
        {
            //AddChild(goMeshHiLanes, LocusFullNamedMesh(lane,lane,0.4f,.6f,0.1f,MH.Lane), highlightGlassMaterial);
            AddChild(goMeshHiLanes, LocusFullNamedMesh(lane,lane,0,1,0.1f,MH.Lane), highlightGlassMaterial);
        }
        
        protected override void RebuildTurn(ITurn turn)
        {
            turn.RecalcRestrictions();
                    
            AddChild(goMeshTurnsNoTraffic, TurnArrowsRestricted(turn, SX.NO_TRAFFIC));
            AddChild(goMeshTurnsNoBusesOrTrucks, TurnArrowsRestricted(turn, SX.NO_LONGS));
            AddChild(goMeshTurnsNoTrucks, TurnArrowsRestricted(turn, SX.NO_TRUCKS));
        }
        
        protected override void DestroyLane(ILane lane)
        {
            foreach (ITurn turnO in lane.GetTurnO())
            {
                DestroyChild(goMeshTurnsNoTraffic, turnO.ToString());
                DestroyChild(goMeshTurnsNoBusesOrTrucks, turnO.ToString());
                DestroyChild(goMeshTurnsNoTrucks, turnO.ToString());
            }
            foreach (ITurn turnI in lane.GetTurnI())
            {
                DestroyChild(goMeshTurnsNoTraffic, turnI.ToString());
                DestroyChild(goMeshTurnsNoBusesOrTrucks, turnI.ToString());
                DestroyChild(goMeshTurnsNoTrucks, turnI.ToString());
            }
        }
        
       
        protected override GameObject GetParent(object mo)
        {
            if (mo is ICentreLine) return goMeshIgnoredWays;

            return null;
        }

        public void SetHighlightMaterial(object mo)
        {
            if (highlightEdits)
            {
                if (mo is IMapPoint) SetMaterial(mo, highlightSolidMaterial);
                else if (mo is ILane lane) HighlightLane(lane);
                else SetMaterial(mo, highlightGlassMaterial);
            }
        }
        public void SetDeletedMaterial(object mo) { SetMaterial(mo, deletedMaterial); }
#else
        protected override void ExtraMeshes(int progress) {}
#endif
    }
}