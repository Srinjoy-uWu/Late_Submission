using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using LateSubmission.Interaction;
using LateSubmission.Core;
using LateSubmission.Inventory;
using LateSubmission.Objectives;

namespace LateSubmission.Editor
{
    public static class Floor01SetupPipeline
    {
        [MenuItem("Tools/Late Submission/Setup Floor 1 Scene & Doors")]
        public static void SetupFloor1DoorsAndScene()
        {
            Debug.Log("<color=cyan>[Floor01Setup] Starting Floor 1 Doors and Checkpoints Setup...</color>");

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Floor01_Main")
            {
                Debug.LogWarning($"Active scene is '{activeScene.name}', opening 'Floor01_Main'...");
                EditorSceneManager.OpenScene("Assets/Scenes/Floor01_Main.unity");
            }

            // 1. Setup Entrance Door & Lock Trigger
            SetupEntranceDoor();

            // 2. Setup Side Doors
            SetupSideDoors();

            // 3. Setup Security Door
            SetupSecurityDoor();

            // 4. Setup Exit Door at Corridor End
            SetupExitDoor();

            // 5. Setup Corridor Table with Story Note
            SetupCorridorTable();

            // 6. Setup Spawn Points
            SetupSpawnPoints();

            // 7. Setup Core Managers (SceneFader, InventoryManager, ObjectiveManager)
            SetupCoreManagers();

            // 8. Setup Player
            SetupPlayer();

            // 9. Ensure Build Settings
            SetupBuildSettings();

            // 10. Mark dirty and save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[Floor01Setup] Floor 1 Scene & Doors successfully configured and saved!</color>");
        }

        private static void SetupEntranceDoor()
        {
            var doorGo = FindGameObject("Entrance_Door_Spawn");
            if (doorGo == null)
            {
                Debug.LogError("[Floor01Setup] Could not find 'Entrance_Door_Spawn'!");
                return;
            }

            EnsureBoxCollider(doorGo, isTrigger: false);

            var doorComp = doorGo.GetComponent<Door>();
            if (doorComp == null) doorComp = doorGo.AddComponent<Door>();

            var so = new SerializedObject(doorComp);
            so.FindProperty("_isOpen").boolValue = false;
            so.FindProperty("_isLocked").boolValue = true;
            so.FindProperty("_lockedMessage").stringValue = "Locked. Cannot turn back now.";
            so.ApplyModifiedProperties();

            // Create or update Entrance_Lock_Trigger in front of the door
            string triggerName = "Entrance_Lock_Trigger";
            var triggerGo = GameObject.Find(triggerName);
            if (triggerGo == null)
            {
                triggerGo = new GameObject(triggerName);
                triggerGo.transform.SetParent(doorGo.transform.parent, true);
            }

            // Place trigger right where player steps into corridor (X ~ 6.8, Y ~ 1.0, Z ~ 0.12)
            triggerGo.transform.position = new Vector3(6.8f, 1.0f, 0.12f);
            var col = triggerGo.GetComponent<BoxCollider>();
            if (col == null) col = triggerGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.5f, 3.0f, 3.5f);

            var lockTrigger = triggerGo.GetComponent<EntranceLockTrigger>();
            if (lockTrigger == null) lockTrigger = triggerGo.AddComponent<EntranceLockTrigger>();

            var soTrigger = new SerializedObject(lockTrigger);
            soTrigger.FindProperty("_entranceDoor").objectReferenceValue = doorComp;
            soTrigger.FindProperty("_slamNoise").floatValue = 8.0f;
            soTrigger.ApplyModifiedProperties();

            Debug.Log("[Floor01Setup] Configured 'Entrance_Door_Spawn' and 'Entrance_Lock_Trigger'.");
        }

        private static void SetupSideDoors()
        {
            string[] sideDoorNames = { "door_2", "door_2 (1)" };
            foreach (var name in sideDoorNames)
            {
                var doorGo = FindGameObject(name);
                if (doorGo == null)
                {
                    Debug.LogWarning($"[Floor01Setup] Could not find side door '{name}'");
                    continue;
                }

                EnsureBoxCollider(doorGo, isTrigger: false);

                var doorComp = doorGo.GetComponent<Door>();
                if (doorComp == null) doorComp = doorGo.AddComponent<Door>();

                var so = new SerializedObject(doorComp);
                so.FindProperty("_isOpen").boolValue = false;
                so.FindProperty("_isLocked").boolValue = true;
                so.FindProperty("_lockedMessage").stringValue = "Locked.";
                so.FindProperty("_requiredKey").intValue = 999; // unattainable dummy key so it remains locked
                so.ApplyModifiedProperties();

                Debug.Log($"[Floor01Setup] Configured Side Door '{name}' as Locked.");
            }
        }

