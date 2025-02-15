using uk.vroad.api.input;
using uk.vroad.apk;

namespace uk.vroad.spc
{
    public class DrivingDigitalFn : AppDigitalFn
    {
        public static DrivingDigitalFn SpecAutoSteer = new DrivingDigitalFn();
        public static DrivingDigitalFn SpecReset = new DrivingDigitalFn();
        public static DrivingDigitalFn SpecRestart = new DrivingDigitalFn();
        private static bool regG = false;

        public override string ToString()
        {
            if (!regG) 
            {
                regG = true;
                ADbr.RegisterStaticObjectNames(SpecAutoSteer);
            }
            return base.ToString();
        }
    }
}
