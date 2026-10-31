using UnityEngine;

/// <summary>Small deterministic synthetic wood creaks; returned clips belong to the caller.</summary>
public static class ForestCreakSynthesizer
{
    public static AudioClip Create(int seed)
    {
        const int rate = 22050;
        const float duration = 2.8f;
        var samples = new float[(int)(rate * duration)];
        var random = new System.Random(seed);
        float phase = 0, filtered = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * t / duration), 2);
            float frequency = 105 + 28 * Mathf.Sin(t * 2.1f + seed) + 12 * Mathf.Sin(t * 17);
            phase += 2 * Mathf.PI * frequency / rate;
            filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, 0.18f);
            float friction = 0.6f + 0.4f * Mathf.Sin(t * 38 + Mathf.Sin(t * 7));
            samples[i] = envelope * friction * (Mathf.Sin(phase) * 0.25f + Mathf.Sin(phase * 2.03f) * 0.12f + filtered * 0.22f);
        }
        var clip = AudioClip.Create("Generated wood creak " + seed, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
