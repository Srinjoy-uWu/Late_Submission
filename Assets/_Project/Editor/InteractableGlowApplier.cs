using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LateSubmission.Interaction;
using LateSubmission.Inventory;

namespace LateSubmission.Editor
{
    public static class InteractableGlowApplier
    {

        [MenuItem("Tools/Late Submission/Apply Interactable Glow To All Prefabs and Scenes")]
        public static void ApplyGlowToAllAssetsAndScenes()
        {
            Debug.Log("<color=cyan>[GlowApplier] Starting visual affordance application across all prefabs & scenes...</color>");

            // 1. Update Prefabs
            ApplyToPrefabs();

            // 2. Update Floor01_Main Scene
            ApplyToFloor01Main();

            // 3. Update Floor01_SecurityRoom Scene
            ApplyToSecurityRoom();

            // Return to Floor01_Main
            EditorSceneManager.OpenScene("Assets/Scenes/Floor01_Main.unity");

            Debug.Log("<color=green>[GlowApplier] Universal glow affordance successfully applied to all prefabs and scenes!</color>");
        }

        private static void ApplyToPrefabs()
        {
            string[] notePrefabs = {
                "Assets/_Project/Prefabs/Interactables/Note_StoryLog1_SecurityMemo.prefab",
                "Assets/_Project/Prefabs/Interactables/Note_StoryLog2_FacultyMemo.prefab"
            };

            foreach (var path in notePrefabs)
            {
                ConfigurePrefabGlow(path, GlowCategory.Note);
            }

            string[] pickupPrefabs = {
                "Assets/_Project/Prefabs/Interactables/Pickup_FacultyKey.prefab",
                "Assets/_Project/Prefabs/Interactables/Pickup_CoverSheet.prefab"
            };

            foreach (var path in pickupPrefabs)
            {
                ConfigurePrefabGlow(path, GlowCategory.Pickup);
            }
        }

        private static void ConfigurePrefabGlow(string prefabPath, GlowCategory category)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[GlowApplier] Prefab not found at path: '{prefabPath}'");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var glow = root.GetComponent<InteractableGlow>();
                if (glow == null) glow = root.AddComponent<InteractableGlow>();

                ConfigureGlowSerialized(glow, category);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[GlowApplier] Configured InteractableGlow on prefab '{prefabAsset.name}' with category {category}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyToFloor01Main()
        {
            string scenePath = "Assets/Scenes/Floor01_Main.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"[GlowApplier] Failed to open scene: {scenePath}");
                return;
            }

            bool modified = false;

            // Notice Note on table
            var notice = FindGameObjectByName("Corridor_Table_Notice");
            if (notice != null)
            {
                var glow = notice.GetComponent<InteractableGlow>();
                if (glow == null) glow = notice.AddComponent<InteractableGlow>();
                ConfigureGlowSerialized(glow, GlowCategory.Note);
                EditorUtility.SetDirty(notice);
                modified = true;
                Debug.Log("[GlowApplier] Configured InteractableGlow on 'Corridor_Table_Notice'.");
            }

            // Scan any InspectNotes in Floor01_Main
            var allNotes = Object.FindObjectsByType<InspectNote>(FindObjectsSortMode.None);
            foreach (var note in allNotes)
            {
                var glow = note.GetComponent<InteractableGlow>();
                if (glow == null) glow = note.gameObject.AddComponent<InteractableGlow>();
                ConfigureGlowSerialized(glow, GlowCategory.Note);
                EditorUtility.SetDirty(note.gameObject);
                modified = true;
            }

            var allPickups = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
            foreach (var pickup in allPickups)
            {
                var glow = pickup.GetComponent<InteractableGlow>();
                if (glow == null) glow = pickup.gameObject.AddComponent<InteractableGlow>();
                ConfigureGlowSerialized(glow, GlowCategory.Pickup);
                EditorUtility.SetDirty(pickup.gameObject);
                modified = true;
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[GlowApplier] Saved Floor01_Main with glowing interactables.");
            }
        }

        private static void ApplyToSecurityRoom()
        {
            string scenePath = "Assets/Scenes/Floor01_SecurityRoom.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"[GlowApplier] Security room scene does not exist at {scenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"[GlowApplier] Failed to open scene: {scenePath}");
                return;
            }

