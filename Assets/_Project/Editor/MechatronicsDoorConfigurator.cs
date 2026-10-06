using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LateSubmission.Interaction;
using LateSubmission.Inventory;

namespace LateSubmission.Editor
{
    [InitializeOnLoad]
    public static class MechatronicsDoorConfigurator
    {
        // [InitializeOnLoadMethod]
        // public static void AutoRun()
        // {
        //     EditorApplication.delayCall += ConfigureMechatronicsDoor;
        // }

        [MenuItem("Tools/Late Submission/Doors/Configure Mechatronics Door Complete")]
        public static void ConfigureMechatronicsDoor()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Floor02_Main")
            {
                Debug.LogWarning($"[MechatronicsDoorConfigurator] Active scene is '{activeScene.name}', opening 'Assets/Scenes/Floor02_Main.unity'...");
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/Floor02_Main.unity", OpenSceneMode.Single);
            }

            Debug.Log($"<color=cyan>==================== [CONFIGURING MECHATRONICS DOOR] ====================</color>");

            // 1. Search for any door object in Mechatronics lab area
            var allGos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject targetDoor = null;

            // First check if user named it "Mechatronics_Lab_Door"
            foreach (var go in allGos)
            {
                if (go.name == "Mechatronics_Lab_Door")
                {
                    targetDoor = go;
                    break;
                }
            }

            // If not found, check "Door_MechatronicsLab"
            if (targetDoor == null)
            {
                foreach (var go in allGos)
                {
                    if (go.name == "Door_MechatronicsLab")
                    {
                        targetDoor = go;
                        break;
                    }
                }
            }

            // Fallback: check any door object near (-32, 0, 2)
            if (targetDoor == null)
            {
                foreach (var go in allGos)
                {
                    if (go.name.ToLower().Contains("door") && Vector3.Distance(go.transform.position, new Vector3(-32f, 0f, 2f)) < 3.5f)
                    {
                        targetDoor = go;
                        break;
                    }
                }
            }

            if (targetDoor == null)
            {
                Debug.LogError("[MechatronicsDoorConfigurator] Could not find any Mechatronics door object!");
                return;
            }

            targetDoor.name = "Door_MechatronicsLab";
            Debug.Log($"[MechatronicsDoorConfigurator] Target door found: '{targetDoor.name}' at pos={targetDoor.transform.position}, rot={targetDoor.transform.rotation.eulerAngles}");

