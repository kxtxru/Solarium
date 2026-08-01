using System.IO;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Solarium.ThreeD.Editor
{
    public static class Solarium3DProjectSetup
    {
        private const string Root = "Assets/Solarium3D";
        private const string TrainingScenePath = Root + "/Scenes/SolariumTraining3D.unity";
        private const string SurvivalScenePath = Root + "/Scenes/SolariumSurvival3D.unity";
        private const string PalettePath = Root + "/Data/SolariumPalette3D.asset";
        private const string VolumePath = Root + "/Data/SolariumVolume3D.asset";
        private const string SettingsPath = "Assets/Solarium/Data/SolariumTrainingSettings.asset";
        private const string ModelPath = Root + "/Models/SolariumPersistentMemoryV3.onnx";

        [MenuItem("Tools/Solarium 3D/Create or Repair Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            SolariumPalette3D palette = GetOrCreatePalette();
            SolariumTrainingSettings settings = AssetDatabase.LoadAssetAtPath<SolariumTrainingSettings>(SettingsPath);
            if (settings == null)
                throw new FileNotFoundException("Solarium training settings are missing.", SettingsPath);
            CreateTrainingScene(settings, palette);
            CreateSurvivalScene(settings, palette);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Solarium 3D ready.\nTraining: {TrainingScenePath}\nSurvival: {SurvivalScenePath}");
        }

        [MenuItem("Tools/Solarium 3D/Open Training Scene")]
        public static void OpenTrainingScene()
        {
            if (!File.Exists(TrainingScenePath))
                BuildAll();
            EditorSceneManager.OpenScene(TrainingScenePath);
        }

        [MenuItem("Tools/Solarium 3D/Open Survival Scene")]
        public static void OpenSurvivalScene()
        {
            if (!File.Exists(SurvivalScenePath))
                BuildAll();
            EditorSceneManager.OpenScene(SurvivalScenePath);
        }

        [MenuItem("Tools/Solarium 3D/Build Windows Training Player")]
        public static void BuildTrainingPlayer()
        {
            BuildAll();
            BuildPlayer(new[] { TrainingScenePath }, "Builds/SolariumTraining3D/SolariumTraining3D.exe");
        }

        [MenuItem("Tools/Solarium 3D/Build Windows Showcase")]
        public static void BuildShowcasePlayer()
        {
            BuildAll();
            BuildPlayer(new[] { SurvivalScenePath }, "Builds/Solarium3D/Solarium3D.exe");
        }

        public static void CreateForBatchMode()
        {
            BuildAll();
            EditorApplication.Exit(0);
        }

        public static void BuildTrainingForBatchMode()
        {
            BuildTrainingPlayer();
            EditorApplication.Exit(0);
        }

        private static void CreateTrainingScene(SolariumTrainingSettings settings, SolariumPalette3D palette)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Training Runtime").AddComponent<TrainingRuntimeSettings>();
            Vector2[] positions =
            {
                new(-28f, -11f), new(0f, -11f), new(28f, -11f),
                new(-28f, 11f), new(0f, 11f), new(28f, 11f)
            };
            for (int i = 0; i < positions.Length; i++)
                CreateEnvironment($"TrainingArena3D_{i + 1}", positions[i], settings, palette, false, i, false);
            EditorSceneManager.SaveScene(scene, TrainingScenePath);
        }

        private static void CreateSurvivalScene(SolariumTrainingSettings settings, SolariumPalette3D palette)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironmentLighting();
            CreateLightingAndVolume();

            SolariumEnvironment3D environment =
                CreateEnvironment("SurvivalArena3D", Vector2.zero, settings, palette, true, 0, true);
            DifficultyController difficulty = environment.GetComponent<DifficultyController>();
            difficulty.ManualDifficulty = 1f;
            difficulty.ArenaSizeMultiplier = 1.4f;

            BehaviorParameters behavior = environment.Agent.GetComponent<BehaviorParameters>();
            ModelAsset model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelPath);
            if (model != null)
            {
                behavior.Model = model;
                behavior.InferenceDevice = InferenceDevice.Default;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }
            else
            {
                behavior.BehaviorType = BehaviorType.HeuristicOnly;
                Debug.LogWarning($"No compatible ONNX found at {ModelPath}.");
            }

            Camera showcaseCamera = CreateCamera(environment);
            CreateFeedback(environment, palette);
            CreateHud(environment, showcaseCamera);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, SurvivalScenePath);
        }

        private static SolariumEnvironment3D CreateEnvironment(
            string name,
            Vector2 position,
            SolariumTrainingSettings settings,
            SolariumPalette3D palette,
            bool manualDifficulty,
            int index,
            bool visuals)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            root.transform.position = Planar3D.ToWorld(position);
            root.AddComponent<WorldObjectPool3D>();
            root.AddComponent<ProceduralSpawner3D>();
            DifficultyController difficulty = root.AddComponent<DifficultyController>();
            difficulty.UseManualDifficulty = manualDifficulty;
            difficulty.ManualDifficulty = manualDifficulty ? 1f : 0f;
            root.AddComponent<EpisodeStatistics>();
            SolariumEnvironment3D environment = root.AddComponent<SolariumEnvironment3D>();

            var sol = new GameObject("Sol");
            sol.layer = SolariumLayers3D.Gameplay;
            sol.transform.SetParent(root.transform, false);
            var body = sol.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            var collider = sol.AddComponent<CapsuleCollider>();
            collider.radius = 0.42f;
            collider.height = 1.1f;
            sol.AddComponent<SolVitals>();
            sol.AddComponent<SolObservations3D>();
            SolAgent3D agent = sol.AddComponent<SolAgent3D>();
            if (visuals)
            {
                Transform visual = LowPolyFactory3D.BuildSol(sol.transform, palette);
                sol.AddComponent<PlanarMotionVisual3D>().Configure(visual, 0.05f, true);
            }

            BehaviorParameters behavior = sol.GetComponent<BehaviorParameters>() ?? sol.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = "Solarium";
            behavior.BrainParameters.VectorObservationSize = SolObservations3D.ObservationSizeFor(settings.rayCount);
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 2, 2 });
            behavior.BrainParameters.VectorActionDescriptions = new[] { "move_x", "move_z", "sprint", "interact" };
            behavior.BehaviorType = BehaviorType.Default;

            DecisionRequester requester = sol.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5;
            requester.DecisionStep = index % 5;
            requester.TakeActionsBetweenDecisions = true;
            agent.MaxStep = 0;
            environment.Configure(settings, palette, agent, 12345 + index * 100003, false, visuals);
            return environment;
        }

        private static Camera CreateCamera(SolariumEnvironment3D environment)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 160f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.19f, 0.23f);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            go.AddComponent<IsometricCamera3D>().Initialize(environment, environment.Agent.transform);
            go.AddComponent<AutomatedShowcaseCapture3D>();
            return camera;
        }

        private static void CreateLightingAndVolume()
        {
            var sun = new GameObject("Warm Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.84f, 0.62f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);
            ColorAdjustments color = GetOrAddVolumeComponent<ColorAdjustments>(profile);
            color.postExposure.Override(0.12f);
            color.contrast.Override(12f);
            color.saturation.Override(2f);
            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.42f);

            var volumeObject = new GameObject("Solarium Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;
            T created = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(created, profile);
            EditorUtility.SetDirty(profile);
            return created;
        }

        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.46f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.28f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.1f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.17f, 0.25f, 0.25f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 80f;
        }

        private static void CreateFeedback(SolariumEnvironment3D environment, SolariumPalette3D palette)
        {
            var go = new GameObject("World Feedback");
            go.AddComponent<AudioSource>();
            go.AddComponent<ProceduralAudio3D>();
            go.AddComponent<FeedbackController3D>().Initialize(environment, palette);
        }

        private static void CreateHud(SolariumEnvironment3D environment, Camera showcaseCamera)
        {
            var canvasObject = new GameObject("Solarium 3D HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = showcaseCamera;
            canvas.planeDistance = 2f;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform panel = CreatePanel(canvasObject.transform, "Status Panel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -290f), new Vector2(520f, -34f),
                new Color(0.035f, 0.075f, 0.075f, 0.9f));
            Text status = CreateText(panel, "Status", "SOL", 28, TextAnchor.UpperLeft,
                new Vector2(24f, -70f), new Vector2(-24f, -18f));
            Image health = CreateBar(panel, "Health", new Vector2(24f, -112f), new Vector2(-24f, -86f),
                new Color(0.95f, 0.25f, 0.18f));
            Image energy = CreateBar(panel, "Energy", new Vector2(24f, -150f), new Vector2(-24f, -124f),
                new Color(0.98f, 0.78f, 0.16f));
            Text metrics = CreateText(panel, "Metrics", string.Empty, 21, TextAnchor.UpperLeft,
                new Vector2(24f, -236f), new Vector2(-24f, -166f));

            RectTransform controls = CreatePanel(canvasObject.transform, "Controls",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-500f, 28f), new Vector2(-28f, 112f),
                new Color(0.035f, 0.075f, 0.075f, 0.88f));
            Button pause = CreateButton(controls, "Pause", "PAUSA", new Vector2(16f, 16f), new Vector2(142f, -16f), out Text pauseText);
            Button speed1 = CreateButton(controls, "Speed 1", "1×", new Vector2(156f, 16f), new Vector2(244f, -16f), out _);
            Button speed2 = CreateButton(controls, "Speed 2", "2×", new Vector2(258f, 16f), new Vector2(346f, -16f), out _);
            Button speed5 = CreateButton(controls, "Speed 5", "5×", new Vector2(360f, 16f), new Vector2(448f, -16f), out _);

            var transitionObject = new GameObject("Episode Transition", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            transitionObject.transform.SetParent(canvasObject.transform, false);
            RectTransform transitionRect = (RectTransform)transitionObject.transform;
            transitionRect.anchorMin = Vector2.zero;
            transitionRect.anchorMax = Vector2.one;
            transitionRect.offsetMin = Vector2.zero;
            transitionRect.offsetMax = Vector2.zero;
            transitionObject.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.035f, 0.75f);
            CanvasGroup transition = transitionObject.GetComponent<CanvasGroup>();
            transition.blocksRaycasts = false;
            Text death = CreateText(transitionRect, "Death Reason", string.Empty, 42, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(0f, 0f), true);

            canvasObject.AddComponent<SolariumHUD3D>().Initialize(
                environment, health, energy, status, metrics, death, pauseText,
                pause, speed1, speed2, speed5, transition);
        }

        private static RectTransform CreatePanel(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(
            Transform parent, string name, string value, int size, TextAnchor alignment,
            Vector2 offsetMin, Vector2 offsetMax, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = stretch ? Vector2.zero : new Vector2(0f, 1f);
            rect.anchorMax = stretch ? Vector2.one : new Vector2(1f, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.97f, 0.9f);
            text.text = value;
            return text;
        }

        private static Image CreateBar(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax, Color fillColor)
        {
            RectTransform background = CreatePanel(parent, name + " Background", new Vector2(0f, 1f), new Vector2(1f, 1f),
                offsetMin, offsetMax, new Color(0f, 0f, 0f, 0.45f));
            var fillObject = new GameObject(name + " Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(background, false);
            RectTransform rect = (RectTransform)fillObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            Image image = fillObject.GetComponent<Image>();
            image.color = fillColor;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            return image;
        }

        private static Button CreateButton(
            Transform parent, string name, string label, Vector2 offsetMin, Vector2 offsetMax, out Text text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = new Color(0.12f, 0.32f, 0.27f, 0.96f);
            text = CreateText(go.transform, "Label", label, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, true);
            return go.GetComponent<Button>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static SolariumPalette3D GetOrCreatePalette()
        {
            SolariumPalette3D palette = AssetDatabase.LoadAssetAtPath<SolariumPalette3D>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<SolariumPalette3D>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            Color[] colors =
            {
                new(0.13f, 0.28f, 0.20f), new(0.31f, 0.40f, 0.26f), new(0.25f, 0.31f, 0.24f),
                new(1f, 0.60f, 0.08f), new(1f, 0.86f, 0.42f), new(0.16f, 0.72f, 0.27f),
                new(0.95f, 0.55f, 0.05f), new(0.08f, 0.58f, 0.95f), new(0.72f, 0.12f, 0.78f),
                new(0.08f, 0.72f, 0.56f), new(0.75f, 0.07f, 0.04f), new(0.92f, 0.28f, 0.04f),
                new(0.20f, 0.10f, 0.035f), new(0.95f, 0.12f, 0.02f), new(0.40f, 0.04f, 0.52f),
                new(0.12f, 0.48f, 0.20f), new(0.28f, 0.36f, 0.35f)
            };
            var materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                VisualKind3D kind = (VisualKind3D)i;
                bool emissive = kind is VisualKind3D.Food or VisualKind3D.GoldenFood
                    or VisualKind3D.Healing or VisualKind3D.Supply or VisualKind3D.Shelter or VisualKind3D.Lava;
                materials[i] = GetOrCreateMaterial(kind.ToString(), colors[i], emissive);
            }
            palette.Configure(materials);
            EditorUtility.SetDirty(palette);
            return palette;
        }

        private static Material GetOrCreateMaterial(string name, Color color, bool emissive)
        {
            string path = $"{Root}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.65f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureBuildSettings()
        {
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SurvivalScenePath, true),
                new EditorBuildSettingsScene(TrainingScenePath, true)
            };
        }

        private static void EnsureFolders()
        {
            string[] folders = { Root, Root + "/Data", Root + "/Editor", Root + "/Materials", Root + "/Scenes", Root + "/Scripts", Root + "/Tests", Root + "/Training" };
            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        private static void BuildPlayer(string[] scenes, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Solarium 3D build failed: {report.summary.result}");
            Debug.Log($"Solarium 3D player created at {Path.GetFullPath(output)}");
        }
    }
}
