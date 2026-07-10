using System.Collections.Generic;
using UnityEngine;

// Tembok voxel yang hancur secara lokal (bukan runtuh total): dash/impact melubangi
// voxel di sekitar jalur tabrakan jadi terowongan, sisa tembok tetap berdiri.
// Voxel yang kehilangan jalur penyangga ke baris bawah ikut runtuh (structural collapse).
// Taruh di GameObject kosong (scale tetap 1,1,1) — voxel dibangun otomatis saat Start,
// pivot di tengah-bawah tembok jadi gampang ditaruh di lantai.
public class BreakableWall : MonoBehaviour
{
    [Header("Bentuk Tembok")]
    public Vector3 wallSize = new Vector3(4f, 3f, 0.4f);
    [Tooltip("Target ukuran sisi voxel (m). Jumlah grid dibulatkan supaya pas mengisi wallSize.")]
    public float voxelSize = 0.25f;
    [Range(0f, 0.2f)]
    [Tooltip("Celah visual antar voxel (fraksi ukuran voxel) biar pola blok kelihatan. Collider tetap rapat.")]
    public float mortarGap = 0.06f;
    public Material voxelMaterial;
    public Color voxelColor = new Color(0.65f, 0.6f, 0.55f);
    [Range(0f, 0.5f)]
    [Tooltip("Variasi gelap-terang acak per voxel biar puing kelihatan (0 = polos).")]
    public float colorVariation = 0.15f;

    [Header("Trigger Hancur")]
    [Tooltip("Momentum minimal penabrak non-player (massa × kecepatan) supaya tembok terlubangi. Dash player selalu melubangi lewat PlayerController.")]
    public float breakMomentum = 12f;

    [Header("Voxel Destruction")]
    [Tooltip("Radius terowongan yang dilubangi di sekitar jalur impact.")]
    public float carveRadius = 0.55f;
    [Tooltip("Kedalaman lubang searah impact (m). Set >= tebal tembok supaya dash menembus.")]
    public float penetrationDepth = 1.5f;
    [Tooltip("Voxel yang kehilangan sambungan ke baris bawah ikut runtuh setelah dilubangi.")]
    public bool structuralCollapse = true;

    [Header("Physics Puing")]
    [Tooltip("Massa jenis puing (kg/m³) — massa tiap voxel dihitung dari volumenya. Beton ringan ±600.")]
    public float density = 600f;
    [Tooltip("Radius pengaruh lemparan. Voxel dekat titik impact terlempar paling keras.")]
    public float impactRadius = 1.2f;
    [Range(0f, 1f)]
    [Tooltip("Fraksi kecepatan penabrak yang ditransfer ke voxel di titik impact.")]
    public float energyTransfer = 0.8f;
    [Tooltip("Kecepatan sebar radial dari titik impact (m/s).")]
    public float scatterSpeed = 2.5f;
    [Tooltip("Dorongan ke atas di sekitar titik impact (m/s).")]
    public float upwardBias = 1f;
    [Tooltip("Kecepatan putar acak maksimum puing (rad/s).")]
    public float tumbleSpeed = 8f;
    [Tooltip("Kecepatan sebar puing yang runtuh karena kehilangan penyangga (m/s).")]
    public float collapseScatter = 0.6f;

    [Header("Cleanup Puing")]
    public float debrisLifetime = 6f;
    public float debrisShrinkTime = 1.5f;

    private static MaterialPropertyBlock propertyBlock;

    private int nx, ny, nz;
    private Transform[] voxels; // slot null = voxel sudah lepas; index lihat Index()
    private int aliveCount;

