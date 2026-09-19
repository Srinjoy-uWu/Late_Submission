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
    public static class RestoreCorridorHierarchy
    {
        [MenuItem("Tools/Late Submission/Restore Floor 1 Corridor Scene")]
        public static void RestoreScene()
        {
            Debug.Log("<color=cyan>[RestoreCorridor] Reconstructing Floor01_Main corridor hierarchy...</color>");

            // Open or prepare Floor01_Main
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Floor01_Main.unity");

            // Clean up existing duplicates
            string[] cleanRoots = { "Wall_Spawn", "Floor_Spawn", "Ceiling_Spawn", "Wall_Corners_Spawn", "Props_Spawn", "door_2", "Security_Door", "workplace_signs" };
            foreach (var rootName in cleanRoots)
            {
                var existing = GameObject.Find(rootName);
                if (existing != null) Object.DestroyImmediate(existing);
            }

            // 1. Floor_Spawn (2 pieces of 4x16 floor tile covering 32m from X = +8 to -24)
            var floorParent = new GameObject("Floor_Spawn");
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/floor/floor_ceiling_4x16.prefab");
            if (floorPrefab != null)
            {
                var f1 = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                f1.name = "floor_ceiling_4x16";
                f1.transform.SetParent(floorParent.transform);
                f1.transform.position = new Vector3(0f, 0f, 0f);

                var f2 = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                f2.name = "floor_ceiling_4x16 (1)";
                f2.transform.SetParent(floorParent.transform);
                f2.transform.position = new Vector3(-16f, 0f, 0f);
            }

            // 2. Ceiling_Spawn (2 pieces of 4x16 ceiling tile at Y = 3.0 + 6 ceiling lights)
            var ceilingParent = new GameObject("Ceiling_Spawn");
            var ceilingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/ceiling/floor_ceiling_4x16.prefab");
            if (ceilingPrefab != null)
            {
                var c1 = (GameObject)PrefabUtility.InstantiatePrefab(ceilingPrefab);
                c1.name = "floor_ceiling_4x16";
                c1.transform.SetParent(ceilingParent.transform);
                c1.transform.position = new Vector3(0f, 3f, 0f);

                var c2 = (GameObject)PrefabUtility.InstantiatePrefab(ceilingPrefab);
                c2.name = "floor_ceiling_4x16 (1)";
                c2.transform.SetParent(ceilingParent.transform);
                c2.transform.position = new Vector3(-16f, 3f, 0f);
            }

            // Ceiling Lights spaced along the corridor
            var lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/props/ceiling_light.prefab");
            float[] lightX = { 6f, 1f, -4f, -9f, -14f, -19f };
            for (int i = 0; i < lightX.Length; i++)
            {
                if (lightPrefab != null)
                {
                    var lightObj = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab);
                    lightObj.name = i == 0 ? "ceiling_light" : $"ceiling_light ({i})";
                    lightObj.transform.SetParent(ceilingParent.transform);
                    lightObj.transform.position = new Vector3(lightX[i], 2.95f, 0f);
                }
            }

            // 3. Wall_Spawn (Modular wall pieces along Z = +2.0 and Z = -2.0)
            var wallParent = new GameObject("Wall_Spawn");
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/walls/wall_piece01.prefab");
            if (wallPrefab != null)
            {
                // North walls (Z = 2.0, facing south - Euler 0, 180, 0)
                int count = 0;
                for (float x = 7.0f; x >= -23.0f; x -= 3.0f)
                {
                    // Skip doorway spots at X = 1.47 and X = -16.30
                    if (Mathf.Abs(x - 1.47f) < 1.2f || Mathf.Abs(x - (-16.30f)) < 1.2f) continue;

                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                    w.name = count == 0 ? "wall_piece01" : $"wall_piece01 ({count})";
                    w.transform.SetParent(wallParent.transform);
                    w.transform.position = new Vector3(x, 0f, 2.0f);
                    w.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    count++;
                }

                // South walls (Z = -2.0, facing north - Euler 0, 0, 0)
                for (float x = 7.0f; x >= -23.0f; x -= 3.0f)
                {
                    // Skip doorway spot at X = -4.05
                    if (Mathf.Abs(x - (-4.05f)) < 1.2f) continue;

                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                    w.name = $"wall_piece01 ({count})";
                    w.transform.SetParent(wallParent.transform);
                    w.transform.position = new Vector3(x, 0f, -2.0f);
                    w.transform.rotation = Quaternion.identity;
                    count++;
                }
            }

            // 4. Wall_Corners_Spawn
            var cornerParent = new GameObject("Wall_Corners_Spawn");
            var cornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/walls/wall_corner01.prefab");
            if (cornerPrefab != null)
            {
                Vector3[] cornerPositions = {
                    new Vector3(8.0f, 0f, 2.0f), new Vector3(8.0f, 0f, -2.0f),
                    new Vector3(-24.0f, 0f, 2.0f), new Vector3(-24.0f, 0f, -2.0f)
                };
                for (int i = 0; i < cornerPositions.Length; i++)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(cornerPrefab);
                    c.name = $"wall_corner01 ({i + 1})";
                    c.transform.SetParent(cornerParent.transform);
                    c.transform.position = cornerPositions[i];
                }
            }

            // 5. Root Doors
            // door_2 (Side door at 1.47, 0.02, 1.98)
            var door2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_2.prefab");
            if (door2Prefab != null)
            {
                var d2 = (GameObject)PrefabUtility.InstantiatePrefab(door2Prefab);
                d2.name = "door_2";
                d2.transform.position = new Vector3(1.47f, 0.02f, 1.98f);
                d2.transform.rotation = Quaternion.identity;
            }

            // Security_Door at (-16.30, 0.01, 1.97)
            var door3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_3.prefab");
            if (door3Prefab != null)
            {
                var sd = (GameObject)PrefabUtility.InstantiatePrefab(door3Prefab);
                sd.name = "Security_Door";
                sd.transform.position = new Vector3(-16.30f, 0.01f, 1.97f);
                sd.transform.rotation = Quaternion.identity;
            }

            // 6. Props_Spawn
            var propsParent = new GameObject("Props_Spawn");

            // Entrance Door (Fire Escape at 8.20, -0.36, 0.12)
            var fireEscapePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/fire-escape-exit-doors/source/Fire Escape.blend");
            if (fireEscapePrefab != null)
            {
                var ent = (GameObject)PrefabUtility.InstantiatePrefab(fireEscapePrefab);
                ent.name = "Entrance_Door_Spawn";
                ent.transform.SetParent(propsParent.transform);
                ent.transform.position = new Vector3(8.20f, -0.36f, 0.12f);
                ent.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                var ext = (GameObject)PrefabUtility.InstantiatePrefab(fireEscapePrefab);
                ext.name = "Exit_Door_Spawn";
                ext.transform.SetParent(propsParent.transform);
                ext.transform.position = new Vector3(-23.53f, -0.36f, 0.12f);
                ext.transform.rotation = Quaternion.identity;
            }

            // door_2 (1) (Opposite side door at -4.05, 0.02, -1.93)
            if (door2Prefab != null)
            {
                var d2_1 = (GameObject)PrefabUtility.InstantiatePrefab(door2Prefab);
                d2_1.name = "door_2 (1)";
                d2_1.transform.SetParent(propsParent.transform);
                d2_1.transform.position = new Vector3(-4.05f, 0.02f, -1.93f);
                d2_1.transform.rotation = Quaternion.identity;
            }

            // Table in corridor at (-4.61, 0.00, 1.38)
            var tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Morgue Room PBR/Prefabs/props/table.prefab");
            if (tablePrefab != null)
            {
                var tbl = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
                tbl.name = "table";
                tbl.transform.SetParent(propsParent.transform);
                tbl.transform.position = new Vector3(-4.61f, 0.00f, 1.38f);
            }

            // Exit Signs
            var exitSignPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/exit-sign/source/Exit_LP.fbx");
            if (exitSignPrefab != null)
            {
                var s1 = (GameObject)PrefabUtility.InstantiatePrefab(exitSignPrefab);
                s1.name = "Exit_LP";
                s1.transform.SetParent(propsParent.transform);
                s1.transform.position = new Vector3(7.91f, 2.55f, 0.03f);
                s1.transform.rotation = Quaternion.Euler(0f, 270f, 0f);

                var s2 = (GameObject)PrefabUtility.InstantiatePrefab(exitSignPrefab);
                s2.name = "Exit_LP (1)";
                s2.transform.SetParent(propsParent.transform);
                s2.transform.position = new Vector3(-23.33f, 2.55f, 0.25f);
                s2.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Fire Extinguishers on the wall
            var extPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/fire-extinguisher-game-ready/source/Fire Extinguisher.fbx");
            if (extPrefab != null)
            {
                float[] extX = { 4.5f, -8.5f, -20.5f };
                for (int i = 0; i < extX.Length; i++)
                {
                    var fe = (GameObject)PrefabUtility.InstantiatePrefab(extPrefab);
                    fe.name = i == 0 ? "Fire Extinguisher" : $"Fire Extinguisher ({i})";
                    fe.transform.SetParent(propsParent.transform);
                    fe.transform.position = new Vector3(extX[i], 1.2f, 1.9f);
                    fe.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }

            // Waste Cans
            var canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/Waste Bins/Waste Can/Prefabs/Waste Can.prefab");
            if (canPrefab != null)
            {
                Vector3[] canPositions = { new Vector3(2.5f, 0f, 1.7f), new Vector3(-3.0f, 0f, -1.7f), new Vector3(-17.5f, 0f, 1.7f) };
                for (int i = 0; i < canPositions.Length; i++)
                {
                    var can = (GameObject)PrefabUtility.InstantiatePrefab(canPrefab);
                    can.name = $"Waste Can ({i + 1})";
                    can.transform.SetParent(propsParent.transform);
                    can.transform.position = canPositions[i];
                }
            }

            // Cork Board on wall
            var corkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/psx-corkevidence-board-2/source/Cork Board 07.fbx");
            if (corkPrefab != null)
            {
                var cb = (GameObject)PrefabUtility.InstantiatePrefab(corkPrefab);
                cb.name = "Cork Board 07";
                cb.transform.SetParent(propsParent.transform);
                cb.transform.position = new Vector3(-1.0f, 1.5f, 1.95f);
                cb.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // Wall Clock
            var clockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/psx-style-vintage-wall-clocks/source/Wall Clock.prefab");
            if (clockPrefab != null)
            {
                var ck = (GameObject)PrefabUtility.InstantiatePrefab(clockPrefab);
                ck.name = "Wall Clock";
                ck.transform.SetParent(propsParent.transform);
                ck.transform.position = new Vector3(7.5f, 2.2f, 1.95f);
                ck.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // Workplace signs
            var signsObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported Assets/workplace-safety-and-security-signs-vol1/source/workplace_signs/workplace_signs.obj");
            if (signsObj != null)
            {
                var signs = (GameObject)PrefabUtility.InstantiatePrefab(signsObj);
                signs.name = "workplace_signs";
                signs.transform.position = new Vector3(-15.0f, 1.6f, 1.95f);
            }

            // Now run Floor01SetupPipeline to ensure door scripts, colliders, triggers, and player are wired
            Floor01SetupPipeline.SetupFloor1DoorsAndScene();

            // Save the scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green>[RestoreCorridor] Successfully restored complete corridor hierarchy!</color>");
        }
    }
}
