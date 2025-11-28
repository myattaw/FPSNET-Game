using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [Header("Gun Settings")]
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magSize = 20;

    [Header("References")]
    public GameObject bulletPrefab;
    public GameObject casingPrefab;
    public Transform bulletSpawn;
    public Transform casingEjectPoint;

    public Transform magazine; // Assign in inspector
    public Camera playerCamera;

    [Header("Audio")]
    public AudioSource gunAudioSource;    // Assign on gun model
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
        else
        {
            Debug.LogWarning("Magazine reference not assigned!");
        }

        // If remote player: force 3D gunshot audio
        if (!IsOwner && gunAudioSource != null)
        {
            gunAudioSource.spatialBlend = 1f; // 3D
        }
    }

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
        
        // Raycast for bullet direction
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            targetPoint = hit.point;
        else
            targetPoint = ray.GetPoint(1000f);

        Vector3 direction = (targetPoint - bulletSpawn.position).normalized;

        // Local-only casing effect
        if (casingPrefab && casingEjectPoint)
            EjectCasingLocal();

        // Server spawns bullet
        SpawnBulletServerRpc(bulletSpawn.position, direction);

        // ---- AUDIO: Tell server we're shooting ----
        if (IsServer)
            PlayShootSoundClientRpc();
        else
            PlayShootSoundServerRpc();
    }

    // ------------------- AUDIO RPCs --------------------

    [ServerRpc]
    private void PlayShootSoundServerRpc(ServerRpcParams rpcParams = default)
    {
        PlayShootSoundClientRpc();
    }

    [ClientRpc]
    private void PlayShootSoundClientRpc()
    {
        if (gunAudioSource != null && gunshotSound != null)
            gunAudioSource.PlayOneShot(gunshotSound, 0.5f);
    }

    [ServerRpc]
    private void PlayReloadSoundServerRpc(ServerRpcParams rpcParams = default)
    {
        PlayReloadSoundClientRpc();
    }

    [ClientRpc]
    private void PlayReloadSoundClientRpc()
    {
        if (gunAudioSource != null && reloadSound != null)
            gunAudioSource.PlayOneShot(reloadSound);
    }

    // --------------------------------------------------

    [ServerRpc]
    private void SpawnBulletServerRpc(Vector3 spawnPos, Vector3 dir, ServerRpcParams rpcParams = default)
    {
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        GameObject obj = Instantiate(bulletPrefab, spawnPos, rot);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

        Bullet b = obj.GetComponent<Bullet>();
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (b != null)
            b.ownerClientId = rpcParams.Receive.SenderClientId;

        if (rb != null)
        {
            rb.isKinematic = false;               // REQUIRED
            rb.linearVelocity = dir * b.speed;    // NEW PHYSICS STYLE
        }
    }

    private void EjectCasingLocal()
    {
        GameObject casing = Instantiate(casingPrefab, casingEjectPoint.position, casingEjectPoint.rotation);

        if (casing.TryGetComponent(out Rigidbody rb))
        {
            // ensure casing uses physics before assigning velocities
            rb.isKinematic = false;

            Vector3 ejectDir = (casingEjectPoint.right * 0.8f) + (casingEjectPoint.up * 0.4f);
            rb.linearVelocity = ejectDir.normalized * Random.Range(2f, 6f);

            rb.angularVelocity = new Vector3(
                Random.Range(-20f, 20f),
                Random.Range(-10f, 10f),
                Random.Range(-15f, 15f)
            );

            rb.transform.rotation *= Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f)
            );
        }

        Destroy(casing, 3f);
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        // ---- AUDIO: server tells everyone to play sound ----
        if (IsServer)
            PlayReloadSoundClientRpc();
        else
            PlayReloadSoundServerRpc();

        // Gun down animation
        Quaternion targetRot = Quaternion.Euler(originalRotation.eulerAngles + reloadRotationOffset);
        float halfTime = reloadTime / 2f;
        float elapsed = 0f;

        while (elapsed < halfTime)
        {
            transform.localRotation = Quaternion.Slerp(originalRotation, targetRot, elapsed / halfTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Magazine out/in animation
        if (magazine != null)
        {
            float magTime = 0.25f;

            // Drop
            elapsed = 0f;
            while (elapsed < magTime)
            {
                magazine.localPosition = Vector3.Lerp(magOriginalPos, magOriginalPos + magDropOffset, elapsed / magTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            magazine.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);

            // Insert
            magazine.gameObject.SetActive(true);
            elapsed = 0f;
            while (elapsed < magTime)
            {
                magazine.localPosition = Vector3.Lerp(magOriginalPos + magDropOffset, magOriginalPos, elapsed / magTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            magazine.localPosition = magOriginalPos;
        }

        // Gun up animation
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
