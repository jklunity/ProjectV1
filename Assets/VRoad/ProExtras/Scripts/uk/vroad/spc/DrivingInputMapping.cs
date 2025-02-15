using uk.vroad.api.input;
using uk.vroad.api.xmpl;
using uk.vroad.apk;

namespace uk.vroad.spc
{
    public class DrivingInputMapping : AppInputMapping
    {
        public static readonly uk.vroad.spc.DrivingInputMapping Simulating = new uk.vroad.spc.DrivingInputMapping(SQA.XIM_SIMULATING);

        protected internal DrivingInputMapping(string name)
            : base(name)
        {
            switch (name)
            {
                case SQA.XIM_SIMULATING:
                {
                    StoreMapping(GamePadAxes.LeftH, DrivingAnalogFn.SpecLane);
                    StoreMapping(GamePadAxes.LeftV, DrivingAnalogFn.SpecSpeed);
                    StoreMapping(GamePadAxes.RightH, AppAnalogFn.Rotate);
                    StoreMapping(GamePadAxes.RightV, AppAnalogFn.Zoom);
                    StoreMapping(GamePadButtons.Options_Start, AppDigitalFn.Pause);
                    StoreMapping(GamePadButtons.JoyL, DrivingDigitalFn.SpecReset);
                    StoreMapping(GamePadButtons.JoyR, DrivingDigitalFn.SpecRestart);
                    StoreMapping(GamePadButtons.ShoulderR, DrivingDigitalFn.SpecAutoSteer);
                    break;
                }
                default:
                {
                    break;
                }
            }
        }
    }
}
