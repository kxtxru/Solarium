using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralAudio3D : MonoBehaviour
    {
        private readonly Dictionary<FeedbackKind3D, AudioClip> clips = new();
        private AudioSource effects;
        private AudioSource ambience;

        private void Awake()
        {
            effects = GetComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0f;
            effects.volume = 0.55f;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.spatialBlend = 0f;
            ambience.volume = 0.08f;
            ambience.clip = CreateNoise("Solarium Wind", 4f, 0.025f);
            ambience.Play();

            clips[FeedbackKind3D.Food] = CreateTone("Food", 620f, 880f, 0.12f);
            clips[FeedbackKind3D.GoldenFood] = CreateTone("Golden Food", 760f, 1320f, 0.2f);
            clips[FeedbackKind3D.Healing] = CreateTone("Healing", 420f, 700f, 0.25f);
            clips[FeedbackKind3D.SupplyPickup] = CreateTone("Supply", 520f, 640f, 0.12f);
            clips[FeedbackKind3D.SupplyDeposit] = CreateTone("Deposit", 480f, 960f, 0.22f);
            clips[FeedbackKind3D.ReserveUse] = CreateTone("Reserve Use", 520f, 920f, 0.18f);
            clips[FeedbackKind3D.Damage] = CreateTone("Damage", 170f, 90f, 0.13f);
            clips[FeedbackKind3D.Death] = CreateTone("Death", 220f, 55f, 0.4f);
        }

        public void Play(FeedbackKind3D kind)
        {
            if (clips.TryGetValue(kind, out AudioClip clip) && clip != null)
                effects.PlayOneShot(clip);
        }

        private static AudioClip CreateTone(string name, float startFrequency, float endFrequency, float seconds)
        {
            const int sampleRate = 22050;
            int length = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var samples = new float[length];
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Mathf.Max(1, length - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Sin(Mathf.PI * t) * (1f - t * 0.35f);
                samples[i] = Mathf.Sin(phase) * envelope * 0.32f;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoise(string name, float seconds, float amplitude)
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(seconds * sampleRate);
            var samples = new float[length];
            var random = new System.Random(7319);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, 0.015f);
                samples[i] = filtered * amplitude;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
