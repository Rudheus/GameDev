using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

// Cutscene video: mainkan VideoClip fullscreen lewat RawImage di Canvas.
// Video jalan real-time (nggak terpengaruh timeScale), jadi aman diputar saat
// game beku. Esc/Space = skip.
//
// Setup: komponen ini + VideoPlayer di GameObject yang sama (clip diisi di
// VideoPlayer), panel gelap + RawImage di Canvas. Wiring sama seperti
// StoryCutscene: tombol/trigger → Play(), event On Finished → lanjutan alurnya.
[RequireComponent(typeof(VideoPlayer))]
public class VideoCutscene : MonoBehaviour
{
    [Header("UI (drag dari Canvas)")]
    [Tooltip("Panel fullscreen milik cutscene (nonaktif di awal).")]
    public GameObject panel;
    [Tooltip("RawImage tempat video ditampilkan (child panel, full-stretch).")]
    public RawImage display;

    [Header("Opsi")]
    public bool skippable = true;

    [Header("Selesai")]
    [Tooltip("Dipanggil saat video habis atau di-skip.")]
    public UnityEvent onFinished;

    private VideoPlayer vp;
    private RenderTexture rt;
    private bool playing;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.loopPointReached += _ => Finish(); // video selesai sendiri
    }

    public void Play()
    {
        if (vp.clip == null)
        {
            Debug.LogWarning("VideoCutscene: VideoPlayer belum diisi clip.", this);
            onFinished.Invoke();
            return;
        }

        // RenderTexture seukuran video, dibuat sekali saat pertama diputar.
        if (rt == null)
        {
            rt = new RenderTexture((int)vp.clip.width, (int)vp.clip.height, 0);
            vp.targetTexture = rt;
            if (display != null) display.texture = rt;
        }

        GameUI.CutsceneActive = true; // GameUI: jangan buka pause selama cutscene
        playing = true;
        if (panel != null) panel.SetActive(true);
        vp.Play();
    }

    void Update()
    {
        if (!playing || !skippable) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)) Finish();
    }

    void Finish()
    {
        if (!playing) return;
        playing = false;
        GameUI.CutsceneActive = false;

        vp.Stop();
        if (panel != null) panel.SetActive(false);
        onFinished.Invoke();
    }
}
