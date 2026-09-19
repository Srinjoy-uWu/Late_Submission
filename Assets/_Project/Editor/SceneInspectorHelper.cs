using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace LateSubmission.Editor
{
    public static class SceneInspectorHelper
    {
        [MenuItem("Tools/Late Submission/Inspect Active Scene")]
        public static void InspectActiveScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[SceneInspector] Active Scene: '{activeScene.name}' (Path: '{activeScene.path}', isDirty: {activeScene.isDirty})");

            GameObject[] roots = activeScene.GetRootGameObjects();
            Debug.Log($"[SceneInspector] Root GameObjects count: {roots.Length}");
            foreach (var root in roots)
            {
                PrintHierarchy(root, 0);
            }
        }

        private static void PrintHierarchy(GameObject go, int depth)
        {
            string indent = new string('-', depth * 2);
            Debug.Log($"[SceneInspector] {indent} {go.name} (Active: {go.activeSelf})");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                PrintHierarchy(go.transform.GetChild(i).gameObject, depth + 1);
            }
        }
    }
}
