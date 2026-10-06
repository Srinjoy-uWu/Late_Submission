// =========================================================================
//  FullBuildAndIntegrator.cs  —  Late Submission
//  Master Integration, Scene Hierarchy Organizer, Cleanup, and Build Pipeline
//
//  MENU:
//    Tools > Late Submission > Integrate Everything (Clean & Organize Scenes)
//    Tools > Late Submission > Build > Build Standalone Windows Game
//
//  GAME FLOW:
//    Floor01_Main (Entrance_Player_Spawn)
//      → SecurityRoom (SecurityRoom_Spawn -> collect FacultyKey & notes)
//      → Floor01_Main (SecurityDoor_Corridor_Spawn — right outside Security Room)
//      → Cabin104 (Cabin104_Spawn -> collect Flashlight, Floor02Key, CoverSheet & notes)
//      → Floor01_Main (C104_Corridor_Spawn — right outside Prof's room)
//      → Floor02_Main (Mechatronics Lab -> Bathroom Safe Room -> Boss Defeated -> Paywall)
// =========================================================================

using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using LateSubmission.Core;
using LateSubmission.Interaction;
using LateSubmission.UI;
using LateSubmission.AI;
using LateSubmission.Inventory;
using LateSubmission.Player;
using LateSubmission.Weapon;
using LateSubmission.Audio;

namespace LateSubmission.Editor
{
    [InitializeOnLoad]
    public static class FullBuildAndIntegrator
    {
        private const string AUTO_INTEGRATE_KEY = "LateSubmission_AutoIntegrated_v7_MonsterNavMeshEnabled";

