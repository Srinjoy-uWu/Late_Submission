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
    public static class SecurityRoomBuilder
    {
        [MenuItem("Tools/Late Submission/Build Security Room Scene")]
        public static void BuildSecurityRoomScene()
        {
            Debug.Log("<color=cyan>[SecurityRoomBuilder] Building Floor01_SecurityRoom.unity...</color>");

            // Create a new scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: Ambient & Directional
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f);

            var lightGo = new GameObject("Security_Room_Fluorescent");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.color = new Color(0.9f, 0.95f, 1.0f);
            light.intensity = 1.2f;
            lightGo.transform.position = new Vector3(0f, 2.5f, 0f);

            // Shell: Floor, Ceiling, Walls
            // Room dimensions: 6m x 6m x 3m high
            var roomShell = new GameObject("Room_Shell");

            // Materials
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/wall/Materials/wall_piece_mat01.mat");
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/floor/Materials/floor_01_mat.mat");
            Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/floor/Materials/ceiling_01_mat.mat");

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(roomShell.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f); // 6m x 6m
            if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Ceiling
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(roomShell.transform);
            ceiling.transform.position = new Vector3(0f, 3.0f, 0f);
            ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            if (ceilingMat != null) ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

            // Walls (North, South, East, West)
            CreateWall("Wall_North", new Vector3(0f, 1.5f, 3f), new Vector3(6f, 3f, 0.2f), roomShell.transform, wallMat);
            CreateWall("Wall_East", new Vector3(3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), roomShell.transform, wallMat);
            CreateWall("Wall_West", new Vector3(-3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), roomShell.transform, wallMat);
            CreateWall("Wall_South_Left", new Vector3(-1.8f, 1.5f, -3f), new Vector3(2.4f, 3f, 0.2f), roomShell.transform, wallMat);
            CreateWall("Wall_South_Right", new Vector3(1.8f, 1.5f, -3f), new Vector3(2.4f, 3f, 0.2f), roomShell.transform, wallMat);

            // Door to Corridor (placed on South Wall at 0, 0, -3)
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_3.prefab");
            GameObject doorGo;
            if (doorPrefab != null)
            {
                doorGo = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                doorGo.name = "Return_Door";
                doorGo.transform.position = new Vector3(0f, 0f, -2.95f);
                doorGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                doorGo.name = "Return_Door";
                doorGo.transform.position = new Vector3(0f, 1.1f, -2.95f);
                doorGo.transform.localScale = new Vector3(1.2f, 2.2f, 0.1f);
            }

            var doorCol = doorGo.GetComponent<Collider>();
            if (doorCol == null)
            {
                var box = doorGo.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 1.1f, 0f);
                box.size = new Vector3(1.2f, 2.2f, 0.2f);
            }

            var returnTransition = doorGo.AddComponent<SceneTransitionDoor>();
            var soDoor = new SerializedObject(returnTransition);
            soDoor.FindProperty("_targetSceneName").stringValue = "Floor01_Main";
            soDoor.FindProperty("_destinationSpawnPoint").stringValue = "SecurityDoor_Corridor_Spawn";
            soDoor.FindProperty("_fadeDuration").floatValue = 1.2f;
            soDoor.FindProperty("_isLocked").boolValue = false;
            soDoor.FindProperty("_unlockedPrompt").stringValue = "Return to Corridor [E]";
            soDoor.ApplyModifiedProperties();

            // Spawn Point inside Security Room
            var spawnPoint = new GameObject("SecurityRoom_Spawn");
            spawnPoint.transform.position = new Vector3(0f, 0.1f, -2.0f);
            spawnPoint.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Facing into the room

            // Security Desk / Table in the center-back
            var tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/props/table.prefab");
            GameObject desk;
            if (tablePrefab != null)
            {
                desk = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
                desk.name = "Security_Desk";
                desk.transform.position = new Vector3(0f, 0f, 1.2f);
                desk.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = "Security_Desk";
                desk.transform.position = new Vector3(0f, 0.45f, 1.2f);
                desk.transform.localScale = new Vector3(2f, 0.9f, 1f);
            }

            // Key on desk: Pickup_FacultyKey.prefab
            var keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Pickup_FacultyKey.prefab");
            if (keyPrefab != null)
            {
                var keyGo = (GameObject)PrefabUtility.InstantiatePrefab(keyPrefab);
                keyGo.name = "Pickup_FacultyKey";
                keyGo.transform.position = new Vector3(-0.35f, 0.92f, 1.2f);
                keyGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                Debug.Log("[SecurityRoomBuilder] Instantiated Pickup_FacultyKey on security desk.");
            }

            // Security Memo Note on desk: Note_StoryLog1_SecurityMemo.prefab
            var memoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Note_StoryLog1_SecurityMemo.prefab");
            if (memoPrefab != null)
            {
                var memoGo = (GameObject)PrefabUtility.InstantiatePrefab(memoPrefab);
                memoGo.name = "Note_StoryLog1_SecurityMemo";
                memoGo.transform.position = new Vector3(0.35f, 0.92f, 1.2f);
                memoGo.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
                Debug.Log("[SecurityRoomBuilder] Instantiated Note_StoryLog1_SecurityMemo on security desk.");
            }

            // Books / Props on Desk
            var bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Books/LowPolyInteriorPropsPack/Prefabs/P_Books_01.prefab");
            if (bookPrefab != null)
            {
                var books = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab);
                books.name = "Desk_Logbooks";
                books.transform.position = new Vector3(0.6f, 0.92f, 1.1f);
                books.transform.localScale = Vector3.one * 0.7f;
            }

            // Waste Can near desk
            var canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Waste Bins/Waste Can/Prefabs/Waste Can.prefab");
            if (canPrefab != null)
            {
                var can = (GameObject)PrefabUtility.InstantiatePrefab(canPrefab);
                can.name = "Office_WasteCan";
                can.transform.position = new Vector3(-1.2f, 0f, 1.2f);
            }

            // Desk spotlight / emergency red/amber accent
            var accentLight = new GameObject("Desk_Lamp_Accent");
            var aLight = accentLight.AddComponent<Light>();
            aLight.type = LightType.Spot;
            aLight.range = 3f;
            aLight.spotAngle = 60f;
            aLight.color = new Color(1f, 0.85f, 0.6f);
            aLight.intensity = 2f;
            accentLight.transform.position = new Vector3(0f, 2.0f, 1.2f);
            accentLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Core Managers
            var gm = new GameObject("[GameManager]");
            gm.AddComponent<InventoryManager>();
            gm.AddComponent<ObjectiveManager>();

            // SceneFader Canvas
            CreateSceneFader();

            // Player setup
            CreatePlayerInRoom(new Vector3(0f, 0.1f, -2.0f), Quaternion.Euler(0f, 0f, 0f));

            // Save scene to Assets/Scenes/Floor01_SecurityRoom.unity
            string scenePath = "Assets/Scenes/Floor01_SecurityRoom.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"<color=green>[SecurityRoomBuilder] Saved '{scenePath}' successfully!</color>");

            // Also create Floor02_Labs if missing
            BuildFloor02LabsScene();
        }

        private static void BuildFloor02LabsScene()
        {
            string scenePath = "Assets/Scenes/Floor02_Labs.unity";
            if (File.Exists(scenePath))
            {
                Debug.Log("[SecurityRoomBuilder] Floor02_Labs.unity already exists.");
                return;
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Basic floor and light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.2f, 0.3f, 0.5f);
            light.intensity = 0.5f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor02_Landing";
            floor.transform.localScale = Vector3.one * 2f;

            var spawn = new GameObject("Floor02_Start_Spawn");
            spawn.transform.position = new Vector3(0f, 0.1f, 0f);

            var gm = new GameObject("[GameManager]");
            gm.AddComponent<InventoryManager>();
            gm.AddComponent<ObjectiveManager>();

            CreateSceneFader();
            CreatePlayerInRoom(new Vector3(0f, 0.1f, 0f), Quaternion.identity);

            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"<color=green>[SecurityRoomBuilder] Saved '{scenePath}' placeholder successfully!</color>");
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Transform parent, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (mat != null) wall.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateSceneFader()
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
        }

        private static void CreatePlayerInRoom(Vector3 pos, Quaternion rot)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = pos;
            player.transform.rotation = rot;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var fpsComp = player.AddComponent<LateSubmission.Player.FPSController>();
            player.AddComponent<LateSubmission.Player.PlayerNoiseEmitter>();

            var camChild = new GameObject("PlayerCamera");
            camChild.tag = "MainCamera";
            camChild.transform.SetParent(player.transform, false);
            camChild.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var cam = camChild.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camChild.AddComponent<AudioListener>();
            camChild.AddComponent<Interactor>();

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

            var soFps = new SerializedObject(fpsComp);
            soFps.FindProperty("_playerCamera").objectReferenceValue = cam;
            soFps.ApplyModifiedProperties();
        }
    }
}
