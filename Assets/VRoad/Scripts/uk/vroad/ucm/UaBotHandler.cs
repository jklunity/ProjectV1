﻿using System.Collections.Generic;
using uk.vroad.api;
using uk.vroad.api.enums;
using uk.vroad.api.etc;
using uk.vroad.api.events;
using uk.vroad.api.geom;
using uk.vroad.api.map;
using uk.vroad.api.route;
using uk.vroad.api.sim;
using uk.vroad.api.str;
using uk.vroad.apk;

using uk.vroad.pac;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace uk.vroad.ucm
{
    /// <summary> An abstract base class containing utilities to handle the bots (pedestrians, cars, trucks, etc)
    /// in the traffic simulation </summary>
    public abstract class UaBotHandler : MonoBehaviour, LAppState, LBotDepart, LBotArrive, LDynamicObject
    {
        [Tooltip("An array of all pedestrian models to use. If you want to replace the " +
                 "Unity standard robot with your own pedestrian models, drag their prefabs here")]
        public GameObject[] prefabPeds;
        [Tooltip("An array of car models to use for the traffic. Our basic example models " +
                 "should not be released with your application - replace them by dragging your own prefabs here")]
        public GameObject[] prefabCars;
        public GameObject[] prefabTaxis;
        public GameObject[] prefabBuses;
        public GameObject[] prefabCoaches;
        public GameObject[] prefabRigidTrucks;
        public GameObject[] prefabTractorTrucks;
        public GameObject[] prefabTrailers;

        public float animationMultiplier = 1.2f;
        
        private IVkl trackingVkl;
        private string trackingMsg;
        
        protected abstract AppStateTransition StartSimTransition();
        
        public static UaBotHandler Instance { get; private set; }
        private static readonly int IS_IDLE_ACTIVE = Animator.StringToHash(SA.Anim_isIdleActive);
        protected static readonly int IS_WALKING = Animator.StringToHash(SA.Anim_isWalking);

        protected static readonly Vector3 PED_LIFT_ABOARD_TAXI = new Vector3(0, 0.25f, 0);
        private static readonly Vector3 PED_LIFT_ABOARD_BUS = new Vector3(0, 0.40f, 0);
        
        protected GameObject uPedsTop;
        private GameObject uCarsTop;
        private GameObject uCoachesTop;
        private GameObject uTrucksTop;
        private GameObject uTaxisTop;
        private GameObject uBusesTop;
       
        
        protected SimBrake simBrake;

        protected bool destroyAllUBotsOnUpdate;
        readonly List<IBot> justDeparted = new List<IBot>();
        readonly KHashSet<IBit> justFinished = new KHashSet<IBit>();
       
        readonly KHash<IBit, GameObject> activeBitToGO = new KHash<IBit, GameObject>();
        readonly KHash<GameObject, IBit> activeGoToBit = new KHash<GameObject, IBit>();
        readonly KHashSet<IPed> walkingPeds = new KHashSet<IPed>();
        protected readonly KHash<IBus, KList<GameObject>> ballastUPeds = new KHash<IBus, KList<GameObject>>();

        private const int probabilityOfIdleAction = 50;

        protected KHash<IType, GameObject> typeToPrefab = new KHash<IType, GameObject>();
        
        private bool finished;
        private int fixedUpdatesPerTimeStep = 1;
        private int playerTweenFrame = 1;
        
        private Vector3 playerPedPositionOld = Vector3.zero;
        private Vector3 playerPedPositionNew = Vector3.zero;
        private GameObject playerPedUBot;
        private bool playerPedTweening;
        
        private Vector3 playerVklPositionOld = Vector3.zero;
        private Vector3 playerVklPositionNew = Vector3.zero;
        private GameObject playerVklUBot;
        private bool playerVklTweening;
        

        protected abstract App App();

        protected GameObject GetChild(string name)
        {
            for (int ci = 0; ci < transform.childCount; ci++)
            {
                GameObject cgoi = transform.GetChild(ci).gameObject;
                if (cgoi.name.Equals(name)) return cgoi;
            }

            GameObject newChild = new GameObject(name);
            newChild.transform.parent = transform;
            return newChild;
        }

        protected virtual void Awake()
        {
            Instance = this;
            App().AddEventConsumer(this);

            uPedsTop = GetChild(SA.VTOP_PEDS);
            uCarsTop = GetChild(SA.VTOP_CARS);
            uCoachesTop = GetChild(SA.VTOP_COACHES);
            uTrucksTop = GetChild(SA.VTOP_TRUCKS);
            uTaxisTop = GetChild(SA.VTOP_TAXIS);
            uBusesTop = GetChild(SA.VTOP_BUSES);

            CreateDrvTypesRigid(prefabCars, Purpose.Car);
            CreateDrvTypesRigid(prefabTaxis, Purpose.Taxi);
            CreateDrvTypesRigid(prefabRigidTrucks, Purpose.Truck);
            CreateDrvTypesRigid(prefabCoaches, Purpose.Coach);

            IType[] trailers = CreateDrvTypesTrailer(prefabTrailers, Purpose.Truck);

            CreateDrvTypesTractor(prefabTractorTrucks, Purpose.Truck, trailers);

            CreateBusTypes(prefabBuses);
            
        }

        public bool DeregisterFireMapChange()
        {
            return true;
        }

        public virtual void AppStateChanged(AppStateTransition ast)
        {
            if (ast == StartSimTransition() && simBrake == null)
            {
                simBrake = new SimBrake(App());
            }
            else if (ast.after == AppState.ReadyToSimulate)
            {
                App().Sim().SetCountPedAvatars(prefabPeds.Length);
            }
        }



        public IBit LookupBit(GameObject go)
        {
            if (go == null) return null;
            return activeGoToBit[go];
        }
        public GameObject LookupUBod(IBit bit)
        {
            if (bit == null) return null;
            return activeBitToGO[bit];
        }

        private void UpdateDisplay()
        {
            if (destroyAllUBotsOnUpdate)
            {
                destroyAllUBotsOnUpdate = false;
                GameObject[] keys = activeGoToBit.KeysAsArray();
                foreach (GameObject vgo in keys)
                {
                    Destroy(vgo);
                }

                activeGoToBit.Clear();
                activeBitToGO.Clear();
                
                justDeparted.Clear();
                justFinished.Clear();

                finished = true;
                playerPedTweening = false;
                playerVklTweening = false;
            }

            if (simBrake != null)
            {
                if (finished || simBrake.StepResultsWaiting())
                {
                    if (!finished) CreateMoveDestroyUBots();

                    simBrake.RenderingCompleted();
                }
            }
        }

        protected virtual void ConfigureNewPed(ITrip trip, IPed ped, GameObject ubod)
        {
            
        }

        private void CreateMoveDestroyUBots()
        {
            UaCamControllerMain cam = UaCamControllerMain.MostRecentInstance;
            trackingMsg = cam.autoTrack? "[ Locating vehicle, please wait... ]": "[ Car Cam is off ]";
            IBot[] jda;
            IBit[] jfa;
            
            lock (this)
            {
                jda = justDeparted.ToArray();
                justDeparted.Clear();

                jfa = justFinished.ToArray();
                justFinished.Clear();
            }

            // CREATE NEW
            foreach (IBot bot in jda)
            {
                if (activeBitToGO.ContainsKey(bot))  //.TryGetValue(bot, out GameObject _))
                {
                    continue;  // a taxi being re-used for another trip
                }

                ITrip trip = bot.GetTrip();
                if (trip == null)
                {
                    Debug.LogWarning("No trip for bot " + bot );
                    continue;
                }

              
                string tripName = trip.ToString();
                switch (bot)
                {
                    case IPed ped:
                    {
                        int pi = RandomAvatarIndex(ped); 

                        GameObject ubod = CreateNewUBod(bot, prefabPeds[pi], tripName);

                        ped.AvatarType(1+pi);

                        ConfigureNewPed(trip, ped, ubod);
                      
                        Animator animator = ubod.GetComponent<Animator>();
                        if (animator != null) animator.SetBool(IS_WALKING, false);
                        
                        break;
                    }
                    case IVkl vkl:
                    {
                        IType type = vkl.GetBitType();
                        
                        if ( ! typeToPrefab.ContainsKey(type))
                        {
                            // DrivingExample uses "SkyCar" type as GhostVkl, but it is not created here
                            if (! (vkl is IGhostVkl)) 
                                Debug.LogWarning("No prefab for type " + type);
                            continue;
                        }
                        GameObject prefab = typeToPrefab.Get(type);

                        GameObject uVkl = CreateNewUBod(bot, prefab, tripName);

                        IBit puller = bot;
                        int trailerIndex = 1;
                        while (puller.GetAttachment() != null)
                        {
                            IBit trailer = puller.GetAttachment();
                            IType trailerType = trailer.GetBitType();
                            
                            GameObject trailerPrefab = typeToPrefab.Get(trailerType);
                            if (trailerPrefab == null) break;
                            
                            CreateNewUBod(trailer, trailerPrefab, tripName +SC.TRAILER_SUFFIX +trailerIndex);

                            puller = trailer;
                            trailerIndex++;
                        }

                        if (type.IsBus())
                        {
                            IBus bus = (IBus)vkl;
                            int[] seatNos = bus.BallastRiderSeatNos();
                            int ns = seatNos.Length;
                            int npp = prefabPeds.Length;
                            
                            // Each seat that has a ballast passenger should (internally) store a negative value
                            // which is negated in bus.BallastAvatarType to return a positive value 1..N
                            for (int si = 0; si < ns; si++)
                            {
                                int seatNo = seatNos[si];
                                int bpti = bus.BallastAvatarType(seatNo); // returns value 1..NPP
                                int pi = bpti - 1; // reduce value to 0 .. NPP-1

                                if (pi < 0 || pi >= npp) 
                                    pi = 0;
                                
                                // The ballast passenger is created as a child of the uBus object (transform)
                                // so it does not need to be moved by our code when the bus moves,
                                // that will happen automatically
                               
                                CreateNewBallastPassenger(bus, seatNo, prefabPeds[pi], uVkl, PED_LIFT_ABOARD_BUS);
                            }
                        }
                        else if (App().IsPlayerVkl(vkl))
                        {
                            CreatePlayerBallast(vkl, uVkl);
                        }
                        
                        // Track a random vehicle, as in WebGL demo
                        if (cam.IsAutoTracking() && !cam.IsTracking() && cam.ReadyToTrack() && type.IsCar())
                        {
                            cam.TrackThis(uVkl);
                            trackingVkl = vkl;
                        }

                        break; // end case
                    }
                }
            }
            
            foreach (IBit bit in jfa)   // Destroy Finished Bots
            {
                /*
                if (bit is ITaxi taxi)  // when a Taxi arrives at a parking zone, do not destroy it
                {
                    IRoad road = taxi.GetRoad();
                    if (road.ExitsXU() > 0) // ... unless the road is a dead-end
                    {
                        if (road.GetZone() is ITaxiZone) continue;
                        if (taxi.GetDestination() is ITaxiZone) continue;
                    }
                }
                //*/
               
                if (bit is IPed ped) walkingPeds.Remove(ped);

                if (activeBitToGO.TryGetValue(bit, out GameObject ubitGO))
                {
                    if (cam.IsTracking()) { cam.UnTrackThis(ubitGO); }

                    activeBitToGO.Remove(bit);
                    activeGoToBit.Remove(ubitGO);

                    if (ubitGO == playerVklUBot) playerVklTweening = false;
                    
                    Destroy(ubitGO);
                }

                IBit rearmost = bit;
                while (rearmost.GetAttachment() != null)
                {
                    IBit trailer = rearmost.GetAttachment();

                    if (activeBitToGO.TryGetValue(trailer, out GameObject uTrailer))
                    {
                        activeBitToGO.Remove(trailer);
                        activeGoToBit.Remove(uTrailer);
                        Destroy(uTrailer);
                    }
                    rearmost = trailer;
                }

                ExtraFinish(bit);
            }

            foreach (IBit bit in activeBitToGO.Keys)
            {
                if (bit.Finished())
                {
                    justFinished.Add(bit); // this will destroy the GameObject
                    continue;
                }

                GameObject ubot = activeBitToGO[bit];

                if (ubot == null) continue;

              
                Vector3 position;
                Quaternion rotation;
                
                if (bit is IPed ped)
                {
                    ChangeMaterial(ped, ubot);
                    
                    position = ped.Centre().ToVector3(); 
                    rotation = Quaternion.LookRotation(ped.Forward().ToVector3());

                    bool isPlayer = App().IsPlayer(ped);

                    if (ped.IsAboard())
                    {
                        IBus bus = ped.GetBus();
                        ITaxi taxi = ped.GetTaxi();

                        if (bus != null) position += PED_LIFT_ABOARD_BUS;
                        else if (taxi != null) position += PED_LIFT_ABOARD_TAXI;

                        else if (isPlayer) // Do not show player inside car (not in a bus or a taxi)
                        {
                            ubot.SetActive(false);
                            continue;
                        }
                    }

                    if (isPlayer) 
                    {
                        if (!ped.IsAboard())
                        {
                            playerVklTweening = false;
                            
                            rotation = RotatePlayer(ped, rotation);

                            CameraFollowsPed(cam, ped, position);
                        }

                        playerPedUBot = ubot;
                        playerPedPositionOld = playerPedPositionNew;
                        playerPedPositionNew = position;
                       
                        if (playerPedTweening) position = playerPedPositionOld;
                        else playerPedPositionOld = position;

                        playerTweenFrame = 1;
                        playerPedTweening = true;
                    }
                    
                    // Animation setting for peds 
                    Animator animator = ubot.GetComponent<Animator>();
                    if (animator != null)
                    {
                        bool wasWalking = walkingPeds.Contains(ped);
                        bool isActivelyBoarding = false;

                        if (ped.IsBoarding())
                        {
                            IBus bus = ped.GetBus();
                            ITaxi taxi = ped.GetTaxi();
                            isActivelyBoarding = bus != null || (taxi != null && ped.TaxiBoardingQuotient(taxi) > 0);
                        }

                        bool isWalking = isActivelyBoarding || (!ped.IsAboard() && ped.Speed() > 0.1);

                        if (wasWalking != isWalking)
                        {
                            if (wasWalking) walkingPeds.Remove(ped);
                            else walkingPeds.Add(ped);

                            animator.SetBool(IS_WALKING, isWalking);

                            
                            if (isWalking)
                            {
                                animator.SetBool(IS_IDLE_ACTIVE, false);

                                animator.speed = (float) ped.Speed() / animationMultiplier;

                            }
                            else
                            {
                                if ( Rng.NextInt(Rng.Vein.SHAPES, 100) < probabilityOfIdleAction)
                                {
                                    animator.SetBool(IS_IDLE_ACTIVE, true);
                                    //idleAnimationStart.Put(ped, now);
                                }
                            }
                        }

                        else if (!isWalking)
                        {
                            // Set 'Has Exit Time' on Transition IdleActive -> Idle, and set Exit Time parameter
                            // in fold-down 'Settings' to 1.0 so that the animation plays one loop before checking
                            // on the boolean parameters.
                            //
                            // Thus even if we set idleActive to false here (which will happen on the next call in to 
                            // this method after the idle animation starts, this will no cause immediate termination of
                            // the animation
                            
                            animator.SetBool(IS_IDLE_ACTIVE, false);
                            
                            /*
                            double startedIdleAnimation = idleAnimationStart.Get(ped);
                            if (now > startedIdleAnimation + 10)
                            {
                                animator.SetBool(IS_IDLE_ACTIVE, false);

                                idleAnimationStart.Remove(ped);
                            }
                            //*/
                        }
                        
                        if (isWalking && animationMultiplier > 0.1) animator.speed = (float) ped.Speed() / animationMultiplier;
                    }
                }
                else if (bit is IVkl vkl)
                {
                    ChangeMaterial(vkl, ubot);

                    position = vkl.Centre().ToVector3(); // + halfHeight;
                    rotation = Quaternion.LookRotation(bit.ForwardGrad().ToVector3());

                    if (vkl == trackingVkl) trackingMsg = "Driving on " + trackingVkl.GetRoad().Description();

                    else if ((vkl is IGhostVkl gv && gv.IsPrimary())||
                             (vkl is IBus bus && App().IsPlayerBus(bus)))
                    {
                        CameraFollowsVkl(cam, vkl, position);

                        playerVklUBot = ubot;
                        playerVklPositionOld = playerVklPositionNew;
                        playerVklPositionNew = position;

                        if (playerVklTweening) position = playerVklPositionOld;
                        else playerVklPositionOld = position;
                        
                        playerTweenFrame = 1;
                        playerVklTweening = true;
                    }

                }
                else continue;
                
                ubot.transform.position = position;
                ubot.transform.rotation = rotation;
            }
        }

        protected virtual void ChangeMaterial(IVkl vkl, GameObject uvkl) { }
        protected virtual void ChangeMaterial(IPed ped, GameObject uPed) { }
        protected virtual void ExtraFinish(IBit bit) { }

        public string GetTrackingMsg() { return trackingMsg; }

        private void CameraFollowsPed(UaCamControllerMain cam, IPed ped, Vector3 position)
        {
            // Gather position, bearing and speed for use by camera
            Angle bearing = ped.Forward().AsBearing();
            IVkl vkl = ped.GetVkl();
            double speed = vkl != null ? vkl.Speed():ped.Speed();
           
            cam.PlayerPosition(position, bearing, speed, ped.IsAboard());
        }
        
        private void CameraFollowsVkl(UaCamControllerMain cam, IVkl vkl, Vector3 position)
        {
            // Gather position, bearing and speed for use by camera
            Angle bearing = vkl.Forward().AsBearing();
            double speed = vkl.Speed();
           
            cam.PlayerPosition(position, bearing, speed, true);
        }

        protected virtual Quaternion RotatePlayer(IPed ped, Quaternion rotation)  { return rotation; }

        public void SetFixedUpdatesPerTimeStep(int fupts) { if (fupts >= 1) fixedUpdatesPerTimeStep = fupts ;}
       
        protected virtual void FixedUpdate()
        {
            UpdateDisplay();

            if (playerPedTweening || playerVklTweening)
            {
                int tweenCount = fixedUpdatesPerTimeStep;
                float pTween = (float) playerTweenFrame++ / (float) tweenCount;
                
                if (playerPedTweening && playerPedUBot) playerPedUBot.transform.position = Vector3.Lerp(playerPedPositionOld, playerPedPositionNew, pTween);
                if (playerVklTweening && playerVklUBot) playerVklUBot.transform.position = Vector3.Lerp(playerVklPositionOld, playerVklPositionNew, pTween);
            }
        }

        protected virtual void CreatePlayerBallast(IVkl vkl, GameObject uVkl) { }

        public virtual void Depart(IBot bot)
        {
            lock (this)
            {
                justDeparted.Add(bot);
            }
        }

        public virtual void Arrive(IBot bot, IZone z)
        {
            if (bot is IPed ped && App().IsPlayer(ped)) UaCamControllerMain.MostRecentInstance.PlayerArrived();
        }

        public void ObjectCreated(object obj)
        {
            if (obj is IBot bot) justDeparted.Add(bot);
        }

        public void ObjectDeleted(object obj)
        {
            if (obj is IBot bot) justFinished.Add(bot);
        }
        
        protected virtual Transform GetParentTransform(IBit bit)
        {
            Transform parent;
            switch (bit)
            {
                case IPed _: parent = uPedsTop.transform; break;
                case ITaxi _:   parent = uTaxisTop.transform;  break;
                case IBus _:   parent = uBusesTop.transform; break;
                case IDrv drv:
                {
                    if (drv.IsCoach()) parent = uCoachesTop.transform;
                    else if (drv.IsTruck()) parent = uTrucksTop.transform;
                    else parent = uCarsTop.transform;
                    break;
                }
                default: // trailers will be here
                    parent = uTrucksTop.transform; 
                    break; 
            }

            return parent;
        }
        protected virtual GameObject CreateNewUBod(IBit bit, GameObject prefab, string bitName)
        {
            Transform parent = GetParentTransform(bit);
            
            Vector3 startPosition = bit.Centre().ToVector3();
            Quaternion rotation = Quaternion.LookRotation(bit.Forward().ToVector3());
            GameObject uBit = Instantiate(prefab, startPosition, rotation, parent);
            
            uBit.name = bitName;
            FireNewUBit(uBit);
            
            if (bit is IPed)
            {
                float pedHeight;
                CharacterController cc = uBit.GetComponent<CharacterController>();
                if (cc != null) pedHeight = cc.height;
                else pedHeight = BodBounds(prefab).y;
                
                float scale = (float) bit.Height() / pedHeight;
                
                Vector3 scaleVec = new Vector3(scale, scale, scale);
                uBit.transform.localScale = scaleVec;
            }

            else
            {
                SetupVehicleOnCreation(uBit);
            }
            activeBitToGO.Add(bit, uBit);
            activeGoToBit.Add(uBit, bit);

            return uBit;
        }

        protected virtual void SetupVehicleOnCreation(GameObject uBit) { } 
        
        protected virtual void FireNewUBit(GameObject uBit) { }
        

        // The ballast passenger GO will be created as a child of the bus GO (transform), so its relative position
        // needs to be set only once, and it will then move with the bus.
        protected void CreateNewBallastPassenger(IVkl vkl, int seatNo, GameObject prefab, GameObject uBus, Vector3 lift)
        {
            Xyz offsetAbs = vkl is IBus bus1? bus1.PositionInBus(seatNo): Xyz.ALLZERO;
            Vector3 startPositionAbs = vkl.Centre().Plus(offsetAbs).ToVector3();
            startPositionAbs += lift;
            Quaternion rotationAbs = Quaternion.Euler(0, (float) vkl.Forward().AsBearing().Degrees(), 0);
            GameObject uBallastPed = Instantiate(prefab, startPositionAbs, rotationAbs, uBus.transform);
            
            Animator animator = uBallastPed.GetComponent<Animator>();
            if (animator != null) animator.SetBool(IS_WALKING, false);

            if (vkl is IBus bus2)
            {
                KList<GameObject> bups = ballastUPeds.Get(bus2);
                if (bups == null) { bups = new KList<GameObject>(); ballastUPeds.Add(bus2, bups); }
                bups.Add(uBallastPed);
            }
        }

        protected virtual int RandomAvatarIndex(IPed ped) { return 0; }
        
        
        protected virtual int PlayerAvatarIndex() { return 0; }

        private static readonly Vector3 UNIT_SIZE = new Vector3(1, 1, 1);

        private Vector3 BodBounds(GameObject prefab)
        {
            Vector3 bounds = UNIT_SIZE;

            if (prefab.transform.childCount >= 1) //&& prefab.transform.GetChild(0).childCount >= 1)
            {
                Transform txChild0 = prefab.transform.GetChild(0);
                if (txChild0.childCount >= 1)
                {
                    GameObject grandChild0 = prefab.transform.GetChild(0).GetChild(0).gameObject;
                    Renderer rend = grandChild0.GetComponent<Renderer>();
                    if (rend != null) bounds = rend.bounds.size;
                }
                else
                {
                    GameObject child0 = txChild0.gameObject;
                    Renderer rend = child0.GetComponent<Renderer>();
                    if (rend != null) bounds = rend.bounds.size;
                }

            }

            return bounds;
        }

        /// <summary> Calculate and return the size of the (vehicle) prefab, x = width, y = height, z = length </summary>
        /// <param name="prefab"> The vehicle prefab</param>
        /// <returns> The size [  x = width, y = height, z = length ] </returns>
        private Vector3 RotatedScaledBounds(GameObject prefab)
        {
            // The prefab may be made up of several meshes, in different materials
            Bounds combinedBounds = new Bounds (prefab.transform.position, Vector3.zero);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer> ();
            foreach (Renderer renderer in renderers)
            {
                combinedBounds.Encapsulate (renderer.bounds);
            }

            // Vector3 size = combinedBounds.size;
            // Reporter.Report("Prefab %s W %.2f x H %.2f x L %.2f", prefab.name, size.x, size.y, size.z);

            return combinedBounds.size;
            
        }
        private void CreateDrvTypesRigid(GameObject[] prefabs, Purpose purpose)
        {
            foreach (GameObject prefab in prefabs)
            {
                Vector3 size = RotatedScaledBounds(prefab);
                
                IType kt = null;
                {
                    TypeSpecCar carSpec = prefab.GetComponent<TypeSpecCar>();
                    if (carSpec != null)
                    {
                        kt = VRoad.NewVehicleType(prefab.name, purpose, carSpec.abundance, carSpec.GetMotorType(),
                            size.z, size.x, size.y, carSpec.wheelBaseMetres, null, 0);
                    }
                }
                
                if (kt == null) // maybe it is a fixed-body truck
                {
                    TypeSpecTruck truckSpec = prefab.GetComponent<TypeSpecTruck>();
                    if (truckSpec != null)
                    {
                        kt = VRoad.NewVehicleType(prefab.name, purpose, truckSpec.abundance, truckSpec.GetMotorType(),
                            size.z, size.x, size.y, truckSpec.wheelBaseMetres, null, 0);
                    }
                }

                if (kt == null) // maybe it is a coach
                {
                    TypeSpecBus busSpec = prefab.GetComponent<TypeSpecBus>();
                    if (busSpec != null)
                    {
                        kt = VRoad.NewVehicleType(prefab.name, purpose, busSpec.abundance, busSpec.GetMotorType(),
                            size.z, size.x, size.y, busSpec.wheelBaseMetres, null, 0);
                    }
                }

                if (kt != null) typeToPrefab.Put(kt, prefab);
            }
        }
        private void CreateDrvTypesTractor(GameObject[] prefabs, Purpose purpose, IType[] trailers)
        {
            foreach (GameObject prefab in prefabs)
            {
                TypeSpecTruck spec = prefab.GetComponent<TypeSpecTruck>();
                if (spec == null) continue;
                Vector3 size = RotatedScaledBounds(prefab);
                
                foreach (IType trailer in trailers)
                {
                    IType kt = VRoad.NewVehicleType(prefab.name, purpose, spec.abundance, spec.GetMotorType(),
                        size.z, size.x, size.y, spec.wheelBaseMetres, trailer, spec.trailerOffset);
                    
                    typeToPrefab.Put(kt, prefab);
                }
            }
        }
        private IType[] CreateDrvTypesTrailer(GameObject[] prefabs, Purpose purpose)
        {
            int np = prefabs.Length;
            IType[] trailers = new IType[np];

            for (int pi = 0; pi < np; pi++)
            {
                GameObject prefab = prefabs[pi];
                
                TypeSpecTrailer spec = prefab.GetComponent<TypeSpecTrailer>();
                if (spec == null) continue;

                Vector3 size = RotatedScaledBounds(prefab);
                
                trailers[pi] = VRoad.NewTrailerType(prefab.name,  size.z, size.x, size.y, spec.trailerOffset);
                
                typeToPrefab.Put(trailers[pi], prefab);
            }

            return trailers;
        }

        private void CreateBusTypes(GameObject[] prefabs)
        {
            foreach (GameObject prefab in prefabs)
            {
                TypeSpecBus spec = prefab.GetComponent<TypeSpecBus>();
                if (spec == null) continue;

                Vector3 size = RotatedScaledBounds(prefab);
                
                IType bt = VRoad.NewBusType(prefab.name, spec.abundance, spec.GetMotorType(), 
                    size.z, size.x, size.y, spec.wheelBaseMetres);

                typeToPrefab.Put(bt, prefab);
            }
        }

        protected virtual void OnDestroy()
        {
            justDeparted.Clear();
            justFinished.Clear();
            activeBitToGO .Clear();
            activeGoToBit .Clear();
            walkingPeds.Clear();
            ballastUPeds.Clear();

            Instance = null;
        }
        /// ///////////////////////////////////////////////////////////////////////////////////
        
        // This 'brake' object controls the master simulation thread, holding it back
        // once a simulation step has been completed until the UBots (game objects)
        // have been created, moved or destroyed

        protected class SimBrake : LSimTimeStep
        {
            private readonly object lockObject;
            private bool stepResultsWaiting;

            public SimBrake(App app)
            {
                app.AddEventConsumer(this);

                
                lockObject = this;
                lockObject = app.Sim().MasterThread();
            }

            public bool DeregisterFireMapChange() { return true; }

            public void TimeStep()
            {
                stepResultsWaiting = true;

                lock (lockObject)
                {
                    KMonitor.Wait(lockObject);
                }
            }

            public bool StepResultsWaiting()
            {
                return stepResultsWaiting;
            }

            public void RenderingCompleted()
            {
                stepResultsWaiting = false;

                lock (lockObject)
                {
                    KMonitor.Pulse(lockObject);
                }

            }

           
        }
        
    }

}