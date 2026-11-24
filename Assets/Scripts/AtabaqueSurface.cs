
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AtabaqueSurface : MonoBehaviour
{
    [Header("Sistema de ritmo")]
    [SerializeField] private RhythmManager rhythmManager;

    [Header("Sons")]
    [SerializeField] private AudioClip leftSound;
    [SerializeField] private AudioClip rightSound;
    [SerializeField] private AudioClip errorSound;

    [Header("Superficie")]
    [SerializeField] private float radius = 0.32f;

    private AudioSource audioSource;

    public float Radius => radius;

    public Vector3 SurfaceCenter =>
        transform.TransformPoint(Vector3.up);

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        if (leftSound == null)
            leftSound = GenerateSound(130f, false);

        if (rightSound == null)
            rightSound = GenerateSound(190f, false);

        if (errorSound == null)
            errorSound = GenerateSound(420f, true);
    }

    public void Hit(bool leftHand)
    {
        if (rhythmManager != null)
            rhythmManager.RegisterHit(leftHand);
    }

    public void PlayCorrect(bool leftHand)
    {
        AudioClip clip = leftHand
            ? leftSound
            : rightSound;

        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void PlayError()
    {
        if (errorSound != null)
            audioSource.PlayOneShot(errorSound);
    }

    private AudioClip GenerateSound(
        float frequency,
        bool errorTone)
    {
        int sampleRate = 44100;
        float duration = errorTone ? 0.18f : 0.3f;
        int count = Mathf.RoundToInt(
            sampleRate * duration
        );

        float[] samples = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(
                (errorTone ? -12f : -18f) * t
            );

            float pitch = errorTone
                ? frequency
                : frequency * Mathf.Exp(-10f * t);

            phase += 2f * Mathf.PI * pitch / sampleRate;

            float wave = Mathf.Sin(phase);

            if (errorTone)
                wave += 0.35f * Mathf.Sin(phase * 1.5f);

            samples[i] = wave * envelope * 0.4f;
        }

        AudioClip clip = AudioClip.Create(
            errorTone ? "Error" : "Atabaque",
            count,
            1,
            sampleRate,
            false
        );

        clip.SetData(samples, 0);
        return clip;
    }
}