            bool modified = false;

            // Scan notes & pickups
            var allNotes = Object.FindObjectsByType<InspectNote>(FindObjectsSortMode.None);
            foreach (var note in allNotes)
            {
                var glow = note.GetComponent<InteractableGlow>();
                if (glow == null) glow = note.gameObject.AddComponent<InteractableGlow>();
                ConfigureGlowSerialized(glow, GlowCategory.Note);
                EditorUtility.SetDirty(note.gameObject);
                modified = true;
                Debug.Log($"[GlowApplier] Configured InteractableGlow on note '{note.gameObject.name}'.");
            }

            var allPickups = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
            foreach (var pickup in allPickups)
            {
                var glow = pickup.GetComponent<InteractableGlow>();
                if (glow == null) glow = pickup.gameObject.AddComponent<InteractableGlow>();
                ConfigureGlowSerialized(glow, GlowCategory.Pickup);
                EditorUtility.SetDirty(pickup.gameObject);
                modified = true;
                Debug.Log($"[GlowApplier] Configured InteractableGlow on pickup '{pickup.gameObject.name}'.");
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[GlowApplier] Saved Floor01_SecurityRoom with glowing interactables.");
            }
        }

        public static void ConfigureGlowSerialized(InteractableGlow glow, GlowCategory category)
        {
            if (glow == null) return;

            glow.Configure(category);

            var so = new SerializedObject(glow);
            so.FindProperty("_category").enumValueIndex = (int)category;

            switch (category)
            {
                case GlowCategory.Note:
                    so.FindProperty("_glowColor").colorValue = new Color(1.0f, 0.68f, 0.22f);
                    so.FindProperty("_minIntensity").floatValue = 0.08f;
                    so.FindProperty("_maxIntensity").floatValue = 0.28f;
                    so.FindProperty("_pulseSpeed").floatValue = 1.8f;
                    so.FindProperty("_hoverMultiplier").floatValue = 1.35f;
                    so.FindProperty("_useSubtleLightHalo").boolValue = true;
                    so.FindProperty("_haloRange").floatValue = 0.5f;
                    so.FindProperty("_haloIntensity").floatValue = 0.15f;
                    so.FindProperty("_onlyWhenUnlockable").boolValue = false;
                    break;

                case GlowCategory.Pickup:
                    so.FindProperty("_glowColor").colorValue = new Color(1.0f, 0.82f, 0.28f);
                    so.FindProperty("_minIntensity").floatValue = 0.12f;
                    so.FindProperty("_maxIntensity").floatValue = 0.40f;
                    so.FindProperty("_pulseSpeed").floatValue = 2.0f;
                    so.FindProperty("_hoverMultiplier").floatValue = 1.35f;
                    so.FindProperty("_useSubtleLightHalo").boolValue = true;
                    so.FindProperty("_haloRange").floatValue = 0.6f;
                    so.FindProperty("_haloIntensity").floatValue = 0.20f;
                    so.FindProperty("_onlyWhenUnlockable").boolValue = false;
                    break;

                case GlowCategory.UnlockableDoor:
                    so.FindProperty("_glowColor").colorValue = new Color(0.10f, 0.55f, 0.38f);
                    so.FindProperty("_minIntensity").floatValue = 0.02f;
                    so.FindProperty("_maxIntensity").floatValue = 0.10f;
                    so.FindProperty("_pulseSpeed").floatValue = 1.6f;
                    so.FindProperty("_hoverMultiplier").floatValue = 1.15f;
                    so.FindProperty("_useSubtleLightHalo").boolValue = false;
                    so.FindProperty("_onlyWhenUnlockable").boolValue = true;
                    break;
            }

            // Populate renderers
            var renderers = glow.GetComponentsInChildren<Renderer>(true);
            var propRenderers = so.FindProperty("_targetRenderers");
            propRenderers.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                propRenderers.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                if (renderers[i] != null)
                {
                    foreach (var mat in renderers[i].sharedMaterials)
                    {
                        if (mat != null)
                        {
                            mat.EnableKeyword("_EMISSION");
                            EditorUtility.SetDirty(mat);
                        }
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(glow);
        }

        private static GameObject FindGameObjectByName(string name)
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
