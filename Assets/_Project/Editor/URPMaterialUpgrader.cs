using System.IO;
using UnityEditor;
using UnityEngine;

namespace LateSubmission.Editor
{
    /// <summary>
    /// Upgrades all Built-in / Standard materials in the project to Universal Render Pipeline (URP) Lit shaders,
    /// fixing pink / magenta rendering issues across all imported asset packs.
    /// </summary>
    [InitializeOnLoad]
    public static class URPMaterialUpgrader
    {
        static URPMaterialUpgrader()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("URP_Materials_Auto_Upgraded", false))
                {
                    SessionState.SetBool("URP_Materials_Auto_Upgraded", true);
                    UpgradeAllMaterials();
                }
            };
        }

        [MenuItem("Tools/Late Submission/Fix All Pink Materials (Convert to URP Lit)")]
        public static void UpgradeAllMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[URPMaterialUpgrader] Could not find 'Universal Render Pipeline/Lit' shader!");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            int upgradedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in matGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    // Skip internal Unity packages / editor resources
                    if (path.StartsWith("Packages/") && !path.StartsWith("Packages/com.unity")) continue;

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null || mat.shader == null) continue;

                    string shaderName = mat.shader.name;

                    // If material is already URP Lit, Simple Lit, Unlit, or Particle, skip
                    if (shaderName.StartsWith("Universal Render Pipeline/") ||
                        shaderName.StartsWith("TextMeshPro/") ||
                        shaderName.StartsWith("UI/") ||
                        shaderName.StartsWith("Skybox/") ||
                        shaderName == "Hidden/Universal Render Pipeline/Lit")
                    {
                        continue;
                    }

                    // Upgrade Built-in Standard, Specular, Mobile, Legacy, or Autodesk shaders
                    if (shaderName.Contains("Standard") ||
                        shaderName.Contains("Legacy") ||
                        shaderName.Contains("Mobile") ||
                        shaderName.Contains("Bumped") ||
                        shaderName.Contains("Diffuse") ||
                        shaderName.Contains("Specular") ||
                        shaderName.Contains("Autodesk") ||
                        shaderName == "Hidden/InternalErrorShader")
                    {
                        UpgradeMaterialToURP(mat, urpLit);
                        EditorUtility.SetDirty(mat);
                        upgradedCount++;
                    }
                }

                // Also check BrazilianDoors prefabs to point to Doors_mat_URP
                FixBrazilianDoorsPrefabs();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[URPMaterialUpgrader] Successfully upgraded {upgradedCount} materials to Universal Render Pipeline/Lit!</color>");
        }

        private static void UpgradeMaterialToURP(Material mat, Shader urpLit)
        {
            // Extract textures and properties from old shader
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            if (mainTex == null && mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

            Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");

            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            if (mat.HasProperty("_Smoothness")) smoothness = mat.GetFloat("_Smoothness");

            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            // Switch to URP Lit
            mat.shader = urpLit;

            // Re-assign to URP property names
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", baseColor);

            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metallicMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (occlusionMap != null)
            {
                mat.SetTexture("_OcclusionMap", occlusionMap);
            }

            if (emissionMap != null || emissionColor.maxColorComponent > 0.01f)
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
            }
        }

        private static void FixBrazilianDoorsPrefabs()
        {
            string urpMatPath = "Assets/Imported Assets/BrazilianDoors v1/Materials/Doors_mat_URP.mat";
            Material urpMat = AssetDatabase.LoadAssetAtPath<Material>(urpMatPath);
            if (urpMat == null) return;

            string[] doorPrefabs = new string[]
            {
                "Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_1.prefab",
                "Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_2.prefab",
                "Assets/Imported Assets/BrazilianDoors v1/Prefabs/door_3.prefab"
            };

            foreach (string prefabPath in doorPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                bool modified = false;

                foreach (var renderer in renderers)
                {
                    Material[] sharedMats = renderer.sharedMaterials;
                    for (int i = 0; i < sharedMats.Length; i++)
                    {
                        if (sharedMats[i] != null && sharedMats[i].name.Contains("Doors_mat"))
                        {
                            sharedMats[i] = urpMat;
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        renderer.sharedMaterials = sharedMats;
                    }
                }

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }
        }
    }
}
