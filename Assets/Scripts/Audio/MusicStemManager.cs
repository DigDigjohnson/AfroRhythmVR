using UnityEngine;

public class MusicStemManager : MonoBehaviour
{
    [Header("Faixas sincronizadas")]
    [SerializeField] private AudioClip instrumental1;
    [SerializeField] private AudioClip instrumental2;
    [SerializeField] private AudioClip instrumental3;
    [SerializeField] private AudioClip percussion;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float instrumentalVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float percussionVolume = 0.8f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

    private AudioSource[] sources;
    private AudioSource percussionSource;
    private float percussionTargetVolume;
    private bool isPlaying;

    public float Duration => instrumental1 != null ? instrumental1.length : 0f;
    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        sources = new AudioSource[4];
        AudioClip[] clips = { instrumental1, instrumental2, instrumental3, percussion };

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.clip = clips[i];
            source.volume = i == 3 ? percussionVolume : instrumentalVolume;
            sources[i] = source;
        }

        percussionSource = sources[3];
        percussionTargetVolume = percussionVolume;
    }

    public bool HasAllClips()
    {
        return instrumental1 != null && instrumental2 != null &&
               instrumental3 != null && percussion != null;
    }

    public bool PlayMusic(double dspStartTime)
    {
        if (!HasAllClips())
        {
            Debug.LogError("MusicStemManager: configure as quatro faixas no Inspector.");
            return false;
        }

        StopMusic();

        AudioClip[] clips = { instrumental1, instrumental2, instrumental3, percussion };
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].clip = clips[i];
            sources[i].volume = i == 3 ? percussionVolume : instrumentalVolume;
            sources[i].PlayScheduled(dspStartTime);
        }

        percussionTargetVolume = percussionVolume;
        isPlaying = true;

        for (int i = 1; i < clips.Length; i++)
        {
            if (Mathf.Abs(clips[i].length - clips[0].length) > 0.05f)
                Debug.LogWarning("MusicStemManager: as faixas possuem duracoes diferentes. Verifique o alinhamento dos stems.");
        }

        return true;
    }

    public void StopMusic()
    {
        if (sources == null)
            return;

        foreach (AudioSource source in sources)
            source.Stop();

        isPlaying = false;
        percussionTargetVolume = percussionVolume;
        if (percussionSource != null)
            percussionSource.volume = percussionVolume;
    }

    public void SetPercussionEnabled(bool enabled)
    {
        percussionTargetVolume = enabled ? percussionVolume : 0f;
    }

    private void Update()
    {
        if (!isPlaying || percussionSource == null)
            return;

        if (fadeDuration <= 0f)
        {
            percussionSource.volume = percussionTargetVolume;
            return;
        }

        float speed = percussionVolume / fadeDuration;
        percussionSource.volume = Mathf.MoveTowards(
            percussionSource.volume,
            percussionTargetVolume,
            speed * Time.unscaledDeltaTime
        );
    }

    private void OnDisable()
    {
        StopMusic();
    }
}
