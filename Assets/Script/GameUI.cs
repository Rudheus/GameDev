using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Logika alur game (Main Menu → Playing ⇄ Pause → Win). Visual dirakit di Canvas
// editor, lalu panel/teks/bar di-drag ke field di bawah. Tombol dipasang lewat
// OnClick di Inspector → panggil method public di sini (StartGame, Resume, dst).
// Skrip lain memanggil GameUI.Instance.ShowCaught() / ShowWin().
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    enum UIState { Menu, Playing, Paused, Won, Lost }

    [Header("Panels (drag dari Canvas)")]
    public GameObject menuPanel;
    public GameObject pausePanel;
    public GameObject winPanel;
    [Tooltip("Game over — waktu habis (skor 0): Adrian terlambat.")]
    public GameObject losePanel;

    [Header("HUD (drag dari Canvas)")]
    [Tooltip("Root stamina bar (disembunyikan saat di main menu).")]
    public GameObject staminaBar;
    [Tooltip("Image isi stamina. Paling enak set Image Type: Filled / Horizontal / Origin Left.")]
    public Image staminaFill;
    [Tooltip("Teks 'TERTANGKAP!' — dibiarkan nonaktif, dinyalakan sebentar saat ketangkap.")]
    public GameObject caughtText;

    [Header("Options")]
    [Tooltip("Berapa lama teks 'TERTANGKAP' muncul (detik).")]
    public float caughtFlashDuration = 1.2f;
    [Tooltip("Langsung main tanpa main menu — buat iterasi cepat di editor.")]
    public bool skipMenu = false;

    // Di-set tombol Restart: setelah scene reload, langsung main (lewati menu).
    static bool restartRequested;

    private UIState state = UIState.Menu;
    private float caughtTimer;

    private PlayerController player;
    private ThirdPersonCamera orbitCam;

    private static readonly Color staminaNormal = new Color(0.35f, 0.85f, 0.4f);
    private static readonly Color staminaExhausted = new Color(0.9f, 0.3f, 0.25f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Di Start (bukan Awake) biar player & kamera pasti sudah ada.
        player = FindFirstObjectByType<PlayerController>();
        orbitCam = FindFirstObjectByType<ThirdPersonCamera>();

        if (menuPanel == null)
        {
            // Canvas belum dirakit/di-assign — jangan kunci game di menu kosong.
            Debug.LogWarning("GameUI: menuPanel belum di-assign — langsung masuk mode main.", this);
            StartGame();
        }
        else if (skipMenu || restartRequested)
        {
            StartGame();
        }
        else
        {
            ShowMenu();
        }
        restartRequested = false;
    }

    void Update()
    {
        // unscaledDeltaTime supaya flash tetap jalan walau timeScale 0.
        if (caughtTimer > 0f)
        {
            caughtTimer -= Time.unscaledDeltaTime;
            if (caughtTimer <= 0f && caughtText != null) caughtText.SetActive(false);
        }

        // Stamina bar mengikuti player: isi = sisa stamina, merah saat exhausted.
        if (player != null && staminaFill != null)
        {
            if (staminaFill.type == Image.Type.Filled)
                staminaFill.fillAmount = player.Stamina01;
            else // fallback kalau Image-nya bukan Filled: skala horizontal (pivot kiri)
                staminaFill.rectTransform.localScale = new Vector3(player.Stamina01, 1f, 1f);

            staminaFill.color = player.IsExhausted ? staminaExhausted : staminaNormal;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == UIState.Playing) Pause();
            else if (state == UIState.Paused) Resume();
        }
    }

    // ---------- alur game (public = bisa dipanggil tombol lewat OnClick) ----------

    void ShowMenu()
    {
        state = UIState.Menu;
        SetPanels(menu: true);
        SetGameplayActive(false);
    }

    public void StartGame()
    {
        state = UIState.Playing;
        SetPanels();
        SetGameplayActive(true);
    }

    public void Pause()
    {
        state = UIState.Paused;
        SetPanels(pause: true);
        SetGameplayActive(false);
    }

    public void Resume()
    {
        state = UIState.Playing;
        SetPanels();
        SetGameplayActive(true);
    }

    // Reload scene = reset total (tembok hancur balik utuh, polisi & player ke awal).
    public void RestartLevel()
    {
        restartRequested = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMenu()
    {
        restartRequested = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Dipanggil saat player tertangkap polisi (dari PlayerRespawn).
    public void ShowCaught()
    {
        if (caughtText == null) return;
        caughtText.SetActive(true);
        caughtTimer = caughtFlashDuration;
    }

    // Dipanggil saat player sampai di goal (dari GoalTrigger).
    public void ShowWin()
    {
        state = UIState.Won;
        SetPanels(win: true);
        SetGameplayActive(false);
    }

    // Dipanggil ScoreManager saat skor habis: waktu Adrian habis — kalah.
    public void ShowGameOver()
    {
        state = UIState.Lost;
        SetPanels(lose: true);
        SetGameplayActive(false);
    }

    void SetPanels(bool menu = false, bool pause = false, bool win = false, bool lose = false)
    {
        if (menuPanel != null) menuPanel.SetActive(menu);
        if (pausePanel != null) pausePanel.SetActive(pause);
        if (winPanel != null) winPanel.SetActive(win);
        if (losePanel != null) losePanel.SetActive(lose);
        if (staminaBar != null) staminaBar.SetActive(!menu); // HUD sembunyi di main menu
    }

    // Satu pintu untuk waktu + cursor + kontrol player + kamera orbit.
    // Kamera wajib ikut dimatikan: LateUpdate-nya tetap jalan walau timeScale 0.
    void SetGameplayActive(bool active)
    {
        Time.timeScale = active ? 1f : 0f;
        Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !active;
        if (player != null) player.enabled = active;
        if (orbitCam != null) orbitCam.enabled = active;
    }
}