        private static void SetupSecurityDoor()
        {
            var doorGo = FindGameObject("Security_Door");
            if (doorGo == null)
            {
                Debug.LogError("[Floor01Setup] Could not find 'Security_Door'!");
                return;
            }

            EnsureBoxCollider(doorGo, isTrigger: false);

            var transComp = doorGo.GetComponent<SceneTransitionDoor>();
            if (transComp == null) transComp = doorGo.AddComponent<SceneTransitionDoor>();

            var so = new SerializedObject(transComp);
            so.FindProperty("_targetSceneName").stringValue = "Floor01_SecurityRoom";
            so.FindProperty("_destinationSpawnPoint").stringValue = "SecurityRoom_Spawn";
            so.FindProperty("_fadeDuration").floatValue = 1.2f;
            so.FindProperty("_isLocked").boolValue = false;
            so.FindProperty("_unlockedPrompt").stringValue = "Enter Security Room [E]";
            so.ApplyModifiedProperties();

            Debug.Log("[Floor01Setup] Configured 'Security_Door' -> Transitions to Floor01_SecurityRoom.");
        }

        private static void SetupExitDoor()
        {
            var doorGo = FindGameObject("Exit_Door_Spawn");
            if (doorGo == null)
            {
                Debug.LogError("[Floor01Setup] Could not find 'Exit_Door_Spawn'!");
                return;
            }

            EnsureBoxCollider(doorGo, isTrigger: false);

            var transComp = doorGo.GetComponent<SceneTransitionDoor>();
            if (transComp == null) transComp = doorGo.AddComponent<SceneTransitionDoor>();

            var so = new SerializedObject(transComp);
            so.FindProperty("_targetSceneName").stringValue = "Floor02_Labs";
            so.FindProperty("_destinationSpawnPoint").stringValue = "Floor02_Start_Spawn";
            so.FindProperty("_fadeDuration").floatValue = 1.5f;
            so.FindProperty("_isLocked").boolValue = true;
            so.FindProperty("_requiredKey").intValue = (int)ItemType.FacultyKey;
            so.FindProperty("_lockedMessage").stringValue = "Locked. Needs Key from Security Room.";
            so.FindProperty("_unlockedPrompt").stringValue = "Unlock & Proceed to Next Floor [E]";
            so.ApplyModifiedProperties();

            Debug.Log("[Floor01Setup] Configured 'Exit_Door_Spawn' -> Locked, requires FacultyKey, transitions to Floor02_Labs.");
        }

        private static void SetupCorridorTable()
        {
            var table = FindGameObject("table");
            if (table == null) return;

            // Check if note is already on table
            string noteName = "Corridor_Table_Notice";
            var existingNote = GameObject.Find(noteName);
            if (existingNote == null)
            {
                var notePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Note_StoryLog2_FacultyMemo.prefab");
                GameObject noteGo;
                if (notePrefab != null)
                {
                    noteGo = (GameObject)PrefabUtility.InstantiatePrefab(notePrefab);
                    noteGo.name = noteName;
                }
                else
                {
                    noteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    noteGo.name = noteName;
                    noteGo.transform.localScale = new Vector3(0.25f, 0.02f, 0.35f);
                    var inspect = noteGo.AddComponent<InspectNote>();
                    var soInspect = new SerializedObject(inspect);
                    soInspect.FindProperty("_noteTitle").stringValue = "Corridor Notice";
                    soInspect.FindProperty("_noteBody").stringValue = "ATTENTION ALL STUDENTS:\nAcademic Block C is closed after 9:00 PM.\nSecurity personnel hold the Master Key in the Security Room down the hall.";
                    soInspect.ApplyModifiedProperties();
                }

                noteGo.transform.position = table.transform.position + new Vector3(0f, 0.85f, 0f);
                var noteCollider = noteGo.GetComponent<Collider>();
                if (noteCollider != null) noteCollider.isTrigger = false;

                Debug.Log($"[Floor01Setup] Placed Notice Note on corridor table at {noteGo.transform.position}.");
            }
        }

