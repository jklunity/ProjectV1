using System;
using uk.vroad.api;
using uk.vroad.api.enums;
using uk.vroad.api.etc;
using uk.vroad.api.events;
using uk.vroad.api.geom;
using uk.vroad.api.map;
using uk.vroad.api.route;
using uk.vroad.api.sim;
using uk.vroad.api.xmpl;
using uk.vroad.apk;
using uk.vroad.pac;
using uk.vroad.spc;
using uk.vroad.ucm;
using unity.examples;
using UnityEngine;
using UnityEngine.UI;

namespace uk.vroad.uspc
{
    public class UPlayerCar : MonoBehaviour, LAppState, LSimTimeStep
    {
        public UaMapMesh mapMesh;
        public GameObject carModel;
        public float steerSensitivity = 0.05f;
        public GameObject targetSphere;
        public bool showGhostCubes;
        public GameObject ghostCube1;
        public GameObject ghostCube2;
        public Text steeringText;
        public Text streetNameText;
       
        private ExampleApp app;
        private DrivingInputHandler dih;
        private IGhostPack ghostPack;
        private bool restart;
        private bool reset;
        private bool isReady;
        private string ghostName;
        private Vector3 ghostSize;
        private Xyz ghostCentre;
        private Xyz ghostForward;
        
        private CarController unityCarController; // the car controller we want to use

        public static UPlayerCar MostRecentInstance { get; private set; }

        void Awake()
        {
            app = ExampleApp.AwakeInstance();
            MostRecentInstance = this;
            app.AddEventConsumer(this);
            dih = DrivingInputHandler.Awake(app);
            app.SetInputHandler(dih);
            
            unityCarController = GetComponent<CarController>();

            MeshRenderer[] mra = carModel.GetComponentsInChildren<MeshRenderer>();
            Bounds ghostBounds = mra[0].bounds;
            for (int mi = 1; mi < mra.Length; mi++)
            {
                Bounds b = mra[mi].bounds;
                ghostBounds.Encapsulate(b.min);
                ghostBounds.Encapsulate(b.max);
            }

            Vector3 center = ghostBounds.center;
            ghostSize = ghostBounds.size + new Vector3(0.5f, 0, 1.0f);
            
            
            ghostCube1.transform.position = center;
            ghostCube1.transform.localScale = ghostSize;

            ghostCube2.transform.position = center;
            ghostCube2.transform.localScale = ghostSize;

            ghostName = carModel.name; // save this in main thread
            ghostCentre = center.ToXyz();
            ghostForward = Xyz.NORTH;
        }

        public bool DeregisterFireMapChange()
        {
            return true;
        }

        public void AppStateChanged(AppStateTransition transition)
        {
            if (transition.after == AppState.ReadyToSimulate) Init();
        }
        private void Init()
        {
            double width = ghostSize.x;
            double height = ghostSize.y;
            double length = ghostSize.z;
            // Could work out wheelbase by looking for wheel objects, but turning is controlled by Unity
            double wheelBase = length * 0.6; 
            IType ghostType = VRoad.NewVehicleType(ghostName, Purpose.Car, 0.000001, MotorType.ElectricCar,
                length, width, height, wheelBase, null, 0);
           
            // UaBotHandler.Instance.SetGhostType(ghostType);
            
            ILane lane = chooseGhostDepartureLane();
            ghostPack = VRoad.NewGhostPack(app, ghostType, lane);
            restart = true;
        }


        private ILane chooseGhostDepartureLane()
        {
            IDrvZone[] dza = app.Map().DrvZones();
            foreach (IDrvZone dz in dza)
            {
                IBranch[] ba = dz.DepartureBranches();
                if (ba.Length > 0 && ba[0] is IRoad) return ((IRoad) ba[0]).GetKerbLane();
            }

            return null;
        }

