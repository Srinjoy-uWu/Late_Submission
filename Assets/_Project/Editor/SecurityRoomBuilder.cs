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
            Debug.Log("<color=cyan>[SecurityRoomBuilder] Building High-Fidelity Floor01_SecurityRoom.unity...</color>");

            // Create a new scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: Atmospheric Low Security Ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.08f);

            // Main Room Fluorescent (subtle flicker green/cyan cast)
            var lightGo = new GameObject("Ceiling_Fluorescent_Tube");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.color = new Color(0.85f, 0.95f, 1.0f);
            light.intensity = 1.0f;
            lightGo.transform.position = new Vector3(0f, 2.7f, 0f);

            // Room Shell (6m wide x 6m deep x 3m high)
            var roomShell = new GameObject("Room_Shell");

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/wall/Materials/wall_piece_mat01.mat");
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/floor/Materials/floor_01_mat.mat");
            Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported Assets/Morgue Room PBR/Models/floor/Materials/ceiling_01_mat.mat");

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(roomShell.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Ceiling
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(roomShell.transform);
            ceiling.transform.position = new Vector3(0f, 3.0f, 0f);
            ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            if (ceilingMat != null) ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

            // Walls (North, East, West, South with doorway)
            CreateWall("Wall_North", new Vector3(0f, 1.5f, 3f), new Vector3(6f, 3f, 0.2f), roomShell.transform, wallMat);
            CreateWall("Wall_East", new Vector3(3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), roomShell.transform, wallMat);
            CreateWall("Wall_West", new Vector3(-3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), roomShell.transform, wallMat);
            CreateWall("Wall_South_Left", new Vector3(-1.8f, 1.5f, -3f), new Vector3(2.4f, 3f, 0.2f), roomShell.transform, wallMat);
            CreateWall("Wall_South_Right", new Vector3(1.8f, 1.5f, -3f), new Vector3(2.4f, 3f, 0.2f), roomShell.transform, wallMat);

            // Doorway Frame & Return Door back to corridor
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

            // Wall Clock above the door
            var clockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/psx-style-vintage-wall-clocks/source/Wall Clock.prefab");
            if (clockPrefab != null)
            {
                var clock = (GameObject)PrefabUtility.InstantiatePrefab(clockPrefab);
                clock.name = "Security_Wall_Clock";
                clock.transform.position = new Vector3(0f, 2.4f, -2.85f);
                clock.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                clock.transform.localScale = Vector3.one * 0.75f;
            }

            // Spawn Point inside Security Room (player facing into room)
            var spawnPoint = new GameObject("SecurityRoom_Spawn");
            spawnPoint.transform.position = new Vector3(0f, 0.1f, -2.0f);
            spawnPoint.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // -------------------------------------------------------------
            // ZONE 2 HERO PROPS: SECURITY CONSOLE & WORKSTATION
            // -------------------------------------------------------------
            var consoleGroup = new GameObject("Security_Console_Station");

            // Office Materials
            Material plasticMat = GetOrCreateMat("M_Office_Plastic", new Color(0.18f, 0.18f, 0.20f), 0.2f);
            Material metalMat = GetOrCreateMat("M_Office_Metal", new Color(0.35f, 0.35f, 0.38f), 0.4f);
            Material deskMat = GetOrCreateMat("M_Office_Desk", new Color(0.25f, 0.25f, 0.28f), 0.25f);
            Material screenEmissiveMat = GetOrCreateEmissiveMat("M_Office_Screen_Emissive", new Color(0.05f, 0.1f, 0.12f), new Color(0.15f, 0.6f, 0.5f) * 1.5f);
            Material paperMat = GetOrCreatePaperMat("M_Office_Paper");

            // 1. Security Desk (SM_Desk)
            var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/OfficeProps/SM_Desk.fbx");
            GameObject deskGo;
            if (deskPrefab != null)
            {
                deskGo = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab);
                deskGo.name = "Security_Desk";
                deskGo.transform.SetParent(consoleGroup.transform);
                deskGo.transform.position = new Vector3(0f, 0f, 1.4f);
                deskGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                ApplyMaterial(deskGo, deskMat);
                EnsureBoxCollider(deskGo);
            }
            else
            {
                deskGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deskGo.name = "Security_Desk";
                deskGo.transform.SetParent(consoleGroup.transform);
                deskGo.transform.position = new Vector3(0f, 0.45f, 1.4f);
                deskGo.transform.localScale = new Vector3(2.2f, 0.9f, 1.1f);
                ApplyMaterial(deskGo, deskMat);
            }

            // 2. Under-desk Drawer (SM_Drawers)
            SpawnProp("SM_Drawers", new Vector3(0.65f, 0f, 1.4f), Quaternion.Euler(0f, 180f, 0f), consoleGroup.transform, metalMat);

            // 3. CCTV Surveillance Monitor 1 (Center)
            var monitorCenter = SpawnProp("SM_Screen", new Vector3(0f, 0.82f, 1.55f), Quaternion.Euler(0f, 180f, 0f), consoleGroup.transform, screenEmissiveMat);

            // 4. CCTV Surveillance Monitor 2 (Angled Left)
            var monitorLeft = SpawnProp("SM_Screen", new Vector3(-0.65f, 0.82f, 1.50f), Quaternion.Euler(0f, 160f, 0f), consoleGroup.transform, screenEmissiveMat);

            // 5. Keyboard & Mouse
            SpawnProp("SM_Keyboard", new Vector3(0f, 0.82f, 1.25f), Quaternion.Euler(0f, 180f, 0f), consoleGroup.transform, plasticMat);
            SpawnProp("SM_Mouse", new Vector3(0.45f, 0.82f, 1.25f), Quaternion.Euler(0f, 180f, 0f), consoleGroup.transform, plasticMat);

            // 6. Security Chair (SM_FoldingChair)
            SpawnProp("SM_FoldingChair", new Vector3(0f, 0f, 0.6f), Quaternion.Euler(0f, 15f, 0f), consoleGroup.transform, plasticMat);

            // 7. Security Coffee Mug & Pen
            SpawnProp("SM_Teacup", new Vector3(-0.4f, 0.82f, 1.22f), Quaternion.identity, consoleGroup.transform, GetOrCreateMat("M_Office_Cup", new Color(0.85f, 0.85f, 0.85f), 0.7f));
            SpawnProp("SM_Pen", new Vector3(0.35f, 0.82f, 1.15f), Quaternion.Euler(0f, 35f, 0f), consoleGroup.transform, plasticMat);

            // 8. Sticky Notes on Monitor
            SpawnProp("SM_StickyNotes", new Vector3(0.28f, 0.82f, 1.45f), Quaternion.Euler(0f, 20f, 0f), consoleGroup.transform, GetOrCreateMat("M_Office_Sticky", new Color(0.95f, 0.9f, 0.3f), 0.1f));

            // 9. Printed Test/Incident Pages on desk
            SpawnProp("SM_TestPage", new Vector3(-0.25f, 0.825f, 1.15f), Quaternion.Euler(0f, -15f, 0f), consoleGroup.transform, paperMat);

            // 10. Filing Cabinet (SM_FilingCabinet) against West wall
            SpawnProp("SM_FilingCabinet", new Vector3(-2.65f, 0f, 1.5f), Quaternion.Euler(0f, 90f, 0f), roomShell.transform, metalMat);

            // 11. Security Room Waste Bin (SM_TrashCan)
            SpawnProp("SM_TrashCan", new Vector3(1.1f, 0f, 1.2f), Quaternion.identity, roomShell.transform, plasticMat);

            // 12. Pinned Cork Notice Board (Cork Board 07) on North Wall behind the desk
            var corkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/psx-corkevidence-board-2/source/Cork Board 07.fbx");
            if (corkPrefab != null)
            {
                var cork = (GameObject)PrefabUtility.InstantiatePrefab(corkPrefab);
                cork.name = "Security_Notice_CorkBoard";
                cork.transform.position = new Vector3(0f, 1.8f, 2.85f);
                cork.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                cork.transform.localScale = Vector3.one * 1.2f;
            }

            // 13. Key Storage Wall Cabinet (from Morgue Room PBR)
            var wallCabinetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/props/cabinet.prefab");
            if (wallCabinetPrefab != null)
            {
                var cab = (GameObject)PrefabUtility.InstantiatePrefab(wallCabinetPrefab);
                cab.name = "Master_Key_Cabinet";
                cab.transform.position = new Vector3(2.65f, 0.8f, 1.5f);
                cab.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                cab.transform.localScale = Vector3.one * 0.8f;
            }

            // -------------------------------------------------------------
            // QUEST CRITICAL OBJECTS ON THE SECURITY DESK
            // -------------------------------------------------------------

            // Faculty Key Pickup: Pickup_FacultyKey.prefab
            var keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Pickup_FacultyKey.prefab");
            if (keyPrefab != null)
            {
                var keyGo = (GameObject)PrefabUtility.InstantiatePrefab(keyPrefab);
                keyGo.name = "Pickup_FacultyKey";
                keyGo.transform.position = new Vector3(0.2f, 0.835f, 1.35f);
                keyGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                Debug.Log("[SecurityRoomBuilder] Instantiated Pickup_FacultyKey on security desk.");
            }

            // Security Memo Note: Note_StoryLog1_SecurityMemo.prefab
            var memoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Note_StoryLog1_SecurityMemo.prefab");
            if (memoPrefab != null)
            {
                var memoGo = (GameObject)PrefabUtility.InstantiatePrefab(memoPrefab);
                memoGo.name = "Note_StoryLog1_SecurityMemo";
                memoGo.transform.position = new Vector3(-0.2f, 0.835f, 1.35f);
                memoGo.transform.rotation = Quaternion.Euler(0f, -15f, 0f);
                Debug.Log("[SecurityRoomBuilder] Instantiated Note_StoryLog1_SecurityMemo on security desk.");
            }

            // Desk spotlight / emergency warm pool
            var accentLight = new GameObject("Desk_Lamp_Spotlight");
            var aLight = accentLight.AddComponent<Light>();
            aLight.type = LightType.Spot;
            aLight.range = 3.5f;
            aLight.spotAngle = 65f;
            aLight.color = new Color(1.0f, 0.9f, 0.7f);
            aLight.intensity = 2.5f;
            accentLight.transform.position = new Vector3(0f, 2.2f, 1.35f);
            accentLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // CCTV Monitor Screen Glow (faint cyan)
            var monitorLight = new GameObject("CCTV_Screen_Glow");
            var mLight = monitorLight.AddComponent<Light>();
            mLight.type = LightType.Point;
            mLight.range = 2f;
            mLight.color = new Color(0.2f, 0.7f, 0.6f);
            mLight.intensity = 0.8f;
            monitorLight.transform.position = new Vector3(0f, 1.1f, 1.45f);

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
            Debug.Log($"<color=green>[SecurityRoomBuilder] Saved '{scenePath}' with high-fidelity props successfully!</color>");

            // Also create Floor02_Labs if missing
            BuildFloor02LabsScene();
        }

        private static GameObject SpawnProp(string propName, Vector3 pos, Quaternion rot, Transform parent, Material mat)
        {
            string path = $"Assets/Imported Assets/OfficeProps/{propName}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = propName;
                go.transform.SetParent(parent);
                go.transform.position = pos;
                go.transform.rotation = rot;
                if (mat != null) ApplyMaterial(go, mat);
                EnsureBoxCollider(go);
                return go;
            }
            return null;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
            }
        }

        private static void EnsureBoxCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) return;
            var childCol = go.GetComponentInChildren<Collider>();
            if (childCol != null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                Vector3 size = go.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
        }

        private static Material GetOrCreateMat(string matName, Color color, float smoothness)
        {
            string matPath = $"Assets/Imported Assets/OfficeProps/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(s);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static Material GetOrCreateEmissiveMat(string matName, Color baseCol, Color emissiveCol)
        {
            string matPath = $"Assets/Imported Assets/OfficeProps/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(s);
                mat.SetColor("_BaseColor", baseCol);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissiveCol);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static Material GetOrCreatePaperMat(string matName)
        {
            string matPath = $"Assets/Imported Assets/OfficeProps/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(s);
                mat.SetColor("_BaseColor", Color.white);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Imported Assets/OfficeProps/Sp_TestPage.png");
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
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

        private static void BuildFloor02LabsScene()
        {
            string scenePath = "Assets/Scenes/Floor02_Labs.unity";
            if (File.Exists(scenePath))
            {
                Debug.Log("[SecurityRoomBuilder] Floor02_Labs.unity already exists.");
                return;
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
    }
}