        static FullBuildAndIntegrator()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                if (!SessionState.GetBool(AUTO_INTEGRATE_KEY, false))
                {
                    SessionState.SetBool(AUTO_INTEGRATE_KEY, true);
                    IntegrateFloor02Only();
                }
            };
        }

        // ─── Scene paths ────────────────────────────────────────────────────
        private const string SCENE_FLOOR01      = "Assets/Scenes/Floor01_Main.unity";
        private const string SCENE_SECURITY     = "Assets/Scenes/Floor01_SecurityRoom.unity";
        private const string SCENE_CABIN104     = "Assets/Scenes/Floor01_Cabin104.unity";
        private const string SCENE_FLOOR02      = "Assets/Scenes/Floor02_Main.unity";

        // ─── Target scene names used at runtime by SceneTransitionDoor ──────
        private const string NAME_FLOOR01       = "Floor01_Main";
        private const string NAME_SECURITY      = "Floor01_SecurityRoom";
        private const string NAME_CABIN104      = "Floor01_Cabin104";
        private const string NAME_FLOOR02       = "Floor02_Main";

        // ─── Spawn point names ──────────────────────────────────────────────
        private const string SPAWN_FLOOR01_ENTRANCE  = "Entrance_Player_Spawn";
        private const string SPAWN_FLOOR01_SECURITY  = "SecurityDoor_Corridor_Spawn";
        private const string SPAWN_FLOOR01_CABIN104  = "C104_Corridor_Spawn";
        private const string SPAWN_SECURITY_ENTRY    = "SecurityRoom_Spawn";
        private const string SPAWN_CABIN104_ENTRY    = "Cabin104_Spawn";
        private const string SPAWN_FLOOR02_START     = "Floor02_Start_Spawn";

        // ─── Asset paths ─────────────────────────────────────────────────────
        private const string QR_TEXTURE_PATH    = "Assets/_Project/Textures/Paywall_QRCode.jpg";
        private const string DOOR_OPEN_SFX      = "Assets/_Project/Audio/Doors/Door_Open.wav";
        private const string DOOR_CLOSE_SFX     = "Assets/_Project/Audio/Doors/Door_Close.wav";
        private const string AMBIENT_CLIP_PATH  = "Assets/_Project/Audio/Ambient/Alone at Twilight 1.wav";
        private const string CHASE_CLIP_PATH    = "Assets/Imported Assets/Horror Starter Pack/sp-horroraction.aif";
        private const string STINGER_CLIP_PATH  = "Assets/Imported Assets/Horror Starter Pack/sp-lost.aif";
        private const string FOOTSTEP_STONE     = "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/Stone.wav";
        private const string FOOTSTEP_METAL     = "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/Metal.wav";
        private const string FOOTSTEP_GRASS     = "Assets/Imported Assets/ElmanGameDevTools/FirstPersonControllerPro/Player/Song/Grass.wav";

        // =====================================================================
        //  ENTRY POINTS
        // =====================================================================

        [MenuItem("Tools/Late Submission/Integrate Everything (Clean & Organize Scenes)")]
        public static void RunFullIntegration()
        {
            Log("==================== [STARTING MASTER INTEGRATION & CLEANUP] ====================", "cyan");

            SetupQRCodeSprite();
            IntegrateFloor01();
            IntegrateSecurityRoom();
            IntegrateCabin104();
            IntegrateFloor02();

            // Leave Floor01_Main open as default starting scene
            EditorSceneManager.OpenScene(SCENE_FLOOR01, OpenSceneMode.Single);

            Log("==================== [MASTER INTEGRATION & CLEANUP COMPLETED] ====================", "green");
        }

        [MenuItem("Tools/Late Submission/Build/Build Standalone Windows Game")]
        public static void BuildStandaloneWindows()
        {
            RunFullIntegration();

            string buildDir = "Builds/Windows";
            if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "LateSubmission.exe");

            string[] scenes = {
                SCENE_FLOOR01,
                SCENE_SECURITY,
                SCENE_CABIN104,
                SCENE_FLOOR02
            };

            var options = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = exePath,
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.None
            };

            Log($"[BUILD] Starting standalone build → {exePath} ...", "cyan");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Log($"[BUILD SUCCESS] Done in {report.summary.totalTime.TotalSeconds:F1}s  |  Size: {report.summary.totalSize / (1024 * 1024):F1} MB", "green");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Succeeded!",
                        $"LateSubmission.exe built successfully!\nLocation: {Path.GetFullPath(exePath)}", "Open Folder");
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{Path.GetFullPath(exePath)}\"");
                }
            }
            else
            {
                LogError($"[BUILD FAILED] {report.summary.result}  |  {report.summary.totalErrors} error(s)");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Failed",
                        $"Build failed with {report.summary.totalErrors} error(s).\nCheck the Console for details.", "OK");
                }
            }
        }

        // =====================================================================
        //  QR CODE SPRITE SETUP
        // =====================================================================

        private static void SetupQRCodeSprite()
        {
            var importer = AssetImporter.GetAtPath(QR_TEXTURE_PATH) as TextureImporter;
            if (importer == null)
            {
                LogWarning($"QR Code texture not found at: {QR_TEXTURE_PATH}");
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType      = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                Log("QR Code re-imported as Sprite.");
            }
        }

        // =====================================================================
        //  FLOOR 01 — Main Corridor
        // =====================================================================

        private static void IntegrateFloor01()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_FLOOR01, OpenSceneMode.Single);
            Log($"[Floor01] Opened: {scene.name}");

            // 1. Remove all menu canvases
            RemoveMenuCanvases(scene);

            // 2. EventSystem — ensure InputSystemUIInputModule
            EnsureEventSystem();

            // 3. SceneFader — smooth screen fades
            EnsureSceneFader(scene);

            // 4. Consistent Music (GameAudioDirector)
            EnsureConsistentAudioDirector(scene);

            // 5. Configure all SceneTransitionDoors in Floor01
            var transitionDoors = UnityEngine.Object.FindObjectsByType<SceneTransitionDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var door in transitionDoors)
            {
                string target = door.GetTargetScene();
                switch (target)
                {
                    case NAME_SECURITY:
                        door.Configure(
                            NAME_SECURITY,
                            SPAWN_SECURITY_ENTRY,
                            isLocked: false,
                            ItemType.FacultyKey,
                            lockedMsg:      "Security Room door.",
                            unlockedPrompt: "Enter Security Room [E]");
                        Log("[Floor01] SecurityRoom door → unlocked (spawns at SecurityRoom_Spawn).");
                        break;

                    case NAME_CABIN104:
                        door.Configure(
                            NAME_CABIN104,
                            SPAWN_CABIN104_ENTRY,
                            isLocked: true,
                            ItemType.FacultyKey,
                            lockedMsg:      "Locked. Prof. Anish Mondal's Cabin C104. Requires Faculty Key (find in Security Room).",
                            unlockedPrompt: "Enter Cabin C104 — Prof. Anish Mondal [E]");
                        Log("[Floor01] Cabin104 door → locked (FacultyKey required, spawns at Cabin104_Spawn).");
                        break;

                    case NAME_FLOOR02:
                        door.Configure(
                            NAME_FLOOR02,
                            SPAWN_FLOOR02_START,
                            isLocked: true,
                            ItemType.Floor02Key,
                            lockedMsg:      "Locked. Fire Escape to Floor 2. Requires Floor 02 Key (find in Cabin C104).",
                            unlockedPrompt: "Unlock Fire Escape — Proceed to Floor 2 [E]");
                        Log("[Floor01] Floor02 transition door → locked (Floor02Key required).");
                        break;
                }
            }

            // 6. Configure exact corridor spawn points & initial Player placement
            // Main corridor entrance: (6.5, 0.1, 0.12) facing -X (-90 deg yaw) down the corridor
            Vector3 entrancePos = new Vector3(6.5f, 0.1f, 0.12f);
            Quaternion entranceRot = Quaternion.Euler(0f, -90f, 0f);
            SetOrCreateSpawnPoint(SPAWN_FLOOR01_ENTRANCE, entrancePos, entranceRot);

            // Right outside Security_Door (-20.1, 0.01, 1.97) on North wall -> spawn at (-20.1, 0.1, 0.8) facing corridor (180 deg yaw)
            SetOrCreateSpawnPoint(SPAWN_FLOOR01_SECURITY, new Vector3(-20.1f, 0.1f, 0.8f), Quaternion.Euler(0f, 180f, 0f));

            // Right outside C104_Room (-12.92, 0.02, -1.96) on South wall -> spawn at (-12.92, 0.1, -0.8) facing corridor (0 deg yaw)
            SetOrCreateSpawnPoint(SPAWN_FLOOR01_CABIN104, new Vector3(-12.92f, 0.1f, -0.8f), Quaternion.Euler(0f, 0f, 0f));

            // Ensure Player starts at the corridor entrance
            SetPlayerStartPose(entrancePos, entranceRot);

            // 7. Ensure no flashlight pickup exists in Floor01 corridor and hide flashlight hand viewmodel
            RemoveFlashlightPickupsFromScene(scene);
            ConfigurePlayerFlashlightAndHeldItems(scene);

            // 8. Clean hierarchy grouping
            OrganizeSceneHierarchy(scene);

            SaveScene(scene, "[Floor01] saved.");
        }

        // =====================================================================
        //  SECURITY ROOM
        // =====================================================================

        private static void IntegrateSecurityRoom()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_SECURITY, OpenSceneMode.Single);
            Log($"[SecurityRoom] Opened: {scene.name}");

            RemoveMenuCanvases(scene);
            EnsureEventSystem();
            EnsureSceneFader(scene);
            EnsureConsistentAudioDirector(scene);

            // Exit door returns to Floor01 right outside the Security Room door (SecurityDoor_Corridor_Spawn)
            var transitionDoors = UnityEngine.Object.FindObjectsByType<SceneTransitionDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var door in transitionDoors)
            {
                if (door.GetTargetScene() == NAME_FLOOR01)
                {
                    door.Configure(
                        NAME_FLOOR01,
                        SPAWN_FLOOR01_SECURITY,
                        isLocked: false,
                        ItemType.FacultyKey,
                        lockedMsg:      "Exit to Corridor.",
                        unlockedPrompt: "Exit to Floor 1 Corridor [E]");
                    Log($"[SecurityRoom] Exit door → {NAME_FLOOR01} ({SPAWN_FLOOR01_SECURITY}).");
                }
            }

            Vector3 secEntryPos = new Vector3(0f, 0.1f, -2f);
            Quaternion secEntryRot = Quaternion.identity;
            SetOrCreateSpawnPoint(SPAWN_SECURITY_ENTRY, secEntryPos, secEntryRot);
            SetPlayerStartPose(secEntryPos, secEntryRot);

            // Ensure no flashlight pickup exists in Security Room and hide flashlight hand viewmodel
            RemoveFlashlightPickupsFromScene(scene);
            ConfigurePlayerFlashlightAndHeldItems(scene);

            OrganizeSceneHierarchy(scene);
            SaveScene(scene, "[SecurityRoom] saved.");
        }

        // =====================================================================
        //  CABIN 104 — Prof. Anish Mondal's Office
        // =====================================================================

        private static void IntegrateCabin104()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_CABIN104, OpenSceneMode.Single);
            Log($"[Cabin104] Opened: {scene.name}");

            RemoveMenuCanvases(scene);
            EnsureEventSystem();
            EnsureSceneFader(scene);
            EnsureConsistentAudioDirector(scene);

            // Exit door returns to Floor01 right outside Prof's Cabin C104 door (C104_Corridor_Spawn)
            var transitionDoors = UnityEngine.Object.FindObjectsByType<SceneTransitionDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var door in transitionDoors)
            {
                if (door.GetTargetScene() == NAME_FLOOR01)
                {
                    door.Configure(
                        NAME_FLOOR01,
                        SPAWN_FLOOR01_CABIN104,
                        isLocked: false,
                        ItemType.FacultyKey,
                        lockedMsg:      "Exit Cabin C104.",
                        unlockedPrompt: "Exit Cabin C104 [E]");
                    Log($"[Cabin104] Exit door → {NAME_FLOOR01} ({SPAWN_FLOOR01_CABIN104}).");
                }
            }

            Vector3 cabinEntryPos = new Vector3(0f, 0.1f, -1.8f);
            Quaternion cabinEntryRot = Quaternion.identity;
            SetOrCreateSpawnPoint(SPAWN_CABIN104_ENTRY, cabinEntryPos, cabinEntryRot);
            SetPlayerStartPose(cabinEntryPos, cabinEntryRot);

            // Ensure Flashlight pickup IS active in Prof's room and hide flashlight hand viewmodel
            VerifyFlashlightPickupInCabin104();
            ConfigurePlayerFlashlightAndHeldItems(scene);

            OrganizeSceneHierarchy(scene);
            SaveScene(scene, "[Cabin104] saved.");
        }

        // =====================================================================
        //  FLOOR 02 — Horror Floor
        // =====================================================================

        [MenuItem("Tools/Late Submission/Update Floor 02 Only (Flashlight & Corridor Lighting)")]
        public static void IntegrateFloor02Only()
        {
            Log("==================== [UPDATING FLOOR 02 FLASHLIGHT, CORRIDOR LIGHTING & NAVMESH] ====================", "cyan");
            string previousScenePath = SceneManager.GetActiveScene().path;
            SetupQRCodeSprite();
            IntegrateFloor02();
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != SCENE_FLOOR02)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
            Log("==================== [FLOOR 02 UPDATE COMPLETED — FLOOR 01 UNTOUCHED] ====================", "green");
        }

        private static void IntegrateFloor02()
        {
            var activeScene = SceneManager.GetActiveScene();
            var scene = (activeScene.path == SCENE_FLOOR02)
                ? activeScene
                : EditorSceneManager.OpenScene(SCENE_FLOOR02, OpenSceneMode.Single);
            Log($"[Floor02] Operating on scene: {scene.name}");

            RemoveMenuCanvases(scene);
            EnsureEventSystem();
            EnsureSceneFader(scene);
            EnsureConsistentAudioDirector(scene);

            // 1. Mechatronics Lab Door
            ConfigureMechatronicsLabDoor();

            // 2. Bathroom Safe Room Doors
            ConfigureBathroomDoors();

            // 3. EC Lab Door
            ConfigureECLabDoors();

            // 4. Boss Monster (dormant at start)
            ConfigureBossMonster();

            // 5. Boss Spawn Trigger (fires when player enters Mechatronics Lab)
            ConfigureBossSpawnTrigger();

            // 6. Paywall UI
            ConfigurePaywallUI();

            // 7. Floor 02 Corridor Ceiling Lights (2 faulty flickering, 8 completely black, very faint corridor)
            ConfigureFloor02CorridorLighting();

            // 8. NavMesh
            BakeNavMesh();

            // 9. Spawn Point (at corridor start (6.5, 0.1, 0.12) facing -90 deg down corridor)
            SetOrCreateSpawnPoint(SPAWN_FLOOR02_START, new Vector3(6.5f, 0.1f, 0.12f), Quaternion.Euler(0f, -90f, 0f));

            // 10. Configure Player Flashlight (aligned forward & hidden hand viewmodel)
            ConfigurePlayerFlashlightAndHeldItems(scene);

            // 11. Clean hierarchy grouping
            OrganizeSceneHierarchy(scene);

            SaveScene(scene, "[Floor02] saved.");
        }

        private static void ConfigureFloor02CorridorLighting()
        {
            // Create or load dedicated pitch-black material for dead Floor 02 ceiling fixtures
            // (so we never modify level_objects_mat used by Floor 01!)
            string matDir = "Assets/_Project/Materials";
            if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
            string deadMatPath = $"{matDir}/Floor02_DeadCeilingFixture.mat";
            Material deadMat = AssetDatabase.LoadAssetAtPath<Material>(deadMatPath);
            if (deadMat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                deadMat = new Material(urpLit != null ? urpLit : Shader.Find("Standard"));
                deadMat.name = "Floor02_DeadCeilingFixture";
                AssetDatabase.CreateAsset(deadMat, deadMatPath);
            }
            if (deadMat.HasProperty("_BaseColor")) deadMat.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.04f, 1f));
            if (deadMat.HasProperty("_Color")) deadMat.SetColor("_Color", new Color(0.03f, 0.03f, 0.04f, 1f));
            if (deadMat.HasProperty("_EmissionColor")) deadMat.SetColor("_EmissionColor", Color.black);
            deadMat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(deadMat);

            var ceilingSpawn = FindGoByName("Ceiling_Spawn");
            if (ceilingSpawn != null)
            {
                foreach (Transform child in ceilingSpawn.transform)
                {
                    string n = child.name;
                    if (!n.StartsWith("CeilingFixture") && !n.Contains("FlickeringLight"))
                        continue;

                    // Disable any duplicate child Point light
                    foreach (var lt in child.GetComponentsInChildren<Light>(true))
                    {
                        if (lt.transform != child)
                        {
                            lt.intensity = 0f;
                            lt.enabled = false;
                            EditorUtility.SetDirty(lt);
                        }
                    }

                    var mainLight = child.GetComponent<Light>();
                    var flicker = child.GetComponent<LateSubmission.Environment.FlickeringLight>();
                    var renderer = child.GetComponent<Renderer>();

                    // Only 2 fixtures flicker faintly: Corridor_LoneFlickeringLight (x=-12) and CeilingFixture_8 (x=-28)
                    bool isFaultyFlicker = (n == "Corridor_LoneFlickeringLight" || n == "CeilingFixture_8");

                    if (isFaultyFlicker)
                    {
                        if (mainLight != null)
                        {
                            mainLight.enabled = true;
                            mainLight.intensity = 0.16f;
                            mainLight.range = 4.2f;
                            mainLight.color = new Color(0.70f, 0.80f, 0.85f, 1f);
                            EditorUtility.SetDirty(mainLight);
                        }

                        if (flicker == null && mainLight != null)
                        {
                            flicker = child.gameObject.AddComponent<LateSubmission.Environment.FlickeringLight>();
                        }

                        if (flicker != null)
                        {
                            flicker.enabled = true;
                            var so = new SerializedObject(flicker);
                            so.FindProperty("_light").objectReferenceValue = mainLight;
                            so.FindProperty("_emissiveRenderer").objectReferenceValue = renderer;
                            so.FindProperty("_pattern").enumValueIndex = (int)LateSubmission.Environment.FlickerPattern.Erratic;
                            so.FindProperty("_baseIntensity").floatValue = 0.16f;
                            so.FindProperty("_minIntensity").floatValue = 0.0f;
                            so.FindProperty("_maxIntensity").floatValue = 0.28f;
                            so.FindProperty("_flickerFrequency").floatValue = 22.0f;
                            so.FindProperty("_blackoutProbability").floatValue = 0.40f;
                            so.FindProperty("_minBlackoutDuration").floatValue = 0.25f;
                            so.FindProperty("_maxBlackoutDuration").floatValue = 1.0f;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(flicker);
                        }
                    }
                    else
                    {
                        // Completely black, burned-out fixture — 0 light and pitch-black bulb mesh
                        if (mainLight != null)
                        {
                            mainLight.intensity = 0f;
                            mainLight.enabled = false;
                            EditorUtility.SetDirty(mainLight);
                        }

                        if (flicker != null)
                        {
                            var so = new SerializedObject(flicker);
                            so.FindProperty("_pattern").enumValueIndex = (int)LateSubmission.Environment.FlickerPattern.Off;
                            so.FindProperty("_baseIntensity").floatValue = 0f;
                            so.ApplyModifiedProperties();
                            flicker.enabled = false;
                            EditorUtility.SetDirty(flicker);
                        }

                        if (renderer != null && deadMat != null)
                        {
                            renderer.sharedMaterial = deadMat;
                            EditorUtility.SetDirty(renderer);
                        }
                    }
                }
            }

            // Tone down Washroom_Switch_FoyerLight so it doesn't spill bright light into the corridor
            var foyerLightGo = FindGoByName("Washroom_Switch_FoyerLight");
            if (foyerLightGo != null)
            {
                foreach (var lt in foyerLightGo.GetComponentsInChildren<Light>(true))
                {
                    if (lt.gameObject == foyerLightGo)
                    {
                        lt.intensity = 0.20f;
                        lt.range = 3.5f;
                    }
                    else
                    {
                        lt.intensity = 0f;
                        lt.enabled = false;
                    }
                    EditorUtility.SetDirty(lt);
                }
            }

            // Ensure Directional Light in Floor 02 is off and ambient light is near-zero so player cannot see without flashlight
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0f;
                    l.enabled = false;
                    EditorUtility.SetDirty(l);
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.002f, 0.002f, 0.004f, 1f);
            RenderSettings.reflectionIntensity = 0.02f;

            Log("[Floor02] Corridor ceiling lights configured: 2 faulty flickering lights, 8 completely black, very faint corridor ambient.", "green");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PLAYER FLASHLIGHT, HELD ITEMS, & AUDIO CONSISTENCY
        // ─────────────────────────────────────────────────────────────────────

        private static void ConfigurePlayerFlashlightAndHeldItems(Scene scene)
        {
            // 1. HeldItemController: default to None, align spotlight forward, turn off spotlight at start, and hide 3D flashlight mesh on hands
            var heldControllers = UnityEngine.Object.FindObjectsByType<HeldItemController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var hc in heldControllers)
            {
                var so = new SerializedObject(hc);
                so.FindProperty("_currentHeldType").enumValueIndex = (int)HeldItemType.None;
                var spotProp = so.FindProperty("_flashlightSpotlight");
                if (spotProp != null && spotProp.objectReferenceValue is Light spotLight)
                {
                    spotLight.transform.localRotation = Quaternion.identity;
                    spotLight.enabled = false;
                    EditorUtility.SetDirty(spotLight.transform);
                    EditorUtility.SetDirty(spotLight);
                }

                var vmProp = so.FindProperty("_flashlightViewmodel");
                if (vmProp != null && vmProp.objectReferenceValue is GameObject vmGo)
                {
                    // Keep GameObject active and aligned with camera so child spotlight points straight ahead,
                    // but disable all MeshRenderers / Flashlight_Mesh so the 3D flashlight is never shown on hands
                    vmGo.SetActive(true);
                    vmGo.transform.localRotation = Quaternion.identity;
                    var vmCtrl = vmGo.GetComponent<ViewmodelController>();
                    if (vmCtrl != null)
                    {
                        vmCtrl.enabled = false;
                        EditorUtility.SetDirty(vmCtrl);
                    }
                    foreach (var r in vmGo.GetComponentsInChildren<Renderer>(true))
                    {
                        r.enabled = false;
                        EditorUtility.SetDirty(r);
                    }
                    Transform meshChild = vmGo.transform.Find("Flashlight_Mesh");
                    if (meshChild != null)
                    {
                        meshChild.gameObject.SetActive(false);
                        EditorUtility.SetDirty(meshChild.gameObject);
                    }
                    EditorUtility.SetDirty(vmGo.transform);
                    EditorUtility.SetDirty(vmGo);
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hc);
            }

            // 2. FlashlightController: ensure startsOff and its duplicate Light is disabled
            var flashControllers = UnityEngine.Object.FindObjectsByType<FlashlightController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var fc in flashControllers)
            {
                var so = new SerializedObject(fc);
                so.FindProperty("_startsOn").boolValue = false;
                so.ApplyModifiedProperties();

                var lt = fc.GetComponentInChildren<Light>(true);
                if (lt != null)
                {
                    lt.enabled = false;
                    EditorUtility.SetDirty(lt);
                }
                EditorUtility.SetDirty(fc);
            }

            // 3. PlayerNoiseEmitter: ensure footstep clips are wired across all scenes
            var noiseEmitters = UnityEngine.Object.FindObjectsByType<PlayerNoiseEmitter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var stoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FOOTSTEP_STONE);
            var metalClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FOOTSTEP_METAL);
            var grassClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FOOTSTEP_GRASS);

            foreach (var ne in noiseEmitters)
            {
                var so = new SerializedObject(ne);
                if (stoneClip != null) so.FindProperty("_stoneFootstepClip").objectReferenceValue = stoneClip;
                if (metalClip != null) so.FindProperty("_metalFootstepClip").objectReferenceValue = metalClip;
                if (grassClip != null) so.FindProperty("_grassFootstepClip").objectReferenceValue = grassClip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ne);
            }

            Log($"[{scene.name}] Player flashlight hidden on hands & gated on inventory.");
        }

        private static void RemoveFlashlightPickupsFromScene(Scene scene)
        {
            var pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var p in pickups)
            {
                if (p == null) continue;
                if (p.Item != null && p.Item.ItemType == ItemType.Flashlight)
                {
                    Log($"[{scene.name}] Removed premature Flashlight pickup '{p.gameObject.name}' (only allowed in Cabin104).", "yellow");
                    UnityEngine.Object.DestroyImmediate(p.gameObject);
                }
            }
        }

        private static void VerifyFlashlightPickupInCabin104()
        {
            var pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var p in pickups)
            {
                if (p != null && p.Item != null && p.Item.ItemType == ItemType.Flashlight)
                {
                    p.gameObject.SetActive(true);
                    EditorUtility.SetDirty(p.gameObject);
                    Log($"[Cabin104] Verified Flashlight pickup '{p.gameObject.name}' at {p.transform.position}.", "green");
                    return;
                }
            }

            LogWarning("[Cabin104] Pickup_Flashlight not found in scene — instantiating prefab on Prof's desk.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Pickup_Flashlight.prefab");
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Pickup_Flashlight";
                instance.transform.position = new Vector3(0.45f, 0.82f, 1.2f);
                EditorUtility.SetDirty(instance);
            }
        }

        private static void EnsureConsistentAudioDirector(Scene scene)
        {
            // Remove duplicate Floor02AudioDirector so GameAudioDirector is the single consistent director
            var floor02Directors = UnityEngine.Object.FindObjectsByType<Floor02AudioDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var f2d in floor02Directors)
            {
                if (f2d == null) continue;
                if (f2d.GetComponent<GameAudioDirector>() != null)
                {
                    UnityEngine.Object.DestroyImmediate(f2d);
                }
                else
                {
                    Log($"[{scene.name}] Removed duplicate Floor02AudioDirector '{f2d.gameObject.name}' in favor of unified GameAudioDirector.");
                    UnityEngine.Object.DestroyImmediate(f2d.gameObject);
                }
            }

            var director = UnityEngine.Object.FindFirstObjectByType<GameAudioDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                var go = new GameObject("GameAudioDirector");
                director = go.AddComponent<GameAudioDirector>();
            }
            director.gameObject.SetActive(true);

            var ambientClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AMBIENT_CLIP_PATH);
            var chaseClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(CHASE_CLIP_PATH);
            var stingerClip = AssetDatabase.LoadAssetAtPath<AudioClip>(STINGER_CLIP_PATH);

            var so = new SerializedObject(director);
            if (ambientClip != null) so.FindProperty("_ambientDroneClip").objectReferenceValue     = ambientClip;
            if (chaseClip   != null) so.FindProperty("_chaseMusicClip").objectReferenceValue       = chaseClip;
            if (stingerClip != null) so.FindProperty("_proximityStingerClip").objectReferenceValue = stingerClip;
            so.FindProperty("_targetAmbientVolume").floatValue = 0.45f;
            so.FindProperty("_targetChaseVolume").floatValue   = 0.75f;
            so.FindProperty("_fadeDuration").floatValue        = 1.2f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);

            Log($"[{scene.name}] GameAudioDirector configured with consistent Floor 02 music (Ambient='{ambientClip?.name}', Chase='{chaseClip?.name}').");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MENU & OBSOLETE UI REMOVAL
        // ─────────────────────────────────────────────────────────────────────

        private static void RemoveMenuCanvases(Scene scene)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var c in canvases)
            {
                if (c == null) continue;
                string n = c.gameObject.name.ToLower();
                if (n.Contains("bloodlines") || n.Contains("mainmenu") || n.Contains("pausemenu"))
                {
                    Log($"[{scene.name}] Deleted menu canvas: '{c.gameObject.name}'", "yellow");
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                }
            }

            // Also clean up any loose leftover panels or buttons
            string[] looseMenuNames = { "MainPanel", "PauseMenuPanel", "InstructionsPanel", "Btn_QuitMenu", "Btn_Guide", "Btn_SurvivalGuide" };
            foreach (var name in looseMenuNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    Log($"[{scene.name}] Deleted loose menu object: '{name}'", "yellow");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SCENE HIERARCHY ORGANIZER
        // ─────────────────────────────────────────────────────────────────────

        private static void OrganizeSceneHierarchy(Scene scene)
        {
            Log($"[{scene.name}] Organizing scene hierarchy into logical groups...", "cyan");

            // Master root groups
            var rootEnv       = GetOrCreateRootGroup("[01_ENVIRONMENT]");
            var rootLighting  = GetOrCreateRootGroup("[02_LIGHTING]");
            var rootGameplay  = GetOrCreateRootGroup("[03_GAMEPLAY]");
            var rootManagers  = GetOrCreateRootGroup("[04_MANAGERS]");

            // Sub-groups under [01_ENVIRONMENT]
            var subArch  = GetOrCreateChildGroup(rootEnv, "Architecture");
            var subDoors = GetOrCreateChildGroup(rootEnv, "Doors");
            var subProps = GetOrCreateChildGroup(rootEnv, "Props");

            // Sub-groups under [03_GAMEPLAY]
            var subSpawns       = GetOrCreateChildGroup(rootGameplay, "SpawnPoints");
            var subInteractables= GetOrCreateChildGroup(rootGameplay, "Interactables");
            var subEnemies      = GetOrCreateChildGroup(rootGameplay, "Enemies");
            var subTriggers     = GetOrCreateChildGroup(rootGameplay, "Triggers");

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go == null) continue;
                string n = go.name;

                // Skip our master group roots
                if (n.StartsWith("[0") || n == rootEnv.name || n == rootLighting.name || n == rootGameplay.name || n == rootManagers.name)
                    continue;

                string lower = n.ToLower();

                // 1. Lighting
                if (go.GetComponent<Light>() != null || go.GetComponent<ReflectionProbe>() != null || lower.Contains("light") || lower.Contains("probe"))
                {
                    MoveToGroup(go, rootLighting);
                    continue;
                }

                // 2. Player
                if (go.CompareTag("Player") || go.GetComponent<FPSController>() != null || lower == "player")
                {
                    MoveToGroup(go, rootGameplay);
                    continue;
                }

                // 3. Spawn points (transform-only without renderers or colliders)
                if (lower.Contains("spawn") && go.GetComponent<Renderer>() == null && go.GetComponent<Collider>() == null)
                {
                    MoveToGroup(go, subSpawns);
                    continue;
                }

                // 4. Enemies (Boss monster, mutant, entity)
                if (go.GetComponent<BossMonsterController>() != null || lower.Contains("monster") || lower.Contains("mutant") || lower.Contains("entity") || lower.Contains("boss"))
                {
                    MoveToGroup(go, subEnemies);
                    continue;
                }

                // 5. Triggers
                if (go.GetComponent<BossSpawnTrigger>() != null || (lower.Contains("trigger") && go.GetComponent<Collider>() != null && go.GetComponent<Collider>().isTrigger))
                {
                    MoveToGroup(go, subTriggers);
                    continue;
                }

                // 6. Interactables (interactive doors, keys, notes, pickups, workbench)
                if (go.GetComponent<IInteractable>() != null || go.GetComponent<SceneTransitionDoor>() != null ||
                    go.GetComponent<BathroomDoorController>() != null || go.GetComponent<WoodDoorController>() != null ||
                    go.GetComponent<ItemPickup>() != null || go.GetComponent<InspectNote>() != null ||
                    go.GetComponent<WorkbenchCrafting>() != null || lower.Contains("pickup") || lower.Contains("note_") ||
                    lower.Contains("door_mechatronics") || lower.Contains("workbench"))
                {
                    MoveToGroup(go, subInteractables);
                    continue;
                }

                // 7. Managers, Canvases, Directors
                if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null || go.GetComponent<SceneFader>() != null ||
                    go.GetComponent<DemoCompletionUI>() != null || go.GetComponent<Canvas>() != null ||
                    lower.Contains("director") || lower.Contains("manager") || lower.Contains("fader") ||
                    lower.Contains("hud") || lower.Contains("eventsystem") || lower.Contains("navmeshsurface"))
                {
                    MoveToGroup(go, rootManagers);
                    continue;
                }

                // 8. Architecture (walls, floors, ceilings, pillars, corners, shell, corridor)
                if (lower.Contains("wall") || lower.Contains("floor") || lower.Contains("ceiling") ||
                    lower.Contains("pillar") || lower.Contains("corner") || lower.Contains("shell") || lower.Contains("corridor"))
                {
                    MoveToGroup(go, subArch);
                    continue;
                }

                // 9. Static non-interactive doors / door frames
                if (lower.Contains("door"))
                {
                    MoveToGroup(go, subDoors);
                    continue;
                }

                // 10. Everything else is Props (tables, chairs, clocks, signs, cabinets, bins, etc.)
                MoveToGroup(go, subProps);
            }

            Log($"[{scene.name}] Hierarchy cleanly organized.");
        }

        private static GameObject GetOrCreateRootGroup(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
            return go;
        }

        private static GameObject GetOrCreateChildGroup(GameObject parent, string childName)
        {
            var child = parent.transform.Find(childName);
            if (child != null) return child.gameObject;

            var go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void MoveToGroup(GameObject go, GameObject group)
        {
            if (go == null || group == null) return;
            go.transform.SetParent(group.transform, true); // true = worldPositionStays: preserves world coords, rotation, and scale!
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Floor02 Sub-Configurators
        // ─────────────────────────────────────────────────────────────────────

        private static void ConfigureMechatronicsLabDoor()
        {
            GameObject door = FindGoByName("Door_MechatronicsLab") ?? FindGoByName("Mechatronics_Lab_Door");
            if (door == null) { LogWarning("[Floor02] Mechatronics Lab door not found — skip."); return; }

            door.name = "Door_MechatronicsLab";
            if (PrefabUtility.IsPartOfAnyPrefab(door))
            {
                PrefabUtility.UnpackPrefabInstance(door, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Log("[Floor02] Mechatronics door — unpacked prefab.");
            }

            Transform doorLeaf = door.transform.Find("DoorLeaf");
            if (doorLeaf == null)
            {
                var leafGo = new GameObject("DoorLeaf");
                doorLeaf = leafGo.transform;
                doorLeaf.SetParent(door.transform, false);
                doorLeaf.localPosition = new Vector3(-1.29f, 0f, -0.02f);
                doorLeaf.localRotation = Quaternion.identity;
                doorLeaf.localScale    = Vector3.one;

                string[] movingParts = { "door_2_LOD0","door_2_LOD1","door_2_LOD2","window","handle_2","Door latch_2","Latch bolt_2" };
                foreach (var partName in movingParts)
                {
                    var part = door.transform.Find(partName);
                    if (part != null) part.SetParent(doorLeaf, true);
                }
            }

            foreach (var col in door.GetComponentsInChildren<Collider>(true))
            {
                if (col.transform != doorLeaf)
                    UnityEngine.Object.DestroyImmediate(col);
            }

            var leafBox = doorLeaf.GetComponent<BoxCollider>() ?? doorLeaf.gameObject.AddComponent<BoxCollider>();
            var renderers = doorLeaf.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                leafBox.center = doorLeaf.InverseTransformPoint(b.center);
                Vector3 localSize = doorLeaf.InverseTransformVector(b.size);
                leafBox.size   = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Max(0.08f, Mathf.Abs(localSize.z)));
            }
            else
            {
                leafBox.center = new Vector3(0.425f, 1.05f, 0.02f);
                leafBox.size   = new Vector3(0.85f, 2.1f, 0.08f);
            }
            leafBox.isTrigger = false;

            var controller = door.GetComponent<WoodDoorController>() ?? door.AddComponent<WoodDoorController>();
            var openClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(DOOR_OPEN_SFX);
            var closeClip  = AssetDatabase.LoadAssetAtPath<AudioClip>(DOOR_CLOSE_SFX);

            var so = new SerializedObject(controller);
            so.FindProperty("_doorLeaf").objectReferenceValue          = doorLeaf;
            so.FindProperty("_openAngle").floatValue                   = -90.0f;
            so.FindProperty("_animationDuration").floatValue           = 0.85f;
            so.FindProperty("_isOpen").boolValue                       = false;
            so.FindProperty("_isLocked").boolValue                     = false;
            so.FindProperty("_isPermanentlyLocked").boolValue          = false;
            so.FindProperty("_requiredKey").enumValueIndex             = (int)ItemType.MechatronicsKey;
            so.FindProperty("_lockedMessage").stringValue              = "Mechatronics Lab — Press [E] to enter.";
            so.FindProperty("_canBolt").boolValue                      = false;
            so.FindProperty("_openSfx").objectReferenceValue           = openClip;
            so.FindProperty("_closeSfx").objectReferenceValue          = closeClip;
            so.ApplyModifiedProperties();

            controller.UpdateColliders();
            Log("[Floor02] Mechatronics Lab door configured (unlocked, hinge wired, colliders clean).");
        }

        private static void ConfigureBathroomDoors()
        {
            var bathroomDoors = UnityEngine.Object.FindObjectsByType<BathroomDoorController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int count = 0;
            foreach (var bd in bathroomDoors)
            {
                var so = new SerializedObject(bd);
                if (bd.CanBolt || count == 0)
                {
                    so.FindProperty("_canBolt").boolValue  = true;
                    so.FindProperty("_isBolted").boolValue = false;
                    so.FindProperty("_isOpen").boolValue   = false;
                    Log($"[Floor02] Bathroom door '{bd.gameObject.name}' — safe room bolt enabled.");
                }
                so.ApplyModifiedProperties();
                count++;
            }

            var woodDoors = UnityEngine.Object.FindObjectsByType<WoodDoorController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wd in woodDoors)
            {
                string n = wd.gameObject.name.ToLower();
                if (n.Contains("bathroom") || n.Contains("restroom") || n.Contains("toilet"))
                {
                    var so = new SerializedObject(wd);
                    so.FindProperty("_isLocked").boolValue          = false;
                    so.FindProperty("_isPermanentlyLocked").boolValue = false;
                    so.FindProperty("_canBolt").boolValue            = true;
                    so.ApplyModifiedProperties();
                    wd.UpdateColliders();
                    Log($"[Floor02] Bathroom WoodDoor '{wd.gameObject.name}' — safe room bolt enabled.");
                }
            }
        }

        private static void ConfigureECLabDoors()
        {
            var woodDoors = UnityEngine.Object.FindObjectsByType<WoodDoorController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wd in woodDoors)
            {
                string n = wd.gameObject.name.ToLower();
                if (n.Contains("eclab") || n.Contains("ec_lab") || n.Contains("electrical"))
                {
                    var so = new SerializedObject(wd);
                    so.FindProperty("_isLocked").boolValue          = false;
                    so.FindProperty("_isPermanentlyLocked").boolValue = false;
                    so.FindProperty("_canBolt").boolValue            = false;
                    so.ApplyModifiedProperties();
                    wd.UpdateColliders();
                    Log($"[Floor02] EC Lab door '{wd.gameObject.name}' — unlocked.");
                }
            }
        }

        private static void ConfigureBossMonster()
        {
            var boss = FindInactive<BossMonsterController>();
            if (boss == null) { LogWarning("[Floor02] BossMonsterController not found!"); return; }

            boss.gameObject.SetActive(true);

            foreach (var anim in boss.GetComponentsInChildren<Animator>(true))
            {
                if (anim != null)
                {
                    anim.applyRootMotion = false;
                    EditorUtility.SetDirty(anim);
                }
            }

            var agent = boss.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.radius = 0.25f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.stoppingDistance = 0.15f;
                agent.acceleration = 28f;
                agent.angularSpeed = 540f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                EditorUtility.SetDirty(agent);
            }

            var so = new SerializedObject(boss);
            so.FindProperty("_startChasingImmediately").boolValue = false; // Stays dormant until door opened
            so.FindProperty("_maxHealth").floatValue              = 100f;  // 2 Light Pistol shots (50 dmg each) kills it
            so.FindProperty("_maxStunsBeforeDefeat").intValue     = 2;
            so.FindProperty("_attackDistance").floatValue         = 3.0f;
            so.FindProperty("_attackDamage").floatValue           = 15f;   // Lower damage to player
            so.FindProperty("_attackInterval").floatValue         = 1.8f;
            so.FindProperty("_firstAttackDelay").floatValue       = 0.75f;
            so.FindProperty("_attackWindupDelay").floatValue      = 0.55f;
            so.FindProperty("_chaseSpeed").floatValue             = 4.6f;
            so.FindProperty("_walkSpeed").floatValue              = 2.4f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(boss);

            Log($"[Floor02] Boss Monster '{boss.gameObject.name}' — NavMeshAgent enabled (R=0.25, H=1.8), primary NavMeshSurface pathfinding + doorway fallback.");
        }

        private static void ConfigureBossSpawnTrigger()
        {
            var mechatroDoor = FindGoByName("Door_MechatronicsLab") ?? FindGoByName("Mechatronics_Lab_Door");
            WoodDoorController doorController = mechatroDoor != null
                ? mechatroDoor.GetComponent<WoodDoorController>()
                : null;

            var trigger = FindInactive<BossSpawnTrigger>();
            if (trigger == null)
            {
                var host = mechatroDoor != null ? mechatroDoor : new GameObject("BossSpawnTrigger_Host");
                trigger = host.AddComponent<BossSpawnTrigger>();
                Log("[Floor02] BossSpawnTrigger created on Mechatronics door.");
            }

            trigger.gameObject.SetActive(true);

            var so = new SerializedObject(trigger);
            if (doorController != null)
            {
                so.FindProperty("_mechatronicsLabDoor").objectReferenceValue = doorController;
                Log($"[Floor02] BossSpawnTrigger wired to door: '{doorController.gameObject.name}'");
            }
            so.ApplyModifiedProperties();

            var col = trigger.GetComponent<BoxCollider>();
            if (col != null && col.isTrigger)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void ConfigurePaywallUI()
        {
            var demoUI = FindInactive<DemoCompletionUI>();
            if (demoUI == null)
            {
                var uiGo = new GameObject("DemoCompletionDirector");
                demoUI = uiGo.AddComponent<DemoCompletionUI>();
                Log("[Floor02] DemoCompletionUI created.");
            }

            var qrSprite = AssetDatabase.LoadAssetAtPath<Sprite>(QR_TEXTURE_PATH);
            if (qrSprite != null)
            {
                var so = new SerializedObject(demoUI);
                so.FindProperty("_qrCodeSprite").objectReferenceValue = qrSprite;
                so.ApplyModifiedProperties();
                Log("[Floor02] QR Code sprite assigned to DemoCompletionUI.");
            }
        }

        private static void BakeNavMesh()
        {
            // Ensure NavMeshModifiers on doors/characters are set to Remove Object (ignoreFromBuild = true)
            var modifiers = UnityEngine.Object.FindObjectsByType<NavMeshModifier>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mod in modifiers)
            {
                if (mod == null) continue;
                string n = mod.gameObject.name.ToLower();
                if (n.Contains("door") || n.Contains("player") || n.Contains("monster") || n.Contains("mutant") || n.Contains("boss"))
                {
                    mod.ignoreFromBuild = true;
                    mod.applyToChildren = true;
                    EditorUtility.SetDirty(mod);
                }
            }

            var navSurface = FindInactive<NavMeshSurface>();
            if (navSurface == null)
            {
                var navGo = new GameObject("NavMeshSurface_Floor02");
                navSurface = navGo.AddComponent<NavMeshSurface>();
                navSurface.collectObjects = CollectObjects.All;
                navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                navSurface.overrideVoxelSize = true;
                navSurface.voxelSize = 0.08f;
                Log("[Floor02] NavMeshSurface created.");
            }
            else
            {
                navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                navSurface.overrideVoxelSize = true;
                navSurface.voxelSize = 0.08f;
                EditorUtility.SetDirty(navSurface);
            }

            if (navSurface.navMeshData == null)
            {
                try
                {
                    navSurface.BuildNavMesh();
                    Log("[Floor02] NavMesh baked successfully!", "green");
                }
                catch (Exception ex)
                {
                    LogWarning($"[Floor02] NavMesh bake: {ex.Message}");
                }
            }
            else
            {
                Log($"[Floor02] Preserved existing baked NavMeshData: '{navSurface.navMeshData.name}'.", "green");
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        public static void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
                Log("EventSystem created.");
            }

#if ENABLE_INPUT_SYSTEM
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);

            var inputModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
                Log("Added InputSystemUIInputModule to EventSystem.");
            }
