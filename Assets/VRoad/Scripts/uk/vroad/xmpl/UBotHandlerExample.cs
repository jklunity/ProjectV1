using uk.vroad.api;
using uk.vroad.api.xmpl;
using uk.vroad.ucm;
using UnityEngine;

namespace uk.vroad.xmpl
{
    /// <summary> A simple concrete example of a Bot Handler </summary>
    /// A more complex example is included in the Pro Variant
    public class UBotHandlerExample : UaBotHandler
    {
        private ExampleApp app;
        private bool useBoxColliders = false;
        
        protected override void Awake()
        {
            app = ExampleApp.AwakeInstance();
            base.Awake();
        }
        protected override App App() { return app; }
        protected override AppStateTransition StartSimTransition() { return ExampleStateTransition.runSimulation; }

        protected override void SetupVehicleOnCreation(GameObject uBit)
        {
            if (useBoxColliders) uBit.transform.GetChild(0).gameObject.AddComponent<BoxCollider>();
        }
    }
}