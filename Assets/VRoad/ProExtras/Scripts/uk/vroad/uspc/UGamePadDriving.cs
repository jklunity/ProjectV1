using uk.vroad.api;
using uk.vroad.api.xmpl;
using uk.vroad.ucm;

namespace uk.vroad.uspc
{
    public class UGamePadDriving : UaGamePad
    {
        private App app;
        void Awake()
        {
            app = ExampleApp.AwakeInstance();
        }

        protected override App App() { return app; }

       
    }
}