using uk.vroad.api;
using uk.vroad.api.etc;
using uk.vroad.api.input;
using uk.vroad.api.map;
using uk.vroad.api.route;
using uk.vroad.api.xmpl;
using uk.vroad.apk;

namespace uk.vroad.spc
{
    public class DrivingInputHandler : AppInputHandler
    {
        public static uk.vroad.spc.DrivingInputHandler Awake(App ap)
        {
            lock (typeof(DrivingInputHandler))
            {
                return new uk.vroad.spc.DrivingInputHandler(ap);
            }
        }

        private DrivingInputHandler(App ap)
            : base(ap, DrivingInputMapping.Simulating)
        {
        }

        public override AppStateTransition MenuClosingTransition()
        {
            if (CurrentMenu() == ExamplePauseMenu.ViewMenu) return ExampleStateTransition.resumeToSimulating;
            return null;
        }

        public override void AppStateChanged(AppStateTransition transition)
        {
            if (transition.after == ExampleState.Simulating) 
            {
                SetCurrentMapping(DrivingInputMapping.Simulating);
                SetCurrentMenu(AppPauseMenu.NoMenu);
            }
            else if (transition.after == ExampleState.PausedWhileSimulating) 
            {
                SetCurrentMapping(AppInputMapping.Paused);
                SetCurrentMenu(ExamplePauseMenu.ViewMenu);
            }
            else base.AppStateChanged(transition);
        }

        public override bool AppInputDigitalEvent(AppDigitalFn dfn, bool isPressed)
        {
            if (dfn == DrivingDigitalFn.SpecAutoSteer) 
            {
                if (isPressed) FreeSteerToggle();
            }
            if (dfn == DrivingDigitalFn.SpecReset) 
            {
                if (isPressed) resetLater = true;
            }
            if (dfn == DrivingDigitalFn.SpecRestart) 
            {
                if (isPressed) restartLater = true;
            }
            return false;
        }

        public override bool AppInputAnalogEvent(AppAnalogFn afn, double value)
        {
            if (afn == DrivingAnalogFn.SpecLane) 
            {
                steer = value;
                return true;
            }
            if (afn == DrivingAnalogFn.SpecSpeed) 
            {
                acc = value;
                return true;
            }
            return false;
        }
        private double acc;
        private double steer;
        private bool freeSteer;
        private bool resetLater;
        private bool restartLater;

        public virtual double Acc()
        {
            return acc;
        }

        public virtual double Steer()
        {
            return steer;
        }

        public virtual void FreeSteerToggle()
        {
            freeSteer = !freeSteer;
        }

        public virtual bool FreeSteer()
        {
            return freeSteer;
        }

        public virtual void FreeSteer(bool v)
        {
            freeSteer = v;
        }

        public virtual bool ResetNow()
        {
            if (resetLater) 
            {
                resetLater = false;
                return true;
            }
            return false;
        }

        public virtual bool RestartNow()
        {
            if (restartLater) 
            {
                restartLater = false;
                return true;
            }
            return false;
        }

        public static ILane RandomStartLane(App app)
        {
            IDrvZone[] dza = app.Map().DrvZones();
            int ndz = dza.Length;
            while (ndz > 0 && dza[ndz - 1] is ITaxiZone)
            {
                ndz--;
            }
            if (ndz == 0) return null;
            int rz = Rng.NextInt(Rng.Vein.PLAYER, ndz);
            for (int zi = 0; zi < ndz; zi++)
            {
                int rzi = (zi + rz) % ndz;
                IDrvZone dz = dza[rzi];
                IBranch[] ba = dz.DepartureBranches();
                if (ba.Length > 0 && ba[0] is IRoad) return ((IRoad)ba[0]).GetKerbLane();
            }
            return null;
        }
    }
}
