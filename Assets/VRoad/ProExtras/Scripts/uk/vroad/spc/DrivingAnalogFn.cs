using uk.vroad.api.input;
using uk.vroad.apk;

namespace uk.vroad.spc
{
    public class DrivingAnalogFn : AppAnalogFn
    {
        public static readonly DrivingAnalogFn SpecLane = new DrivingAnalogFn();
        public static readonly DrivingAnalogFn SpecSpeed = new DrivingAnalogFn();
        private static bool regG = false;

        public override string ToString()
        {
            if (!regG) 
            {
                regG = true;
                ADbr.RegisterStaticObjectNames(SpecLane);
            }
            return base.ToString();
        }
    }
}
