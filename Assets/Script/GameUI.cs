using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI + alur game (Main Menu → Playing ⇄ Pause → Win) dibangun via kode.
// Cukup taruh komponen ini di satu GameObject kosong di scene gameplay.
// Skrip lain memanggil GameUI.Instance.ShowCaught() / ShowWin().
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    enum UIState { Menu, Playing, Paused, Won }

    [Tooltip("Berapa lama teks 'TERTANGKAP' muncul (detik).")]
    public float caughtFlashDuration = 1.2f;
    [Tooltip("Langsung main tanpa main menu — buat iterasi cepat di editor.")]
    public bool skipMenu = false;

    // Di-set tombol Restart: setelah scene reload, langsung main (lewati menu).
    static bool restartRequested;

    private UIState state = UIState.Menu;
    private Font font;
    private Text caughtText;
    private GameObject menuPanel;
    private GameObject pausePanel;
    private GameObject winPanel;
    private Image staminaFill;
    private GameObject staminaBar;
    private float caughtTimer;

    private PlayerController player;
    private ThirdPersonCamera orbitCam;

    private static readonly Color staminaNormal = new Color(0.35f, 0.85f, 0.4f);
    private static readonly Color staminaExhausted = new Color(0.9f, 0.3f, 0.25f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        EnsureEventSystem();
    }

    void Start()
    {
        // Di Start (bukan Awake) biar player & kamera pasti sudah ada.
        player = FindFirstObjectByType<PlayerController>();
        orbitCam = FindFirstObjectByType<ThirdPersonCamera>();

        if (skipMenu || restartRequested) StartGame();
        else ShowMenu();
        restartRequested = false;
    }

    void Update()
    {
        // unscaledDeltaTime supaya flash tetap jalan walau timeScale 0.
        if (caughtTimer > 0f)
        {
            caughtTimer -= Time.unscaledDeltaTime;
            if (caughtTimer <= 0f) caughtText.gameObject.SetActive(false);
        }

        // Stamina bar mengikuti player: panjang = sisa stamina, merah saat exhausted.
        if (player != null && staminaFill != null)
        {
            staminaFill.rectTransform.localScale = new Vector3(player.Stamina01, 1f, 1f);
            staminaFill.color = player.IsExhausted ? staminaExhausted : staminaNormal;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == UIState.Playing) Pause();
            else if (state == UIState.Paused) Resume();
        }
    }

    // ---------- alur game ----------

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

    void Pause()
    {
        state = UIState.Paused;
        SetPanels(pause: true);
        SetGameplayActive(false);
    }

    void Resume()
    {
        state = UIState.Playing;
        SetPanels();
        SetGameplayActive(true);
    }

    // Reload scene = reset total (tembok hancur balik utuh, polisi & player ke awal).
    void Restart()
    {
        restartRequested = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void BackToMenu()
    {
        restartRequested = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void QuitGame()
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
        caughtText.gameObject.SetActive(true);
        caughtTimer = caughtFlashDuration;
    }

    // Dipanggil saat player sampai di goal (dari GoalTrigger).
    public void ShowWin()
    {
        state = UIState.Won;
        SetPanels(win: true);
        SetGameplayActive(false);
    }

    void SetPanels(bool menu = false, bool pause = false, bool win = false)
    {
        menuPanel.SetActive(menu);
        pausePanel.SetActive(pause);
        winPanel.SetActive(win);
        staminaBar.SetActive(!menu); // HUD disembunyikan di main menu
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

    // ---------- pembangunan UI ----------

    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("GameUI_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        Transform root = canvasGO.transform;

        // --- HUD ---
        BuildStaminaBar(root);
        caughtText = MakeText(root, "TERTANGKAP!", 90, new Color(1f, 0.25f, 0.2f), Vector2.zero, new Vector2(1200f, 200f));
        caughtText.gameObject.SetActive(false);

        // --- Main menu ---
        menuPanel = MakePanel(root, 0.88f);
        MakeText(menuPanel.transform, "LAST ESCAPE", 110, Color.white, new Vector2(0f, 200f), new Vector2(1400f, 160f));
        MakeText(menuPanel.transform, "Kabur dari penjara. Demi Elina.", 30, new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, 110f), new Vector2(1000f, 60f));
        MakeButton(menuPanel.transform, "MULAI", new Vector2(0f, -30f), StartGame);
        MakeButton(menuPanel.transform, "KELUAR", new Vector2(0f, -110f), QuitGame);

        // --- Pause ---
        pausePanel = MakePanel(root, 0.7f);
        MakeText(pausePanel.transform, "PAUSE", 80, Color.white, new Vector2(0f, 180f), new Vector2(800f, 120f));
        MakeButton(pausePanel.transform, "LANJUT", new Vector2(0f, 40f), Resume);
        MakeButton(pausePanel.transform, "ULANGI LEVEL", new Vector2(0f, -40f), Restart);
        MakeButton(pausePanel.transform, "MENU UTAMA", new Vector2(0f, -120f), BackToMenu);

        // --- Win / ending ---
        winPanel = MakePanel(root, 0.8f);
        MakeText(winPanel.transform, "KAMU BEBAS", 100, Color.white, new Vector2(0f, 170f), new Vector2(1400f, 150f));
        MakeText(winPanel.transform, "Adrian berhasil lolos... perjalanan menuju Elina berlanjut.", 30, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, 80f), new Vector2(1400f, 60f));
        MakeButton(winPanel.transform, "MAIN LAGI", new Vector2(0f, -40f), Restart);
        MakeButton(winPanel.transform, "MENU UTAMA", new Vector2(0f, -120f), BackToMenu);
    }

    void BuildStaminaBar(Transform root)
    {
        // Background gelap + fill yang menyusut dari kanan (pivot kiri).
        staminaBar = new GameObject("StaminaBar");
        staminaBar.transform.SetParent(root, false);
        var bgImg = staminaBar.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRT = bgImg.rectTransform;
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0f, 0f); // pojok kiri-bawah
        bgRT.pivot = new Vector2(0f, 0f);
        bgRT.anchoredPosition = new Vector2(40f, 40f);
        bgRT.sizeDelta = new Vector2(360f, 26f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(staminaBar.transform, false);
        staminaFill = fillGO.AddComponent<Image>();
        staminaFill.color = staminaNormal;
        var fillRT = staminaFill.rectTransform;
        Stretch(fillRT);
        fillRT.pivot = new Vector2(0f, 0.5f); // pivot dulu, baru offset — biar nggak geser
        fillRT.offsetMin = new Vector2(3f, 3f);
        fillRT.offsetMax = new Vector2(-3f, -3f);
    }

    GameObject MakePanel(Transform parent, float alpha)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.03f, 0.06f, alpha); // gelap kebiruan — nuansa malam
        Stretch(img.rectTransform);
        go.SetActive(false);
        return go;
    }

    Text MakeText(Transform parent, string content, int size, Color color, Vector2 pos, Vector2 rectSize)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        // Shadow tipis biar kebaca di background terang/gelap.
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(3f, -3f);

        var rt = t.rectTransform;
        rt.sizeDelta = rectSize;
        rt.anchoredPosition = pos;
        return t;
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = Color.white;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // Warna dasar transparan; menyala saat hover/klik.
        var c = btn.colors;
        c.normalColor      = new Color(1f, 1f, 1f, 0.10f);
        c.highlightedColor = new Color(1f, 1f, 1f, 0.30f);
        c.pressedColor     = new Color(1f, 1f, 1f, 0.45f);
        c.selectedColor    = c.normalColor;
        btn.colors = c;
        btn.onClick.AddListener(onClick);

        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(340f, 62f);
        rt.anchoredPosition = pos;

        var t = MakeText(go.transform, label, 30, Color.white, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        return btn;
    }

    // Tombol butuh EventSystem buat menerima klik — bikin kalau scene belum punya.
    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.transform.SetParent(transform, false);
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    // Rentangkan RectTransform memenuhi parent.
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
