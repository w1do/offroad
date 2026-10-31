using UnityEngine;

/// <summary>Bounded spatial voice pool; only nearby trees creak, according to the shared wind.</summary>
[DisallowMultipleComponent]
public sealed class ForestWindAudio : MonoBehaviour
{
    public ForestWind wind;
    public Transform forestRoot;
    [Range(0, 1)] public float volume = 0.22f;
    public float audibleRadius = 28;
    public int PlayedCreaks { get; private set; }
    private ForestTreeWind[] trees;
    private AudioSource[] voices;
    private AudioClip[] clips;
    private Transform listener;
    private float nextCreak;
    private float nextForestScan;
    private System.Random random;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        random = new System.Random(3101026);
        trees = forestRoot == null ? new ForestTreeWind[0] : forestRoot.GetComponentsInChildren<ForestTreeWind>();
        clips = new AudioClip[3];
        voices = new AudioSource[4];
        for (int i = 0; i < clips.Length; i++) clips[i] = ForestCreakSynthesizer.Create(17 + i * 29);
        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("Spatial tree creak " + i);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.minDistance = 3;
            source.maxDistance = audibleRadius;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0;
            voices[i] = source;
        }
        nextCreak = Time.time + 1;
    }

    private void Update()
    {
        if (voices == null) return;
        if (Time.time >= nextForestScan) { nextForestScan = Time.time + 2; if (forestRoot != null) trees = forestRoot.GetComponentsInChildren<ForestTreeWind>(); }
        if (wind == null || wind.strength < 0.15f || Time.time < nextCreak) return;
        if (listener == null)
        {
            var audioListener = FindAnyObjectByType<AudioListener>();
            if (audioListener != null) listener = audioListener.transform;
        }
        nextCreak = Time.time + Mathf.Lerp(6, 1.7f, Mathf.Clamp01(wind.strength)) + (float)random.NextDouble() * 2;
        if (listener == null || trees.Length == 0) return;
        int start = random.Next(trees.Length);
        for (int i = 0; i < trees.Length; i++)
        {
            var tree = trees[(start + i) % trees.Length];
            if (tree == null || !tree.isActiveAndEnabled) continue;
            var position = tree.transform.position + Vector3.up * 3;
            if ((listener.position - position).sqrMagnitude > audibleRadius * audibleRadius) continue;
            var voice = System.Array.Find(voices, v => !v.isPlaying);
            if (voice == null) return;
            float phase = Vector2.Dot(new Vector2(position.x, position.z), new Vector2(.17f, .23f));
            float gust = .65f + .22f * Mathf.Sin(wind.WindTime * .63f + phase) + .13f * Mathf.Sin(wind.WindTime * 1.27f + phase * .7f);
            voice.transform.position = position;
            voice.clip = clips[random.Next(clips.Length)];
            voice.pitch = .8f + (float)random.NextDouble() * .4f;
            voice.volume = volume * Mathf.Clamp01(wind.strength * gust);
            voice.Play();
            PlayedCreaks++;
            return;
        }
    }

    private void OnDisable()
    {
        if (voices != null) foreach (var voice in voices) if (voice != null) { voice.Stop(); Destroy(voice.gameObject); }
        if (clips != null) foreach (var clip in clips) if (clip != null) Destroy(clip);
        voices = null;
        clips = null;
        listener = null;
    }
}


