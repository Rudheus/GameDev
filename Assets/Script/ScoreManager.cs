using TMPro;
using UnityEngine;

// Sistem skor "berpacu dengan waktu": mulai dari startScore, berkurang tiap detik,
// kena penalti saat tertangkap, bertambah dari pickup. Berhenti dihitung saat menang.
// Taruh di satu GameObject (boleh nempel di GameUI), drag teks TMP-nya.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Aturan skor")]
    public int startScore = 10000;
    [Tooltip("Skor berkurang segini tiap detik — waktu adalah nyawa.")]
    public int timePenaltyPerSecond = 10;
    [Tooltip("Penalti tiap kali tertangkap polisi.")]
    public int caughtPenalty = 500;

    [Header("UI (drag dari Canvas)")]
    [Tooltip("Teks skor di HUD (update tiap frame).")]
    public TMP_Text hudScoreText;
    [Tooltip("Teks skor akhir di WinPanel (diisi saat menang). Opsional.")]
    public TMP_Text finalScoreText;

    private float elapsed;
    private int caughtCount;
    private int bonus;
    private bool running = true;

    public int CurrentScore =>
        Mathf.Max(0, startScore
                     - Mathf.FloorToInt(elapsed) * timePenaltyPerSecond
                     - caughtCount * caughtPenalty
                     + bonus);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // Time.deltaTime ikut timeScale → timer otomatis berhenti saat menu/pause.
        if (running)
        {
            elapsed += Time.deltaTime;

            // Skor habis = waktu Adrian habis — game over (sekali saja).
            if (CurrentScore <= 0)
            {
                running = false;
                if (GameUI.Instance != null) GameUI.Instance.ShowGameOver();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayLose();
            }
        }

        if (hudScoreText != null) hudScoreText.text = $"SKOR: {CurrentScore}";
    }

    // Dipanggil PlayerRespawn saat tertangkap.
    public void OnCaught() => caughtCount++;

    // Dipanggil ScorePickup (atau apa pun yang memberi bonus).
    public void AddBonus(int value) => bonus += value;

    // Dipanggil GoalTrigger saat menang: bekukan skor + tampilkan di panel menang.
    public void OnWin()
    {
        running = false;
        if (finalScoreText != null) finalScoreText.text = $"Skor Akhir: {CurrentScore}";
    }
}