            // 2. Remove any other duplicate doors in Mechatronics area
            foreach (var go in allGos)
            {
                if (go != null && go != targetDoor && go.name.ToLower().Contains("mechatronics") && go.name.ToLower().Contains("door"))
                {
                    Debug.LogWarning($"[MechatronicsDoorConfigurator] Removing duplicate door '{go.name}'");
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            // 3. Ensure no solid colliders on the root object
            foreach (var col in targetDoor.GetComponents<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // 4. Create or setup DoorLeaf hinge parent
            Transform doorLeaf = targetDoor.transform.Find("DoorLeaf");
            if (doorLeaf == null)
            {
                // Look for existing leaf objects (e.g. Door, door_2_LOD0, grid_2)
                var existingDoor = targetDoor.transform.Find("Door");
                if (existingDoor != null)
                {
                    doorLeaf = existingDoor;
                }
                else
                {
                    var leafGo = new GameObject("DoorLeaf");
                    doorLeaf = leafGo.transform;
                    doorLeaf.SetParent(targetDoor.transform, false);

                    // Position at the hinge point
                    var lod0 = targetDoor.transform.Find("door_2_LOD0");
                    if (lod0 != null)
                    {
                        doorLeaf.localPosition = new Vector3(lod0.localPosition.x, 0f, lod0.localPosition.z);
                    }
                    else
                    {
                        doorLeaf.localPosition = new Vector3(-1.29f, 0f, -0.02f);
                    }
                    doorLeaf.localRotation = Quaternion.identity;
                    doorLeaf.localScale = Vector3.one;

                    // Reparent moving parts
                    string[] movingParts = { "door_2_LOD0", "door_2_LOD1", "door_2_LOD2", "window", "handle_2", "Door latch_2", "Latch bolt_2" };
                    foreach (var partName in movingParts)
                    {
                        var part = targetDoor.transform.Find(partName);
                        if (part != null)
                        {
                            part.SetParent(doorLeaf, true);
                        }
                    }
                }
            }

            // 5. Clean colliders on children: remove any MeshColliders so they don't block the player
            foreach (var col in targetDoor.GetComponentsInChildren<Collider>(true))
            {
                if (col.transform != doorLeaf)
                {
                    UnityEngine.Object.DestroyImmediate(col);
                }
            }

            // 6. Attach clean BoxCollider to DoorLeaf
            var leafBox = doorLeaf.GetComponent<BoxCollider>();
            if (leafBox == null) leafBox = doorLeaf.gameObject.AddComponent<BoxCollider>();

            // Calculate bounding box of renderers under doorLeaf
            var renderers = doorLeaf.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                // Convert world bounds to local bounds relative to doorLeaf
                Vector3 localCenter = doorLeaf.InverseTransformPoint(b.center);
                Vector3 localSize = doorLeaf.InverseTransformVector(b.size);
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Max(0.08f, Mathf.Abs(localSize.z)));

                leafBox.center = localCenter;
                leafBox.size = localSize;
                Debug.Log($"[MechatronicsDoorConfigurator] Computed leafBox bounds: center={leafBox.center}, size={leafBox.size}");
            }
            else
            {
                leafBox.center = new Vector3(0.425f, 1.05f, 0.02f);
                leafBox.size = new Vector3(0.85f, 2.1f, 0.08f);
            }
            leafBox.isTrigger = false;

            // 7. Setup WoodDoorController
            var controller = targetDoor.GetComponent<WoodDoorController>();
            if (controller == null) controller = targetDoor.AddComponent<WoodDoorController>();

            var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Imported Assets/Free Wood Door Pack/Audio/Door_Open.wav");
            var closeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Imported Assets/Free Wood Door Pack/Audio/Door_Close.wav");

            var so = new SerializedObject(controller);
            so.FindProperty("_doorLeaf").objectReferenceValue = doorLeaf;
            so.FindProperty("_openAngle").floatValue = -90.0f; // Inward swing into Mechatronics lab
            so.FindProperty("_animationDuration").floatValue = 0.85f;
            so.FindProperty("_isOpen").boolValue = false;
            so.FindProperty("_isLocked").boolValue = true;
            so.FindProperty("_isPermanentlyLocked").boolValue = false;
            so.FindProperty("_requiredKey").enumValueIndex = (int)ItemType.MechatronicsKey;
            so.FindProperty("_lockedMessage").stringValue = "Locked. Requires Mechatronics Lab Key (Search Restroom).";
            so.FindProperty("_canBolt").boolValue = false;
            so.FindProperty("_openSfx").objectReferenceValue = openClip;
            so.FindProperty("_closeSfx").objectReferenceValue = closeClip;
            so.ApplyModifiedProperties();

            // Set tags to Interactable
            targetDoor.tag = "Interactable";
            doorLeaf.gameObject.tag = "Interactable";

            controller.UpdateColliders();

            // 8. Ensure Boss Monster is active in scene
            var boss = UnityEngine.Object.FindFirstObjectByType<AI.BossMonsterController>(FindObjectsInactive.Include);
            if (boss != null)
            {
                if (!boss.gameObject.activeSelf)
                {
                    boss.gameObject.SetActive(true);
                    Debug.Log("[MechatronicsDoorConfigurator] Set Boss_MonsterMutant7 active in scene.");
                }
            }

            // 9. Mark dirty and save
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=green>[MechatronicsDoorConfigurator] SUCCESS: Mechatronics Door configured with animation, locking/unlocking, and seamless clear passage!</color>");
        }
    }
}
