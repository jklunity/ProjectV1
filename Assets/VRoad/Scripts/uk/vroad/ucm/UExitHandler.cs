using uk.vroad.api;
using uk.vroad.apk;
using UnityEditor;
using UnityEngine;


namespace uk.vroad.ucm
{
    public class UExitHandler : MonoBehaviour
    {
        private static App quitThisApp;
        private int counter;
        
        // There are some exit handling functions in UaStateHandler because it has an App() reference
        void FixedUpdate()
        {
            if (quitThisApp != null)
            {
                int nAlive = KThreads.CountAlive(); // outside of test, to dispose of references to old threads

                if (nAlive == 0) QuitAfterTidyingUp();
                
                else if (nAlive == 1)
                {
                    ISim sim = quitThisApp.Sim();
                    if (sim != null)
                    {
                        object simMaster = sim.MasterThread();
                        lock (simMaster) { KMonitor.Pulse(simMaster); }
                    }
                }
            }
            else if (++counter % 100 == 0) KThreads.CountAlive(); // To dispose of references to old threads
        }

        public static void AppExitStatic(App app)
        {
            app?.Asc()?.Exit();

            quitThisApp = app;
        }

        private void QuitAfterTidyingUp()
        {
            Application.Quit(); // This is ignored by Editor

#if UNITY_EDITOR
            // Switch the Editor out of Play state if application is stopped from menu
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
#endif
        }

    }
}