        private static void SetupSpawnPoints()
        {
            // Initial player spawn at entrance
            var entranceSpawn = GameObject.Find("Entrance_Player_Spawn");
            if (entranceSpawn == null)
            {
                entranceSpawn = new GameObject("Entrance_Player_Spawn");
                entranceSpawn.transform.position = new Vector3(6.5f, 0.1f, 0.12f);
                entranceSpawn.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            // Return spawn outside security room
            var securityDoorCorridorSpawn = GameObject.Find("SecurityDoor_Corridor_Spawn");
            if (securityDoorCorridorSpawn == null)
            {
                securityDoorCorridorSpawn = new GameObject("SecurityDoor_Corridor_Spawn");
                securityDoorCorridorSpawn.transform.position = new Vector3(-16.3f, 0.1f, 0.5f);
                securityDoorCorridorSpawn.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        private static void SetupCoreManagers()
        {
            // GameManager
            var gm = GameObject.Find("[GameManager]");
            if (gm == null)
            {
                gm = new GameObject("[GameManager]");
            }
            if (gm.GetComponent<InventoryManager>() == null) gm.AddComponent<InventoryManager>();
            if (gm.GetComponent<ObjectiveManager>() == null) gm.AddComponent<ObjectiveManager>();

            // SceneFader
            var fader = Object.FindFirstObjectByType<SceneFader>();
            if (fader == null)
            {
                var canvasGo = new GameObject("SceneFader_Canvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

                var cg = canvasGo.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;

                var faderComp = canvasGo.AddComponent<SceneFader>();

                // Black image
                var imgGo = new GameObject("FadeBlackImage");
                imgGo.transform.SetParent(canvasGo.transform, false);
                var img = imgGo.AddComponent<UnityEngine.UI.Image>();
                img.color = Color.black;
                img.raycastTarget = false;
                var rt = imgGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                var soFader = new SerializedObject(faderComp);
                soFader.FindProperty("_canvasGroup").objectReferenceValue = cg;
                soFader.FindProperty("_defaultFadeDuration").floatValue = 1.0f;
                soFader.FindProperty("_fadeInOnStart").boolValue = true;
                soFader.ApplyModifiedProperties();

                Debug.Log("[Floor01Setup] Created SceneFader canvas.");
            }
        }

        private static void SetupPlayer()
        {
            var fps = Object.FindFirstObjectByType<LateSubmission.Player.FPSController>();
            if (fps == null)
            {
                // Remove standalone Main Camera if present
                var camGo = GameObject.Find("Main Camera");
                if (camGo != null && camGo.transform.parent == null)
                {
                    Object.DestroyImmediate(camGo);
                }

                // Create Player
                var player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = new Vector3(6.5f, 0.1f, 0.12f);
                player.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);

                var fpsComp = player.AddComponent<LateSubmission.Player.FPSController>();
                player.AddComponent<LateSubmission.Player.PlayerNoiseEmitter>();

                // Child Camera
                var camChild = new GameObject("PlayerCamera");
                camChild.tag = "MainCamera";
                camChild.transform.SetParent(player.transform, false);
                camChild.transform.localPosition = new Vector3(0f, 1.6f, 0f);

                var cam = camChild.AddComponent<Camera>();
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                camChild.AddComponent<AudioListener>();
                var interactor = camChild.AddComponent<Interactor>();

                // Flashlight
                var lightGo = new GameObject("Flashlight");
                lightGo.transform.SetParent(camChild.transform, false);
                var spot = lightGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.range = 18f;
                spot.spotAngle = 55f;
                spot.innerSpotAngle = 30f;
                spot.color = new Color(1f, 0.95f, 0.85f);
                spot.intensity = 1.8f;
                lightGo.AddComponent<LateSubmission.Player.FlashlightController>();

                // Hook up serialized fields
                var soFps = new SerializedObject(fpsComp);
                soFps.FindProperty("_playerCamera").objectReferenceValue = cam;
                soFps.ApplyModifiedProperties();

                Debug.Log("[Floor01Setup] Created Player at Entrance_Player_Spawn.");
            }
        }

        private static void SetupBuildSettings()
        {
            string[] scenes = {
                "Assets/Scenes/Floor01_Main.unity",
                "Assets/Scenes/Floor01_SecurityRoom.unity",
                "Assets/Scenes/Floor02_Labs.unity"
            };

            var buildScenes = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                buildScenes[i] = new EditorBuildSettingsScene(scenes[i], true);
            }
            EditorBuildSettings.scenes = buildScenes;
            Debug.Log("[Floor01Setup] Configured EditorBuildSettings with 3 scenes.");
        }

        private static void EnsureBoxCollider(GameObject go, bool isTrigger)
        {
            var existingCol = go.GetComponent<Collider>();
            if (existingCol != null) return;

            var childCols = go.GetComponentsInChildren<Collider>();
            if (childCols.Length > 0) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }

                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = isTrigger;
                col.center = go.transform.InverseTransformPoint(b.center);
                Vector3 size = go.transform.InverseTransformVector(b.size);
                col.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            else
            {
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = isTrigger;
                col.center = new Vector3(0f, 1.2f, 0f);
                col.size = new Vector3(1.2f, 2.4f, 0.3f);
            }
        }

        private static GameObject FindGameObject(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) return go;

            var props = GameObject.Find("Props_Spawn");
            if (props != null)
            {
                var t = props.transform.Find(name);
                if (t != null) return t.gameObject;
            }

            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var g in all)
            {
                if (g.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return g;
                }
            }
            return null;
        }
    }
}
