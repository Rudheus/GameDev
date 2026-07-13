using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Cutscene teks-cerita: panel gelap + narasi muncul huruf-per-huruf (typewriter).
// Klik/Space: lengkapi baris → baris berikutnya. Esc: skip seluruh cutscene.
// Jalan dengan unscaled time, jadi aman dipanggil saat game beku (timeScale 0).
//
// Wiring untuk opening: tombol MULAI → StoryCutscene.Play (bukan GameUI.StartGame),
// lalu event On Finished → GameUI.StartGame.
public class StoryCutscene : MonoBehaviour
{
    [Header("UI (drag dari Canvas)")]
    [Tooltip("Panel gelap full-screen milik cutscene (nonaktif di awal).")]
    public GameObject panel;
    [Tooltip("Teks TMP di dalam panel tempat narasi ditampilkan.")]
    public TMP_Text textDisplay;
    [Tooltip("Petunjuk kecil 'klik untuk lanjut' (opsional).")]
    public GameObject continueHint;

    [Header("Cerita")]
    [TextArea(2, 5)]
    public string[] lines;
    [Tooltip("Kecepatan ketik (huruf per detik).")]
    public float charsPerSecond = 40f;

    [Header("Selesai")]
    [Tooltip("Dipanggil setelah baris terakhir (atau di-skip). Hook: GameUI.StartGame.")]
    public UnityEvent onFinished;

    private int lineIndex;
    private float visibleChars;
    private bool playing;

    public void Play()
    {
        if (lines == null || lines.Length == 0) { Finish(); return; }

        GameUI.CutsceneActive = true; // Esc = skip cutscene, bukan buka pause
        playing = true;
        lineIndex = 0;
        visibleChars = 0f;
        if (panel != null) panel.SetActive(true);
        ShowLine();
    }

    void Update()
    {
        if (!playing || textDisplay == null) return;

        bool lineDone = visibleChars >= textDisplay.textInfo.characterCount;

        // Typewriter: tambah huruf yang tampak (unscaled — jalan walau game beku).
        if (!lineDone)
        {
            visibleChars += charsPerSecond * Time.unscaledDeltaTime;
            textDisplay.maxVisibleCharacters = Mathf.FloorToInt(visibleChars);
        }
        if (continueHint != null) continueHint.SetActive(lineDone);

        // Esc: skip semua. Klik/Space: lengkapi baris dulu, klik lagi baru lanjut.
        if (Input.GetKeyDown(KeyCode.Escape)) { Finish(); return; }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (!lineDone)
            {
                visibleChars = textDisplay.textInfo.characterCount; // lengkapi instan
                textDisplay.maxVisibleCharacters = int.MaxValue;
            }
            else
            {
                lineIndex++;
                if (lineIndex >= lines.Length) Finish();
                else ShowLine();
            }
        }
    }

    void ShowLine()
    {
        visibleChars = 0f;
        textDisplay.text = lines[lineIndex];
        textDisplay.maxVisibleCharacters = 0;
        textDisplay.ForceMeshUpdate(); // biar characterCount langsung benar
    }

    void Finish()
    {
        playing = false;
        GameUI.CutsceneActive = false;
        if (panel != null) panel.SetActive(false);
        onFinished.Invoke();
    }
}
