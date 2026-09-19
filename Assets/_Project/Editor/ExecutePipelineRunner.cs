using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LateSubmission.Editor
{
    [InitializeOnLoad]
    public static class ExecutePipelineRunner
    {
        private static bool _hasExecuted = false;

        static ExecutePipelineRunner()
        {
            EditorApplication.delayCall += ExecuteOnce;
        }

        [MenuItem("Tools/Late Submission/Run Complete Setup Pipeline")]
        public static void RunManually()
        {
            _hasExecuted = false;
            ExecuteOnce();
        }

        private const string SessionKey = "LateSubmission_Pipeline_Ran_Session";

        private static void ExecuteOnce()
        {
            if (_hasExecuted) return;
            _hasExecuted = true;

            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }
            SessionState.SetBool(SessionKey, true);

            Debug.Log("<color=yellow>=== [Late Submission Pipeline] Running Full Setup ===</color>");

            try
            {
                // Step 1: Build Security Room & Floor 2
                SecurityRoomBuilder.BuildSecurityRoomScene();

                // Step 2: Open Floor01_Main and configure all doors & systems
                EditorSceneManager.OpenScene("Assets/Scenes/Floor01_Main.unity");
                Floor01SetupPipeline.SetupFloor1DoorsAndScene();

                Debug.Log("<color=green>=== [Late Submission Pipeline] Full Setup Completed Successfully! ===</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Late Submission Pipeline] Exception during setup: {ex}");
            }
        }
    }
}