    void Start()
    {
        BuildVoxels();

        // Rigidbody kinematic menyatukan semua collider voxel jadi satu compound:
        // murah selama tembok diam, dan OnCollisionEnter di root ini menerima
        // contact point persis di voxel yang kena.
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    int Index(int x, int y, int z) => (z * ny + y) * nx + x;

    void BuildVoxels()
    {
        nx = Mathf.Max(1, Mathf.RoundToInt(wallSize.x / voxelSize));
        ny = Mathf.Max(1, Mathf.RoundToInt(wallSize.y / voxelSize));
        nz = Mathf.Max(1, Mathf.RoundToInt(wallSize.z / voxelSize));

        // Terlalu banyak voxel bikin physics/editor tersedak — kasarkan grid otomatis.
        const int maxVoxels = 4096;
        int total = nx * ny * nz;
        if (total > maxVoxels)
        {
            float shrink = Mathf.Pow((float)total / maxVoxels, 1f / 3f);
            nx = Mathf.Max(1, Mathf.FloorToInt(nx / shrink));
            ny = Mathf.Max(1, Mathf.FloorToInt(ny / shrink));
            nz = Mathf.Max(1, Mathf.FloorToInt(nz / shrink));
            Debug.LogWarning($"BreakableWall: voxelSize {voxelSize} menghasilkan {total} voxel, grid dikasarkan ke {nx}×{ny}×{nz}.", this);
        }

        Vector3 cellSize = new Vector3(wallSize.x / nx, wallSize.y / ny, wallSize.z / nz);
        Vector3 visualSize = cellSize * (1f - mortarGap);
        voxels = new Transform[nx * ny * nz];

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxel.name = $"Voxel_{x}_{y}_{z}";
                    voxel.layer = gameObject.layer;
                    voxel.transform.SetParent(transform, false);
                    voxel.transform.localPosition = new Vector3(
                        -wallSize.x * 0.5f + (x + 0.5f) * cellSize.x,
                        (y + 0.5f) * cellSize.y,
                        -wallSize.z * 0.5f + (z + 0.5f) * cellSize.z);
                    voxel.transform.localScale = visualSize;

                    // Collider dikompensasi ke ukuran sel penuh supaya permukaan tembok rapat
                    // (celah mortar cuma visual); dikembalikan ke ukuran visual saat jadi puing.
                    voxel.GetComponent<BoxCollider>().size = Vector3.one / (1f - mortarGap);

                    var rend = voxel.GetComponent<MeshRenderer>();
                    if (voxelMaterial != null) rend.sharedMaterial = voxelMaterial;

                    // Tint per voxel via MaterialPropertyBlock supaya nggak bikin instance material baru.
                    float shade = 1f + Random.Range(-colorVariation, colorVariation);
                    Color tint = new Color(voxelColor.r * shade, voxelColor.g * shade, voxelColor.b * shade, 1f);
                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_BaseColor", tint);
                    rend.SetPropertyBlock(propertyBlock);

                    voxels[Index(x, y, z)] = voxel.transform;
                }

        aliveCount = voxels.Length;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Dash player ditangani PlayerController (memanggil Carve langsung);
        // jalan kaki biasa tidak boleh melubangi tembok berapapun massa player.
        if (collision.rigidbody != null && collision.rigidbody.TryGetComponent(out PlayerController _)) return;

        // Threshold pakai momentum (massa × kecepatan relatif) biar benda ringan/lambat
        // nggak melubangi tembok, tapi benda berat yang pelan tetap bisa.
        float mass = collision.rigidbody != null ? collision.rigidbody.mass : 1f;
        if (mass * collision.relativeVelocity.magnitude < breakMomentum) return;

        ContactPoint contact = collision.GetContact(0);

        // Tanda relativeVelocity ambigu — pastikan arah dorong masuk ke dalam tembok
        // (sisi yang kena diketahui dari tanda Z lokal titik kontak).
        Vector3 impactVelocity = -collision.relativeVelocity;
        float sideZ = transform.InverseTransformPoint(contact.point).z;
        Vector3 inward = transform.TransformDirection(new Vector3(0f, 0f, -Mathf.Sign(sideZ)));
        if (Vector3.Dot(impactVelocity, inward) < 0f) impactVelocity = -impactVelocity;

        Carve(contact.point, impactVelocity);
    }

    // Lubangi tembok: voxel di dalam kapsul (impactPoint → penetrationDepth searah
    // impactVelocity) lepas jadi puing terlempar; sisa tembok tetap berdiri.
    public void Carve(Vector3 impactPoint, Vector3 impactVelocity)
    {
        if (aliveCount == 0) return;

        float impactSpeed = impactVelocity.magnitude;
        Vector3 pushDir = impactSpeed > 0.01f ? impactVelocity / impactSpeed : -transform.forward;
        Vector3 tunnelEnd = impactPoint + pushDir * penetrationDepth;

        bool carvedAny = false;
        int nearestIndex = -1;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < voxels.Length; i++)
        {
            Transform voxel = voxels[i];
            if (voxel == null) continue;

            if (DistanceToSegment(voxel.position, impactPoint, tunnelEnd) <= carveRadius)
            {
                ThrowAsDebris(i, impactPoint, pushDir, impactSpeed);
                carvedAny = true;
            }
            else
            {
                float dSqr = (voxel.position - impactPoint).sqrMagnitude;
                if (dSqr < nearestSqr) { nearestSqr = dSqr; nearestIndex = i; }
            }
        }

