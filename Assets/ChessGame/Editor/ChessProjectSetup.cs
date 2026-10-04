using System.Collections.Generic;
using Chess.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chess.EditorTools
{
    /// <summary>
    /// One-shot project setup. Each step is also a menu item so the project can be re-generated
    /// by hand, and <see cref="SetupAll"/> is the batch-mode entry point.
    /// </summary>
    public static class ChessProjectSetup
    {
        public const string ScenePath = "Assets/ChessGame/Scenes/Chess.unity";

        [MenuItem("Chess/Import TMP Essentials", priority = 10)]
        public static void ImportTextMeshProResources()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[ChessSetup] TMP essential resources already present.");
                return;
            }

            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();

            Debug.Log(AssetDatabase.IsValidFolder("Assets/TextMesh Pro")
                ? "[ChessSetup] Imported TMP essential resources."
                : "[ChessSetup] TMP import was requested but no assets landed yet.");
        }

        [MenuItem("Chess/Build Chess Scene", priority = 20)]
        public static void BuildChessScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x23, 0x20, 0x1E, 0xFF);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.8f, -7.6f), Quaternion.Euler(52f, 0f, 0f));

            var keyLightObject = new GameObject("Key Light");
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.color = new Color(1f, 0.97f, 0.92f);
            keyLightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            // A dim, shadowless opposite light keeps the dark pieces from going to silhouette.
            var fillLightObject = new GameObject("Fill Light");
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.35f;
            fillLight.shadows = LightShadows.None;
            fillLight.color = new Color(0.82f, 0.88f, 1f);
            fillLightObject.transform.rotation = Quaternion.Euler(28f, 152f, 0f);

            var gameRoot = new GameObject("Game");
            gameRoot.AddComponent<GameManager>();

            string folder = System.IO.Path.GetDirectoryName(ScenePath);
            if (!AssetDatabase.IsValidFolder(folder))
                System.IO.Directory.CreateDirectory(folder);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            RegisterScene();
            Debug.Log("[ChessSetup] Built scene at " + ScenePath);
        }

        /// <summary>Makes the chess scene the one a build starts from.</summary>
        private static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath && existing.path != "Assets/Scenes/SampleScene.unity")
                    scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Every material in this game is built in code, so no asset references the shaders and
        /// the build strips them. Shader.Find then returns null in the player while still working
        /// in the editor, which is exactly the kind of break no editor test can see. Listing them
        /// under Always Included Shaders keeps them in the build and findable.
        /// </summary>
        [MenuItem("Chess/Include Runtime Shaders In Builds", priority = 25)]
        public static void EnsureRuntimeShadersIncluded()
        {
            string[] required =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit"
            };

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[ChessSetup] Could not open GraphicsSettings.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogError("[ChessSetup] GraphicsSettings has no m_AlwaysIncludedShaders property.");
                return;
            }

            int added = 0;

            foreach (string name in required)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogError("[ChessSetup] Shader not found in the project: " + name);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }

                if (present) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added++;
            }

            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log("[ChessSetup] Always Included Shaders now holds " + list.arraySize + " entries (" + added + " added).");
        }

        /// <summary>
        /// Prints what the project is actually configured for. Run after a platform switch:
        /// the shader list is per-project but stripping is decided per-target, so this is the
        /// cheapest way to confirm both survived the switch.
        /// </summary>
        public static void ReportSetupState()
        {
            Debug.Log("[ChessSetup] Active build target: " + EditorUserBuildSettings.activeBuildTarget
                      + " (group " + EditorUserBuildSettings.selectedBuildTargetGroup + ")");
            Debug.Log("[ChessSetup] Scenes in build: " + EditorBuildSettings.scenes.Length
                      + ", first = " + (EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "none"));

            EnsureRuntimeShadersIncluded();
        }

        [MenuItem("Chess/Configure Mobile Player Settings", priority = 30)]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Bravestars";
            PlayerSettings.productName = "Chess 3D";

            // Portrait first, but landscape stays available: the camera rig reframes either way.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.bravestars.chess3d");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.bravestars.chess3d");

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.runInBackground = false;
            QualitySettings.vSyncCount = 1;

            AssetDatabase.SaveAssets();
            Debug.Log("[ChessSetup] Player settings configured for mobile.");
        }

        [MenuItem("Chess/Run Full Setup", priority = 40)]
        public static void SetupAll()
        {
            ImportTextMeshProResources();
            EnsureRuntimeShadersIncluded();
            ConfigurePlayerSettings();
            BuildChessScene();
        }

        /// <summary>
        /// Batch-mode entry point. TMP resources land during an asset import, so the scene is
        /// built by a second invocation once the domain has reloaded.
        /// </summary>
        public static void BatchImportResources()
        {
            ImportTextMeshProResources();
            EnsureRuntimeShadersIncluded();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
        }

        public static void BatchBuildScene()
        {
            BuildChessScene();
            AssetDatabase.SaveAssets();
        }
    }
}
