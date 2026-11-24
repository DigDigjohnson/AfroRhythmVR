using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class RhythmManager : MonoBehaviour
{
    public event Action OnGameFinished;

    [Header("Instrumento")]
    [SerializeField] private AtabaqueSurface drum;

    [Header("Audio Musical")]
    [SerializeField] private MusicStemManager musicStemManager;
    [SerializeField] private bool waitForMusicEnd = true;

    [Header("Mapa de Notas (segundos da musica)")]
    [SerializeField] private TextAsset noteMapCsv;
    [SerializeField, Min(0.5f)] private float startDelay = 3f;
    [SerializeField, Min(0.01f)] private float hitWindow = 0.25f;
    [SerializeField] private float noteTimingOffset = 0f;

    [Header("Indicadores Visuais")]
    [SerializeField] private Renderer leftCue;
    [SerializeField] private Renderer rightCue;

    [Header("Notas Descendentes")]
    [SerializeField, Min(0.1f)] private float noteTravelTime = 1.5f;
    [SerializeField, Min(0.1f)] private float spawnHeight = 0.8f;
    [SerializeField, Min(0.01f)] private float fallingNoteSize = 0.1f;

    [Header("Cores")]
    [SerializeField] private Color waitingColor = Color.gray;
    [SerializeField] private Color touchColor = Color.yellow;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float feedbackDuration = 0.5f;
    [SerializeField] private bool playMissSound = false;

    private struct Note
    {
        public double Time;
        public bool RightHand;
        public Note(double time, bool rightHand)
        {
            Time = time;
            RightHand = rightHand;
        }
    }

    private readonly List<Note> notes = new List<Note>();
    private GameObject[] fallingNotes = Array.Empty<GameObject>();
    private MaterialPropertyBlock leftProperties;
    private MaterialPropertyBlock rightProperties;
    private double songStartTime;
    private int nextNote;
    private bool isPlaying;
    private int hits;
    private int errors;
    private int misses;
    private double leftFeedbackUntil = -1;
    private double rightFeedbackUntil = -1;
    private Color leftFeedbackColor;
    private Color rightFeedbackColor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        leftProperties = new MaterialPropertyBlock();
        rightProperties = new MaterialPropertyBlock();
        leftFeedbackColor = waitingColor;
        rightFeedbackColor = waitingColor;
        ResetVisuals();
    }

    public void StartGame()
    {
        StopGame();

        if (drum == null || musicStemManager == null || !musicStemManager.HasAllClips())
        {
            Debug.LogError("RhythmManager: configure AtabaqueSkin, MusicStemManager e as quatro faixas.");
            return;
        }

        if (!LoadNoteMap())
            return;

        hitWindow = Mathf.Max(0.01f, hitWindow);
        noteTravelTime = Mathf.Max(0.1f, noteTravelTime);
        spawnHeight = Mathf.Max(0.1f, spawnHeight);
        fallingNoteSize = Mathf.Max(0.01f, fallingNoteSize);
        feedbackDuration = Mathf.Max(0f, feedbackDuration);

        nextNote = 0;
        hits = 0;
        errors = 0;
        misses = 0;
        fallingNotes = new GameObject[notes.Count];
        ResetVisuals();

        songStartTime = AudioSettings.dspTime + Mathf.Max(0.5f, startDelay);
        if (!musicStemManager.PlayMusic(songStartTime))
            return;

        isPlaying = true;
        if (GetNoteTime(notes.Count - 1) + hitWindow > songStartTime + musicStemManager.Duration)
            Debug.LogWarning("RhythmManager: ultima nota ultrapassa o final da faixa.");
        Debug.Log("Partida iniciada com " + notes.Count + " notas sincronizadas.");
    }

    public void StopGame()
    {
        isPlaying = false;
        if (musicStemManager != null)
            musicStemManager.StopMusic();
        RemoveAllFallingNotes();
        ResetVisuals();
    }

    private bool LoadNoteMap()
    {
        notes.Clear();
        if (noteMapCsv == null)
        {
            Debug.LogError("RhythmManager: atribua um arquivo CSV em Note Map Csv.");
            return false;
        }

        string[] lines = noteMapCsv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        double previousTime = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("time,", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] fields = line.Split(',');
            if (fields.Length != 2 || !double.TryParse(fields[0].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double time) || double.IsNaN(time) || double.IsInfinity(time))
            {
                Debug.LogError("RhythmManager: CSV invalido na linha " + (i + 1) + ": " + line);
                notes.Clear();
                return false;
            }

            string hand = fields[1].Trim().ToUpperInvariant();
            if (hand != "L" && hand != "R")
            {
                Debug.LogError("RhythmManager: use L (esquerda) ou R (direita), linha " + (i + 1));
                notes.Clear();
                return false;
            }

            if (time < 0 || time <= previousTime)
            {
                Debug.LogError("RhythmManager: tempos devem ser crescentes e nao negativos, linha " + (i + 1));
                notes.Clear();
                return false;
            }

            notes.Add(new Note(time, hand == "R"));
            previousTime = time;
        }

        if (notes.Count == 0)
        {
            Debug.LogError("RhythmManager: arquivo de notas vazio.");
            return false;
        }
        return true;
    }

    private double GetNoteTime(int index)
    {
        return songStartTime + notes[index].Time + noteTimingOffset;
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        double now = AudioSettings.dspTime;
        ProcessMissedNotes(now);
        UpdateFallingNotes(now);
        UpdateIndicators(now);

        bool allNotesJudged = nextNote >= notes.Count;
        bool musicEnded = now >= songStartTime + musicStemManager.Duration;
        if (allNotesJudged && (!waitForMusicEnd || musicEnded))
            FinishGame();
    }

    private void UpdateFallingNotes(double now)
    {
        for (int i = nextNote; i < notes.Count; i++)
        {
            double remaining = GetNoteTime(i) - now;
            if (remaining > noteTravelTime)
                break;
            if (remaining <= 0)
            {
                RemoveFallingNote(i);
                continue;
            }

            Renderer target = notes[i].RightHand ? rightCue : leftCue;
            if (target == null)
                continue;
            if (fallingNotes[i] == null)
                CreateFallingNote(i, target);
            if (fallingNotes[i] == null)
                continue;

            Vector3 end = target.transform.position;
            Vector3 start = end + Vector3.up * spawnHeight;
            float progress = Mathf.Clamp01(1f - (float)(remaining / noteTravelTime));
            fallingNotes[i].transform.position = Vector3.Lerp(start, end, progress);
        }
    }

    private void CreateFallingNote(int index, Renderer target)
    {
        GameObject note = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        note.name = "FallingNote_" + (index + 1);
        note.transform.localScale = Vector3.one * fallingNoteSize;
        Collider noteCollider = note.GetComponent<Collider>();
        if (noteCollider != null)
            Destroy(noteCollider);
        Renderer noteRenderer = note.GetComponent<Renderer>();
        noteRenderer.sharedMaterial = target.sharedMaterial;
        ApplyColor(noteRenderer, new MaterialPropertyBlock(), touchColor);
        fallingNotes[index] = note;
    }

    private void RemoveFallingNote(int index)
    {
        if (fallingNotes == null || index < 0 || index >= fallingNotes.Length)
            return;
        if (fallingNotes[index] != null)
        {
            Destroy(fallingNotes[index]);
            fallingNotes[index] = null;
        }
    }

    private void RemoveAllFallingNotes()
    {
        if (fallingNotes == null)
            return;
        for (int i = 0; i < fallingNotes.Length; i++)
            RemoveFallingNote(i);
    }

    public void RegisterHit(bool leftHand)
    {
        if (!isPlaying || drum == null)
            return;

        double now = AudioSettings.dspTime;
        ProcessMissedNotes(now);
        if (nextNote >= notes.Count)
            return;

        double difference = now - GetNoteTime(nextNote);
        bool correctTiming = Math.Abs(difference) <= hitWindow;
        bool correctHand = leftHand != notes[nextNote].RightHand;

        if (correctTiming && correctHand)
        {
            hits++;
            drum.PlayCorrect(leftHand);
            musicStemManager.SetPercussionEnabled(true);
            SetFeedback(leftHand, successColor, now);
            Debug.Log("HIT " + (nextNote + 1) + " | desvio " + (difference * 1000).ToString("F0") + " ms");
            RemoveFallingNote(nextNote);
            nextNote++;
        }
        else
        {
            errors++;
            drum.PlayError();
            musicStemManager.SetPercussionEnabled(false);
            SetFeedback(leftHand, errorColor, now);
            Debug.Log("WRONG HIT " + (nextNote + 1));
        }
        UpdateIndicators(now);
    }

    private void ProcessMissedNotes(double now)
    {
        while (nextNote < notes.Count && now > GetNoteTime(nextNote) + hitWindow)
        {
            misses++;
            bool missedLeft = !notes[nextNote].RightHand;
            SetFeedback(missedLeft, errorColor, now);
            musicStemManager.SetPercussionEnabled(false);
            if (playMissSound && drum != null)
                drum.PlayError();
            Debug.Log("MISS " + (nextNote + 1));
            RemoveFallingNote(nextNote);
            nextNote++;
        }
    }

    private void SetFeedback(bool leftHand, Color color, double now)
    {
        double until = now + feedbackDuration;
        if (leftHand)
        {
            leftFeedbackColor = color;
            leftFeedbackUntil = until;
        }
        else
        {
            rightFeedbackColor = color;
            rightFeedbackUntil = until;
        }
    }

    private void UpdateIndicators(double now)
    {
        Color left = waitingColor;
        Color right = waitingColor;
        if (isPlaying && nextNote < notes.Count && Math.Abs(now - GetNoteTime(nextNote)) <= hitWindow)
        {
            if (notes[nextNote].RightHand)
                right = touchColor;
            else
                left = touchColor;
        }
        if (now < leftFeedbackUntil)
            left = leftFeedbackColor;
        if (now < rightFeedbackUntil)
            right = rightFeedbackColor;
        ApplyColor(leftCue, leftProperties, left);
        ApplyColor(rightCue, rightProperties, right);
    }

    private void ApplyColor(Renderer target, MaterialPropertyBlock block, Color color)
    {
        if (target == null || block == null || target.sharedMaterial == null)
            return;
        Material material = target.sharedMaterial;
        target.GetPropertyBlock(block);
        if (material.HasProperty(BaseColorId))
            block.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId))
            block.SetColor(ColorId, color);
        if (material.HasProperty(EmissionColorId))
            block.SetColor(EmissionColorId, color == waitingColor ? Color.black : color * 1.5f);
        target.SetPropertyBlock(block);
    }

    private void ResetVisuals()
    {
        leftFeedbackUntil = -1;
        rightFeedbackUntil = -1;
        leftFeedbackColor = waitingColor;
        rightFeedbackColor = waitingColor;
        ApplyColor(leftCue, leftProperties, waitingColor);
        ApplyColor(rightCue, rightProperties, waitingColor);
    }

    private void FinishGame()
    {
        if (!isPlaying)
            return;
        isPlaying = false;
        musicStemManager.StopMusic();
        RemoveAllFallingNotes();
        ResetVisuals();
        Debug.Log("FIM | Acertos: " + hits + " | Erros: " + errors + " | Misses: " + misses);
        OnGameFinished?.Invoke();
    }

    private void OnDisable()
    {
        StopGame();
        if (leftCue != null)
            leftCue.SetPropertyBlock(null);
        if (rightCue != null)
            rightCue.SetPropertyBlock(null);
    }
}
