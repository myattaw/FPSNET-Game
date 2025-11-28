using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [Header("Gun Settings")]
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magSize = 20;
    public int bulletDamage = 25;

    [Header("References")]
    public GameObject bulletPrefab;
    public GameObject casingPrefab;
    public Transform bulletSpawn;
    public Transform casingEjectPoint;

    public GameObject tracerPrefab;
    public float tracerSpeed = 150f;
    public float tracerLifeTime = 3f;

    public Transform magazine;
    public Camera playerCamera;

    [Header("Audio")]
    public AudioSource gunAudioSource;
    public AudioClip gunshotSound;
    public AudioClip reloadSound;

    private int currentAmmo;
    private bool isReloading = false;
    private float nextTimeToFire = 0f;

    // Gun animation
    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private Vector3 reloadRotationOffset = new Vector3(60f, 50f, 50f);

    // Magazine animation
    private Vector3 magOriginalPos;
    private Quaternion magOriginalRot;
    private Vector3 magDropOffset = new Vector3(0f, -0.3f, 0f);

    //------------------------------------------------------------

    void Start()
    {
        currentAmmo = magSize;
        UIManager.instance.ammoText.text = "Ammo: " + currentAmmo;

        originalRotation = transform.localRotation;
        originalPosition = transform.localPosition;

        if (magazine != null)
        {
            magOriginalPos = magazine.localPosition;
            magOriginalRot = magazine.localRotation;
        }

        if (!IsOwner && gunAudioSource != null)
            gunAudioSource.spatialBlend = 1f;
    }

    //------------------------------------------------------------
    // SHOOT INPUT
    //------------------------------------------------------------

    public void Shoot()
    {
        if (!IsOwner || isReloading) return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        if (Time.time < nextTimeToFire) return;

        nextTimeToFire = Time.time + fireRate;
        currentAmmo--;
        UIManager.instance.ammoText.text = "Ammo: " + currentAmmo;

        // client-side aim raycast
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Vector3 targetPoint = ray.GetPoint(1000f);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            targetPoint = hit.point;

        Vector3 direction = (targetPoint - bulletSpawn.position).normalized;

        if (casingPrefab) EjectCasingLocal();

        FireServerRpc(bulletSpawn.position, direction);

        // audio
        if (IsServer) PlayShootSoundClientRpc();
        else PlayShootSoundServerRpc();
    }

    //------------------------------------------------------------
    // AUDIO
    //------------------------------------------------------------

    [ServerRpc]
    private void PlayShootSoundServerRpc() => PlayShootSoundClientRpc();

    [ClientRpc]
    private void PlayShootSoundClientRpc()
    {
        if (gunAudioSource && gunshotSound)
            gunAudioSource.PlayOneShot(gunshotSound, 0.5f);
    }

    [ServerRpc]
    private void PlayReloadSoundServerRpc() => PlayReloadSoundClientRpc();

    [ClientRpc]
    private void PlayReloadSoundClientRpc()
    {
        if (gunAudioSource && reloadSound)
            gunAudioSource.PlayOneShot(reloadSound);
    }

    //------------------------------------------------------------
    // SERVER AUTHORITATIVE FIRING
    //------------------------------------------------------------

    // Struct guaranteeing per-shot immutable data
    private struct BulletShot
    {
        public Vector3 origin;
        public Vector3 direction;
        public float distance;
        public float travelTime;
        public ulong shooter;
        public int damage;
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 spawnPos, Vector3 dir, ServerRpcParams rpcParams = default)
    {
        ulong shooter = rpcParams.Receive.SenderClientId;

        // Initial hit check (fast raycast)
        bool didHit = Physics.Raycast(spawnPos, dir, out RaycastHit hitInfo, 1000f);

        float distance;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.zero;

        if (didHit)
        {
            hitPoint = hitInfo.point;
            hitNormal = hitInfo.normal;
            distance = Vector3.Distance(spawnPos, hitPoint);
        }
        else
        {
            float visualRange = tracerSpeed * tracerLifeTime;
            distance = Mathf.Clamp(visualRange, 1f, 1000f);
        }

        float travelTime = distance / Mathf.Max(0.01f, tracerSpeed);

        // Shot data (per bullet)
        BulletShot shot = new BulletShot()
        {
            origin = spawnPos,
            direction = dir,
            distance = distance,
            travelTime = travelTime,
            shooter = shooter,
            damage = bulletDamage
        };

        StartCoroutine(ProcessShot(shot));

        // Spawn tracer visuals on clients
        SpawnTracerClientRpc(spawnPos, dir, didHit, hitPoint, hitNormal, tracerSpeed, tracerLifeTime);
    }

    //------------------------------------------------------------
    // SERVER HIT VERIFICATION (delayed recast)
    //------------------------------------------------------------

    private IEnumerator ProcessShot(BulletShot shot)
    {
        yield return new WaitForSeconds(shot.travelTime);

        float tolerance = 0.25f;

        if (Physics.Raycast(
            shot.origin,
            shot.direction,
            out RaycastHit hit,
            shot.distance + tolerance))
        {
            var target = hit.collider.GetComponentInParent<UnityStandardAssets.Characters.FirstPerson.NetworkFirstPersonController>();

            if (target != null && target.OwnerClientId != shot.shooter)
                target.ApplyDamage(shot.damage, shot.shooter);

            // if you want NPC/environment damage, add it here
        }
    }

    //------------------------------------------------------------
    // CLIENT VISUAL TRACER
    //------------------------------------------------------------

    [ClientRpc]
    private void SpawnTracerClientRpc(
        Vector3 spawnPos,
        Vector3 dir,
        bool didHit,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float speed,
        float lifetime)
    {
        GameObject prefab = tracerPrefab != null ? tracerPrefab : bulletPrefab;
        if (!prefab) return;

        GameObject vis = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        // --- ALWAYS ensure VisualBullet exists ---
        VisualBullet vb = vis.GetComponent<VisualBullet>();
        if (vb == null)
            vb = vis.AddComponent<VisualBullet>();

        // --- initialize movement ---
        if (didHit)
            vb.InitializeToTarget(hitPoint, speed, lifetime);
        else
            vb.InitializeDirection(dir, speed, lifetime);

        // --- guarantee no physics interference ---
        Rigidbody rb = vis.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Destroy(vis, lifetime + 0.1f);
    }


    //------------------------------------------------------------
    // CASING
    //------------------------------------------------------------

    private void EjectCasingLocal()
    {
        GameObject casing = Instantiate(casingPrefab, casingEjectPoint.position, casingEjectPoint.rotation);

        if (casing.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            Vector3 ejectDir = casingEjectPoint.right * 0.8f + casingEjectPoint.up * 0.4f;

            rb.linearVelocity = ejectDir.normalized * Random.Range(2f, 6f);
            rb.angularVelocity = Random.insideUnitSphere * 20f;
        }

        Destroy(casing, 3f);
    }

    //------------------------------------------------------------
    // RELOAD
    //------------------------------------------------------------

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (IsServer) PlayReloadSoundClientRpc();
        else PlayReloadSoundServerRpc();

        float halfTime = reloadTime / 2f;
        float elapsed = 0f;

        // gun down
        Quaternion targetRot = Quaternion.Euler(originalRotation.eulerAngles + reloadRotationOffset);

        while (elapsed < halfTime)
        {
            transform.localRotation = Quaternion.Slerp(originalRotation, targetRot, elapsed / halfTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // magazine animation
        if (magazine != null)
        {
            float magTime = 0.25f;
            elapsed = 0f;

            while (elapsed < magTime)
            {
                magazine.localPosition = Vector3.Lerp(magOriginalPos, magOriginalPos + magDropOffset, elapsed / magTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            magazine.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);

            magazine.gameObject.SetActive(true);
            magazine.localPosition = magOriginalPos;
        }

        // gun up
        elapsed = 0f;
        while (elapsed < halfTime)
        {
            transform.localRotation = Quaternion.Slerp(targetRot, originalRotation, elapsed / halfTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentAmmo = magSize;
        UIManager.instance.ammoText.text = "Ammo: " + currentAmmo;
        isReloading = false;
    }

    public void TryReload()
    {
        if (!isReloading && currentAmmo < magSize)
            StartCoroutine(ReloadRoutine());
    }
}
