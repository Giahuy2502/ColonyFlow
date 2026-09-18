using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    internal static class RestoreColonyFlowScene
    {
        private const string ScenePath = "Assets/_Game/Scenes/SampleScene.unity";
        private const string RevisionKey = "ColonyFlow.SceneRevision.14";
        private const string PendingKey = "ColonyFlow.SceneReloadPending";

        static RestoreColonyFlowScene()
        {
            EditorApplication.delayCall += RestoreSceneIfNeeded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += RestoreSceneIfNeeded;
        }

        private static void RestoreSceneIfNeeded()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool isBackup = activeScene.path.Replace('\\', '/').StartsWith("Temp/__Backupscenes/");
            bool requiresRevisionReload = !SessionState.GetBool(RevisionKey, false);
            bool pending = SessionState.GetBool(PendingKey, false);
            if (!isBackup && !requiresRevisionReload && !pending)
                return;

            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.ExitPlaymode();
                return;
            }

            SessionState.SetBool(RevisionKey, true);
            SessionState.SetBool(PendingKey, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
