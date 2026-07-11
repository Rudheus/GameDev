using UnityEngine;

// Audio global: BGM loop, ambience opsional, sirine yang fade in/out otomatis
// saat ada polisi mengejar, dan stinger sekali-bunyi (tertangkap/menang).
// Taruh di satu GameObject (boleh nempel di object GameUI). AudioSource dibuat
// otomatis — cukup isi clip-nya di Inspector.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [Tooltip("BGM tegang, loop terus selama scene hidup.")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.35f;

    [Header("Ambience (opsional)")]
    [Tooltip("Suara latar loop: alarm penjara / angin malam / hujan.")]
    public AudioClip ambienceClip;
    [Range(0f, 1f)] public float ambienceVolume = 0.3f;

    [Header("Sirine — otomatis saat dikejar")]
    [Tooltip("Loop sirine polisi. Fade in saat ada polisi mengejar, fade out saat lolos.")]
    public AudioClip sirenClip;
    [Range(0f, 1f)] public float sirenVolume = 0.55f;
    [Tooltip("Kecepatan fade sirine (volume per detik).")]
    public float sirenFadeSpeed = 0.8f;

    [Header("Deteksi & intensitas")]
    [Tooltip("Sting sekali bunyi saat polisi PERTAMA KALI melihatmu (alert '!').")]
    public AudioClip detectedClip;
    [Tooltip("Jarak polisi pengejar saat intensitas maksimal (sirine paling keras & tinggi).")]
    public float intensityNearDistance = 4f;
    [Tooltip("Jarak polisi pengejar saat intensitas minimal.")]
    public float intensityFarDistance = 18f;
    [Tooltip("Pitch sirine saat polisi nempel (1 = normal). Naik = makin panik.")]
    public float sirenMaxPitch = 1.15f;
    [Range(0f, 1f)]
    [Tooltip("Pengali volume BGM selama dikejar (ducking) — biar sirine mendominasi.")]
    public float bgmDuckWhileChased = 0.35f;

    [Header("Stingers")]
    public AudioClip caughtClip;
    public AudioClip winClip;
    [Range(0f, 1f)] public float stingerVolume = 0.8f;

    private AudioSource bgmSource;
    private AudioSource ambienceSource;
    private AudioSource sirenSource;
    private AudioSource sfxSource;
    private EnemyChaser[] enemies;
    private bool[] wasChasing;      // buat deteksi momen "baru saja melihatmu"
    private Transform playerT;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bgmSource = CreateSource(loop: true);
        ambienceSource = CreateSource(loop: true);
        sirenSource = CreateSource(loop: true);
        sfxSource = CreateSource(loop: false);
    }

    void Start()
    {
        enemies = FindObjectsByType<EnemyChaser>(FindObjectsSortMode.None);
        wasChasing = new bool[enemies.Length];

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) playerT = player.transform;

        PlayLoop(bgmSource, bgmClip, bgmVolume);
        PlayLoop(ambienceSource, ambienceClip, ambienceVolume);

        // Sirine standby volume 0 — Update yang mengatur naik/turunnya.
        if (sirenClip != null)
        {
            sirenSource.clip = sirenClip;
            sirenSource.volume = 0f;
            sirenSource.Play();
        }
    }

    void Update()
    {
        // Scan semua polisi: ada yang ngejar? seberapa dekat yang paling dekat?
        // Sekalian deteksi transisi "baru mulai ngejar" → sting alert.
        bool chased = false;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null) continue;

            bool chasing = e.IsChasing;
            if (chasing && !wasChasing[i]) PlayOneShot(detectedClip); // baru saja melihatmu!
            wasChasing[i] = chasing;

            if (chasing)
            {
                chased = true;
                if (playerT != null)
                {
                    float dSqr = (e.transform.position - playerT.position).sqrMagnitude;
                    if (dSqr < nearestSqr) nearestSqr = dSqr;
                }
            }
        }

        // Intensitas 0..1 dari jarak polisi pengejar terdekat (dekat = 1 = panik).
        float intensity = 0f;
        if (chased && nearestSqr < float.MaxValue)
        {
            float dist = Mathf.Sqrt(nearestSqr);
            intensity = Mathf.InverseLerp(intensityFarDistance, intensityNearDistance, dist);
        }

        float dt = Time.unscaledDeltaTime; // fade tetap jalan walau timeScale 0 (pause)

        // Sirine: volume & pitch naik mengikuti intensitas.
        if (sirenClip != null)
        {
            float targetVol = chased ? Mathf.Lerp(0.5f, 1f, intensity) * sirenVolume : 0f;
            sirenSource.volume = Mathf.MoveTowards(sirenSource.volume, targetVol, sirenFadeSpeed * dt);

            float targetPitch = chased ? Mathf.Lerp(1f, sirenMaxPitch, intensity) : 1f;
            sirenSource.pitch = Mathf.MoveTowards(sirenSource.pitch, targetPitch, 0.5f * dt);
        }

        // BGM ducking: mengecil selama dikejar, balik normal setelah lolos.
        if (bgmClip != null)
        {
            float targetBgm = bgmVolume * (chased ? bgmDuckWhileChased : 1f);
            bgmSource.volume = Mathf.MoveTowards(bgmSource.volume, targetBgm, sirenFadeSpeed * dt);
        }
    }

    public void PlayCaught() => PlayOneShot(caughtClip);

    // Menang: semua musik latar dimatikan, sisakan sting kemenangan saja.
    public void PlayWin()
    {
        bgmSource.Stop();
        sirenSource.Stop();
        ambienceSource.Stop();
        PlayOneShot(winClip);
    }

    // Buat SFX bebas (tombol UI, dll.) dari script lain.
    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, stingerVolume * volumeScale);
    }

    AudioSource CreateSource(bool loop)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D — kedengaran sama di mana pun player berada
        return src;
    }

    static void PlayLoop(AudioSource src, AudioClip clip, float volume)
    {
        if (clip == null) return;
        src.clip = clip;
        src.volume = volume;
        src.Play();
    }
}
