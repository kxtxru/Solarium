using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Solarium.Editor
{
    [InitializeOnLoad]
    public static class SolariumProjectSetup
    {
        private const string Root = "Assets/Solarium";
        private const string SettingsPath = Root + "/Data/SolariumTrainingSettings.asset";
        private const string TrainingScenePath = Root + "/Scenes/SolariumTraining.unity";
        private const string DemoScenePath = Root + "/Scenes/SolariumDemo.unity";
        private const string SurvivalScenePath = Root + "/Scenes/SolariumSurvival.unity";
        private const string TrainedModelPath = Root + "/Models/SolariumPersistentMemoryV3.onnx";

        static SolariumProjectSetup()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        [MenuItem("Tools/Solarium/Create or Repair Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            SolariumTrainingSettings settings = GetOrCreateSettings();
            CreateTrainingScene(settings);
            CreateDemoScene(settings);
            CreateSurvivalScene(settings);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Solarium setup complete.\nTraining: {TrainingScenePath}\nDemo: {DemoScenePath}");
        }

        [MenuItem("Tools/Solarium/Open Training Scene")]
        public static void OpenTrainingScene()
        {
            if (!File.Exists(TrainingScenePath))
                BuildAll();
            EditorSceneManager.OpenScene(TrainingScenePath);
        }

        [MenuItem("Tools/Solarium/Create or Repair Training (6 Arenas)")]
        public static void CreateOrRepairTrainingScene()
        {
            EnsureFolders();
            CreateTrainingScene(GetOrCreateSettings());
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Solarium training scene created with 6 arenas at {TrainingScenePath}");
        }

        [MenuItem("Tools/Solarium/Open Demo Scene")]
        public static void OpenDemoScene()
        {
            if (!File.Exists(DemoScenePath))
                BuildAll();
            EditorSceneManager.OpenScene(DemoScenePath);
        }

        [MenuItem("Tools/Solarium/Open Survival Showcase")]
        public static void OpenSurvivalScene()
        {
            EnsureFolders();
            SolariumTrainingSettings settings = GetOrCreateSettings();
            if (!File.Exists(SurvivalScenePath))
            {
                CreateSurvivalScene(settings);
                ConfigureBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            EditorSceneManager.OpenScene(SurvivalScenePath);
        }

        [MenuItem("Tools/Solarium/Create or Repair Survival Showcase")]
        public static void CreateOrRepairSurvivalScene()
        {
            EnsureFolders();
            CreateSurvivalScene(GetOrCreateSettings());
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Solarium survival showcase created at {SurvivalScenePath}");
        }

        [MenuItem("Tools/Solarium/Build Windows Training Player")]
        public static void BuildTrainingPlayer()
        {
            if (!File.Exists(TrainingScenePath))
                BuildAll();
            const string output = "Builds/SolariumTraining/SolariumTraining.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReportUtility.Build(
                new[] { TrainingScenePath },
                output,
                BuildTarget.StandaloneWindows64);
        }

        public static void CreateForBatchMode()
        {
            BuildAll();
            EditorApplication.Exit(0);
        }

        private static void CreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                )
                return;

            if (!File.Exists(TrainingScenePath) || !File.Exists(DemoScenePath))
            {
                BuildAll();
                return;
            }

            // Add the showcase without recreating scenes the user may have edited.
            if (!File.Exists(SurvivalScenePath))
            {
                EnsureFolders();
                CreateSurvivalScene(GetOrCreateSettings());
                ConfigureBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static SolariumTrainingSettings GetOrCreateSettings()
        {
            SolariumTrainingSettings settings =
                AssetDatabase.LoadAssetAtPath<SolariumTrainingSettings>(SettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<SolariumTrainingSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void CreateTrainingScene(SolariumTrainingSettings settings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Training Runtime").AddComponent<TrainingRuntimeSettings>();
            Vector2[] positions =
            {
                new(-24f, -10f),
                new(0f, -10f),
                new(24f, -10f),
                new(-24f, 10f),
                new(0f, 10f),
                new(24f, 10f)
            };
            for (int i = 0; i < positions.Length; i++)
                CreateEnvironment($"TrainingArena_{i + 1}", positions[i], settings, false, i, false);
            EditorSceneManager.SaveScene(scene, TrainingScenePath);
        }

        private static void CreateDemoScene(SolariumTrainingSettings settings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.075f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SolariumEnvironment environment =
                CreateEnvironment("DemoArena", Vector2.zero, settings, true, 0, true);
            var ui = new GameObject("Solarium UI");
            ui.AddComponent<SolariumHUD>().Initialize(environment);
            ui.AddComponent<TrainingGraph>().Initialize(environment);
            EditorSceneManager.SaveScene(scene, DemoScenePath);
        }

        private static void CreateSurvivalScene(SolariumTrainingSettings settings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.027f, 0.05f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SolariumEnvironment environment =
                CreateEnvironment("SurvivalArena", Vector2.zero, settings, true, 0, false);
            // Awake has not run while the scene is being assembled in edit mode,
            // so use the component directly instead of the runtime convenience property.
            DifficultyController difficulty = environment.GetComponent<DifficultyController>();
            difficulty.ManualDifficulty = 1f;
            difficulty.ArenaSizeMultiplier = 1.4f;
            cameraObject.AddComponent<CameraFollow2D>().Initialize(environment.Agent.transform);

            BehaviorParameters behavior = environment.Agent.GetComponent<BehaviorParameters>();
            ModelAsset model = AssetDatabase.LoadAssetAtPath<ModelAsset>(TrainedModelPath);
            if (model == null)
            {
                behavior.BehaviorType = BehaviorType.HeuristicOnly;
                Debug.LogWarning(
                    $"No persistent-memory-v3 model found at {TrainedModelPath}. Train Solarium v3, import its ONNX and repair this scene.");
            }
            else
            {
                behavior.Model = model;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }

            var ui = new GameObject("Survival UI");
            ui.AddComponent<SolariumHUD>().Initialize(environment);
            ui.AddComponent<TrainingGraph>().Initialize(environment);
            EditorSceneManager.SaveScene(scene, SurvivalScenePath);
        }

        private static SolariumEnvironment CreateEnvironment(
            string name,
            Vector2 position,
            SolariumTrainingSettings settings,
            bool manualDifficulty,
            int index,
            bool heuristic)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<ProceduralSpawner>();
            DifficultyController difficulty = root.AddComponent<DifficultyController>();
            difficulty.UseManualDifficulty = manualDifficulty;
            difficulty.ManualDifficulty = manualDifficulty ? 0.45f : 0f;
            root.AddComponent<EpisodeStatistics>();
            SolariumEnvironment environment = root.AddComponent<SolariumEnvironment>();

            var sol = new GameObject("Sol");
            sol.transform.SetParent(root.transform, false);
            sol.transform.localScale = Vector3.one * 0.8f;
            sol.AddComponent<SpriteRenderer>();
            sol.AddComponent<SimpleShapeRenderer>().Configure(
                SimpleShape.Circle,
                new Color(1f, 0.88f, 0.2f),
                3);
            var body = sol.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = sol.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            sol.AddComponent<SolVitals>();
            sol.AddComponent<SolObservations>();
            SolAgent agent = sol.AddComponent<SolAgent>();
            sol.AddComponent<AgentDebugView>();

            BehaviorParameters behavior = sol.GetComponent<BehaviorParameters>();
            if (behavior == null)
                behavior = sol.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = "Solarium";
            behavior.BrainParameters.VectorObservationSize =
                SolObservations.ObservationSizeFor(settings.rayCount);
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 2, 2 });
            behavior.BrainParameters.VectorActionDescriptions =
                new[] { "move_x", "move_y", "sprint", "interact" };
            behavior.BehaviorType = heuristic
                ? BehaviorType.HeuristicOnly
                : BehaviorType.Default;

            DecisionRequester requester = sol.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5;
            requester.DecisionStep = index % 5;
            requester.TakeActionsBetweenDecisions = true;
            agent.MaxStep = 0;
            environment.Configure(settings, agent, 12345 + index * 100003, false);
            return environment;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TrainingScenePath, true),
                new EditorBuildSettingsScene(DemoScenePath, true)
            };
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root,
                Root + "/Data",
                Root + "/Scenes",
                Root + "/Prefabs",
                Root + "/Materials",
                Root + "/Models",
                Root + "/Sprites",
                Root + "/Training"
            };
            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string child = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }

    internal static class BuildReportUtility
    {
        public static void Build(string[] scenes, string output, BuildTarget target)
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Solarium build failed: {report.summary.result}");
            Debug.Log($"Training player created at {Path.GetFullPath(output)}");
        }
    }
}