#else
            var sm = es.GetComponent<StandaloneInputModule>();
            if (sm == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void EnsureSceneFader(Scene scene)
        {
            var fader = UnityEngine.Object.FindFirstObjectByType<SceneFader>(FindObjectsInactive.Include);
            if (fader != null) return;

            var canvasGo = new GameObject("SceneFader_Canvas");
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9998;
            canvasGo.AddComponent<CanvasScaler>();

            var panelGo = new GameObject("FadePanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.sizeDelta  = Vector2.zero;
            var img       = panelGo.AddComponent<Image>();
            img.color     = Color.black;
            var cg        = panelGo.AddComponent<CanvasGroup>();

            var faderComp = canvasGo.AddComponent<SceneFader>();
            var so = new SerializedObject(faderComp);
            so.FindProperty("_canvasGroup").objectReferenceValue = cg;
            so.FindProperty("_fadeInOnStart").boolValue          = true;
            so.FindProperty("_defaultFadeDuration").floatValue   = 1.2f;
            so.ApplyModifiedProperties();

            Log($"[{scene.name}] SceneFader created.");
        }

        private static void EnsureSpawnPoint(string spawnName, Vector3 defaultPos)
        {
            var existing = FindGoByName(spawnName);
            if (existing != null) return;

            var go = new GameObject(spawnName);
            go.transform.position = defaultPos;
            Log($"Created spawn point '{spawnName}' at {defaultPos}.");
        }

        private static void SetOrCreateSpawnPoint(string spawnName, Vector3 pos, Quaternion rot)
        {
            var go = FindGoByName(spawnName);
            if (go == null)
            {
                go = new GameObject(spawnName);
            }
            go.SetActive(true);
            go.transform.position = pos;
            go.transform.rotation = rot;
            EditorUtility.SetDirty(go);
            Log($"Configured spawn point '{spawnName}' at {pos} (Yaw={rot.eulerAngles.y:F0}°).");
        }

        private static void SetPlayerStartPose(Vector3 pos, Quaternion rot)
        {
            var fps = UnityEngine.Object.FindFirstObjectByType<FPSController>(FindObjectsInactive.Include);
            if (fps == null) return;

            fps.gameObject.SetActive(true);
            fps.transform.position = pos;
            fps.transform.rotation = rot;
            EditorUtility.SetDirty(fps.gameObject);
            EditorUtility.SetDirty(fps.transform);
            Log($"Positioned Player at start spawn {pos} (Yaw={rot.eulerAngles.y:F0}°).");
        }

        private static GameObject FindGoByName(string name)
        {
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name) return go;
            }
            return null;
        }

        private static T FindInactive<T>() where T : UnityEngine.Object
            => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static void SaveScene(Scene scene, string msg)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log(msg);
        }

        private static void Log(string msg, string color = "white")
            => Debug.Log($"<color={color}>[FullBuildIntegrator] {msg}</color>");

        private static void LogWarning(string msg)
            => Debug.LogWarning($"[FullBuildIntegrator] {msg}");

        private static void LogError(string msg)
            => Debug.LogError($"[FullBuildIntegrator] {msg}");
    }

    // =========================================================================
    //  Extension: expose target scene name from SceneTransitionDoor
    // =========================================================================
    public static class SceneTransitionDoorExtensions
    {
        public static string GetTargetScene(this SceneTransitionDoor door)
        {
            var so = new SerializedObject(door);
            return so.FindProperty("_targetSceneName").stringValue;
        }
    }
}
