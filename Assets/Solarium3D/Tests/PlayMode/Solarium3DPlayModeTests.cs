using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Solarium.ThreeD.Tests
{
    public sealed class Solarium3DPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrainingSceneHasSixIndependentCompatibleAgents()
        {
            yield return SceneManager.LoadSceneAsync("SolariumTraining3D", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
            SolariumEnvironment3D[] environments = Object.FindObjectsByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environments.Any(item => item.EpisodeNumber == 0); i++)
                yield return null;
            foreach (SolariumEnvironment3D item in environments)
            {
                if (item.EpisodeNumber == 0)
                    item.Agent.OnEpisodeBegin();
            }
            Assert.That(environments.Length, Is.EqualTo(6));
            Assert.That(environments.All(item => item.Agent != null), Is.True);
            Assert.That(environments.All(item => item.Agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize == 194), Is.True);

            SolariumEnvironment3D target = environments[0];
            int targetEpisode = target.EpisodeNumber;
            int[] others = environments.Skip(1).Select(item => item.EpisodeNumber).ToArray();
            target.Agent.Vitals.Kill("health_depleted");
            yield return null;

            Assert.That(target.LastTerminationReason, Is.EqualTo("health_depleted"));
            Assert.That(environments.Skip(1).Select(item => item.EpisodeNumber), Is.EqualTo(others));
            if (target.EpisodeNumber == targetEpisode)
                target.Agent.OnEpisodeBegin();
            Assert.That(target.EpisodeNumber, Is.GreaterThan(targetEpisode));
            Assert.That(environments.Skip(1).Select(item => item.EpisodeNumber), Is.EqualTo(others));
        }

        [UnityTest]
        public IEnumerator EpisodeCanRunPastOneHundredTwentySeconds()
        {
            yield return SceneManager.LoadSceneAsync("SolariumTraining3D", LoadSceneMode.Single);
            yield return null;
            SolariumEnvironment3D environment = Object.FindAnyObjectByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environment.EpisodeNumber == 0; i++)
                yield return null;
            Assert.That(environment.EpisodeNumber, Is.GreaterThan(0));
            int episode = environment.EpisodeNumber;
            for (int i = 0; i < 6100; i++)
                environment.OnAgentFixedStep(0f, false, 0f);
            Assert.That(environment.SurvivalTime, Is.GreaterThan(120f));
            Assert.That(environment.EpisodeNumber, Is.EqualTo(episode));
        }

        [UnityTest]
        public IEnumerator SurvivalSceneLoadsCompatibleOnnx()
        {
            yield return SceneManager.LoadSceneAsync("SolariumSurvival3D", LoadSceneMode.Single);
            yield return null;
            SolariumEnvironment3D environment = Object.FindAnyObjectByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environment.EpisodeNumber == 0; i++)
                yield return null;
            BehaviorParameters behavior = environment.Agent.GetComponent<BehaviorParameters>();
            Assert.That(behavior.BrainParameters.VectorObservationSize, Is.EqualTo(194));
            Assert.That(behavior.BrainParameters.ActionSpec.NumContinuousActions, Is.EqualTo(2));
            Assert.That(behavior.BrainParameters.ActionSpec.BranchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(behavior.Model, Is.Not.Null);
            Assert.That(behavior.Model.name, Is.EqualTo("SolariumPersistentMemoryV3"));
            Assert.That(behavior.BehaviorType, Is.EqualTo(BehaviorType.InferenceOnly));

            if (!environment.IsEpisodeActive)
            {
                environment.Agent.OnEpisodeBegin();
                yield return null;
            }
            int episode = environment.EpisodeNumber;
            for (int i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(environment.EpisodeNumber, Is.EqualTo(episode));
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(environment.Agent.isActiveAndEnabled, Is.True);
            Assert.That(environment.Agent.Vitals.IsAlive, Is.True);
            Assert.That(environment.SurvivalTime, Is.GreaterThan(0.1f));
        }
    }
}