        private void FixedUpdate()
        {
            if (!isReady)
            {
                isReady = ghostPack != null && unityCarController != null && mapMesh.MeshesCreated();
                if (!isReady) return;
            }

            targetSphere.transform.position = ghostPack.PrimaryTarget().ToVector3();

            Angle ghostBearing = ghostPack.Primary().Forward().AsBearing();
            Angle angleToTarget = ghostPack.AngleToTarget();
            float targetAngle = (float) angleToTarget.Minus(ghostBearing).RangeN180().Degrees();

            double speed_MPS = unityCarController.CurrentSpeedMPS;
            float steerMax = (float) (1.0 - (speed_MPS / (unityCarController.TopSpeedMPS + 5f)));
            float steerAuto = dih.FreeSteer()? 0: Mathf.Clamp(targetAngle * steerSensitivity, -steerMax, steerMax);
            
            float steerInput = Mathf.Clamp((float) dih.Steer(), -steerMax, steerMax);
            float steerManual = 2f * steerInput; 

          
            string steerMsg = dih.FreeSteer() ? "[Free Steering]" : "Guided Steering";
            
            //if (! dih.FreeSteer()) steerMsg = KFormat.Sprintf("sp %.2f A %.2f M %.2f X %.2f",
            //       speed_MPS, steerAuto, steerManual, steerMax);
            
            steeringText.text = steerMsg;
            
            float steer = Mathf.Clamp(steerAuto +  steerManual, -1, 1);

            float acc = (float) dih.Acc();

            float brake = acc > 0 ? 0 : acc;
            float accF = acc > 0 ? acc : 0;
            
            unityCarController.Move(steer, accF, brake, 0);

            if (reset)
            {
                reset = false;

                Xyz lpt = ghostPack.Reset();
                
                transform.position = lpt.ToVector3() + (0.4f * Vector3.up);
                transform.rotation = Quaternion.LookRotation(angleToTarget.UnitVectorXY().ToVector3());

            }
        }

        private void Update()
        {
            if (!isReady) return;
        
            double halfGhostHeight = 0.5 * ghostSize.y;

            IVkl v1 = ghostPack.Primary();
            
            
            if (restart)
            {
                restart = false;
                gameObject.transform.position = v1.Centre().PlusZ(halfGhostHeight).ToVector3();
                gameObject.transform.rotation = Quaternion.LookRotation(v1.ForwardGrad().ToVector3());
                GetComponent<Rigidbody>().useGravity = true;
            }

            ghostCentre = gameObject.transform.position.ToXyz().PlusZ(-halfGhostHeight);
            ghostForward = gameObject.transform.forward.ToXyz();

            if (showGhostCubes)
            {
                ghostCube1.transform.position = v1.Centre().PlusZ(halfGhostHeight).ToVector3();
                ghostCube1.transform.rotation = Quaternion.LookRotation(v1.ForwardGrad().ToVector3());
                ghostCube1.SetActive(true);

                if (ghostPack.Secondary().IsInUse())
                {
                    IVkl v2 = ghostPack.Secondary();
                    ghostCube2.transform.position = v2.Centre().PlusZ(halfGhostHeight).ToVector3();
                    ghostCube2.transform.rotation = Quaternion.LookRotation(v2.ForwardGrad().ToVector3());
                    ghostCube2.SetActive(true);

                }
                else ghostCube2.SetActive(false);
            }
            else
            {
                ghostCube1.SetActive(false);
                ghostCube2.SetActive(false);
            }

            IRoad rd = v1.GetRoad();
            streetNameText.text = rd == null? "[Off-Road]": rd.Description();
        }

        private IJunction junctionPaused;
        
        public void TimeStep()
        {
            if (!restart) ghostPack.SetCentreForward(ghostCentre, ghostForward);

            if (dih.RestartNow())
            {
                ghostPack.Restart(DrivingInputHandler.RandomStartLane(app));
                restart = true;
            }
          
            else if (dih.ResetNow())
            {
                reset = true;
            }

            else
            {
                bool onLocus = ghostPack.CheckSwitchLocus();

                if (onLocus)
                {
                    ILocus loc = ghostPack.Primary().GetLocus();
                    if (loc is IStreme s)
                    {
                        IJunction jn = s.GetJunction();
                        if (jn != junctionPaused)
                        {
                            if (junctionPaused != null) junctionPaused.Stopped(false);
                            junctionPaused = jn;
                            junctionPaused.Stopped(true);
                        }
                    }
                    else if (junctionPaused != null)
                    {
                        junctionPaused.Stopped(false);
                        junctionPaused = null;
                    }
                }
                //else dih.FreeSteer(true);
            }
            
        }
    }
}