        // Impact menyerempet / carveRadius lebih kecil dari voxel: minimal voxel
        // terdekat dari titik kontak tetap lepas biar tabrakan selalu ada respons.
        if (!carvedAny && nearestIndex >= 0)
        {
            ThrowAsDebris(nearestIndex, impactPoint, pushDir, impactSpeed);
        }

        if (structuralCollapse) CollapseUnsupported();

        if (aliveCount == 0) Destroy(gameObject);
    }

    // Voxel di jalur impact: terlempar searah tabrakan + sebar radial, falloff
    // kuadratik dari titik impact (dekat → keras, jauh → cuma terdorong pelan).
    void ThrowAsDebris(int index, Vector3 impactPoint, Vector3 pushDir, float impactSpeed)
    {
        Vector3 position = voxels[index].position;
        Rigidbody rb = Detach(index);

        float dist = Vector3.Distance(position, impactPoint);
        float falloff = 1f / (1f + (dist * dist) / (impactRadius * impactRadius));

        Vector3 radial = position - impactPoint;
        radial = radial.sqrMagnitude > 0.0001f ? radial.normalized : Random.onUnitSphere;

        rb.linearVelocity =
            pushDir * (impactSpeed * energyTransfer * falloff)
            + radial * (scatterSpeed * falloff)
            + Vector3.up * (upwardBias * falloff);
        rb.angularVelocity = Random.insideUnitSphere * (tumbleSpeed * falloff);
    }

    // Voxel yang tidak lagi tersambung ke baris bawah (lewat 6 tetangga grid)
    // kehilangan penyangga → runtuh kena gravitasi, cuma diberi sebar kecil.
    void CollapseUnsupported()
    {
        bool[] supported = new bool[voxels.Length];
        var queue = new Queue<int>();

        void Visit(int x, int y, int z)
        {
            if (x < 0 || x >= nx || y < 0 || y >= ny || z < 0 || z >= nz) return;
            int i = Index(x, y, z);
            if (supported[i] || voxels[i] == null) return;
            supported[i] = true;
            queue.Enqueue(i);
        }

        // Flood fill dari voxel baris bawah (menyentuh tanah).
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
                Visit(x, 0, z);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % nx;
            int y = (i / nx) % ny;
            int z = (i / nx) / ny;
            Visit(x - 1, y, z);
            Visit(x + 1, y, z);
            Visit(x, y - 1, z);
            Visit(x, y + 1, z);
            Visit(x, y, z - 1);
            Visit(x, y, z + 1);
        }

        for (int i = 0; i < voxels.Length; i++)
        {
            if (voxels[i] == null || supported[i]) continue;

            Rigidbody rb = Detach(i);
            rb.linearVelocity = Random.insideUnitSphere * collapseScatter;
            rb.angularVelocity = Random.insideUnitSphere * (tumbleSpeed * 0.3f);
        }
    }

    // Lepaskan voxel dari tembok jadi puing ber-physics; kecepatan diatur pemanggil.
    Rigidbody Detach(int index)
    {
        Transform voxel = voxels[index];
        voxels[index] = null;
        aliveCount--;

        voxel.SetParent(null, true);

        // Balikkan collider ke ukuran visual — ukuran sel penuh saling bersentuhan
        // dengan tetangga dan bikin puing meledak sendiri karena depenetrasi.
        voxel.GetComponent<BoxCollider>().size = Vector3.one;

        var rb = voxel.gameObject.AddComponent<Rigidbody>();
        Vector3 s = voxel.lossyScale;
        rb.mass = Mathf.Max(0.05f, density * s.x * s.y * s.z);
        // Puing kecil + kecepatan tinggi rawan tembus lantai dengan deteksi discrete.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        voxel.gameObject.AddComponent<Debris>()
            .Init(debrisLifetime + Random.Range(0f, 1.5f), debrisShrinkTime);

        return rb;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return Vector3.Distance(p, a + ab * t);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(0f, wallSize.y * 0.5f, 0f), wallSize);
    }

    // Ditempel ke tiap puing saat lepas: setelah lifetime, menyusut lalu Destroy.
    // Nested karena hanya dipakai lewat AddComponent di runtime.
    private class Debris : MonoBehaviour
    {
        private float lifetime;
        private float shrinkTime;
        private float timer;
        private Vector3 initialScale;

        public void Init(float lifetime, float shrinkTime)
        {
            this.lifetime = lifetime;
            this.shrinkTime = Mathf.Max(0.01f, shrinkTime);
            initialScale = transform.localScale;
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < lifetime) return;

            float t = (timer - lifetime) / shrinkTime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = initialScale * (1f - t);
        }
    }
}
