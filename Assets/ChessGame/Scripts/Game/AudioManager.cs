using System;
using UnityEngine;

namespace Chess.Game
{
    /// <summary>
    /// Synthesises every sound effect at startup, so the project ships no audio files. Each clip
    /// is a short tone or noise burst shaped by an attack/decay envelope.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;

        private AudioClip _move;
        private AudioClip _capture;
        private AudioClip _castle;
        private AudioClip _check;
        private AudioClip _promote;
        private AudioClip _win;
        private AudioClip _lose;
        private AudioClip _draw;
        private AudioClip _click;
        private AudioClip _illegal;

        public void Build()
        {
            _source = gameObject.GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _move = Synthesise("MoveSfx", 0.10f, t => Envelope(t, 0.10f, 0.002f) * (Tone(t, 190f) * 0.45f + Noise(t) * 0.35f));
            _capture = Synthesise("CaptureSfx", 0.18f, t => Envelope(t, 0.18f, 0.003f) * (Tone(t, 110f) * 0.55f + Noise(t) * 0.45f));
            _castle = Synthesise("CastleSfx", 0.26f, t =>
                Envelope(t, 0.09f, 0.002f) * (Tone(t, 190f) * 0.4f + Noise(t) * 0.3f) +
                Envelope(t - 0.13f, 0.09f, 0.002f) * (Tone(t, 165f) * 0.4f + Noise(t) * 0.3f));
            _check = Synthesise("CheckSfx", 0.34f, t =>
                Envelope(t, 0.16f, 0.004f) * Tone(t, 880f) * 0.5f +
                Envelope(t - 0.13f, 0.20f, 0.004f) * Tone(t, 1320f) * 0.45f);
            _promote = Synthesise("PromoteSfx", 0.44f, t =>
                Envelope(t, 0.14f, 0.004f) * Tone(t, 523.25f) * 0.4f +
                Envelope(t - 0.11f, 0.14f, 0.004f) * Tone(t, 659.25f) * 0.4f +
                Envelope(t - 0.22f, 0.20f, 0.004f) * Tone(t, 783.99f) * 0.4f);
            _win = Synthesise("WinSfx", 0.72f, t =>
                Envelope(t, 0.18f, 0.005f) * Tone(t, 523.25f) * 0.35f +
                Envelope(t - 0.14f, 0.18f, 0.005f) * Tone(t, 659.25f) * 0.35f +
                Envelope(t - 0.28f, 0.18f, 0.005f) * Tone(t, 783.99f) * 0.35f +
                Envelope(t - 0.42f, 0.30f, 0.005f) * (Tone(t, 1046.5f) * 0.3f + Tone(t, 783.99f) * 0.2f));
            _lose = Synthesise("LoseSfx", 0.80f, t =>
                Envelope(t, 0.24f, 0.006f) * Tone(t, 440f) * 0.35f +
                Envelope(t - 0.20f, 0.24f, 0.006f) * Tone(t, 349.23f) * 0.35f +
                Envelope(t - 0.40f, 0.38f, 0.006f) * Tone(t, 261.63f) * 0.38f);
            _draw = Synthesise("DrawSfx", 0.60f, t =>
                Envelope(t, 0.26f, 0.006f) * Tone(t, 392f) * 0.35f +
                Envelope(t - 0.24f, 0.32f, 0.006f) * Tone(t, 392f) * 0.3f);
            _click = Synthesise("ClickSfx", 0.05f, t => Envelope(t, 0.05f, 0.001f) * (Tone(t, 1100f) * 0.3f + Noise(t) * 0.2f));
            _illegal = Synthesise("IllegalSfx", 0.16f, t => Envelope(t, 0.16f, 0.002f) * Square(t, 120f) * 0.3f);
        }

        private static float Tone(float t, float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * t);

        private static float Square(float t, float frequency) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));

        /// <summary>Deterministic value noise, so a clip sounds identical on every run.</summary>
        private static float Noise(float t)
        {
            float x = t * 54321.7f;
            float hashed = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return (hashed - Mathf.Floor(hashed)) * 2f - 1f;
        }

        /// <summary>Linear attack into an exponential decay, offset so clips can be layered in sequence.</summary>
        private static float Envelope(float t, float decay, float attack)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;

            float remaining = (t - attack) / decay;
            return remaining >= 1f ? 0f : Mathf.Exp(-4f * remaining) * (1f - remaining);
        }

        private static AudioClip Synthesise(string name, float seconds, Func<float, float> generator)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];

            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(generator(i / (float)SampleRate), -1f, 1f);

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || _source == null) return;
            if (!GameSettings.SoundEnabled) return;

            _source.PlayOneShot(clip, Mathf.Clamp01(GameSettings.Volume * volumeScale));
        }

        public void PlayMove() => Play(_move);
        public void PlayCapture() => Play(_capture);
        public void PlayCastle() => Play(_castle);
        public void PlayCheck() => Play(_check, 0.85f);
        public void PlayPromotion() => Play(_promote);
        public void PlayWin() => Play(_win);
        public void PlayLose() => Play(_lose);
        public void PlayDraw() => Play(_draw);
        public void PlayClick() => Play(_click, 0.6f);
        public void PlayIllegal() => Play(_illegal, 0.5f);

        /// <summary>Used by the settings screen so a volume change is audible immediately.</summary>
        public void PreviewVolume() => Play(_click, 1f);
    }
}
