using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LateSubmission.Editor
{
    public static class DoorSetupHelper
    {
        [MenuItem("Tools/Late Submission/Measure Door Bounds")]
        public static void MeasureBounds()
        {
            string[] names = { "Entrance_Door_Spawn", "Exit_Door_Spawn", "Security_Door", "door_2", "door_2 (1)", "table" };
            foreach (var name in names)
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    var props = GameObject.Find("Props_Spawn");
                    if (props != null)
                    {
                        var t = props.transform.Find(name);
                        if (t != null) go = t.gameObject;
                    }
                }

                if (go != null)
                {
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds b = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                        {
                            b.Encapsulate(renderers[i].bounds);
                        }
                        Vector3 localCenter = go.transform.InverseTransformPoint(b.center);
                        Vector3 localSize = go.transform.InverseTransformVector(b.size);
                        // Abs localSize because scale or rotation could invert signs
                        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                        Debug.Log($"[Bounds] '{go.name}' WorldBounds: Center={b.center}, Size={b.size} | LocalCenter={localCenter}, LocalSize={localSize}");
                    }
                    else
                    {
                        Debug.Log($"[Bounds] '{go.name}' has no renderers.");
                    }
                }
            }
        }
    }
}
