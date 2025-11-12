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
    public Transform bulletSpawn;
    public Transform magazine; // Assign your "Magazine" child in Inspector
    public Camera playerCamera;

    private int currentAmmo;
    private bool isReloading = false;
    private float nextTimeToFire = 0f;

    // --- Gun rotation animation ---
    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private Vector3 reloadRotationOffset = new Vector3(60f, 50f, 50f);

    // --- Magazine animation ---
    private Vector3 magOriginalPos;
    private Quaternion magOriginalRot;
    private Vector3 magDropOffset = new Vector3(0f, -0.3f, 0f); // how far it drops

    void Start()
    {
        currentAmmo = magSize;
        originalRotation = transform.localRotation;
        originalPosition = transform.localPosition;

        if (magazine != null)
        {
            magOriginalPos = magazine.localPosition;
            magOriginalRot = magazine.localRotation;
        }
        else
        {
            Debug.LogWarning("Magazine reference not assigned in inspector!");
        }
    }

    public void Shoot()
    {
        if (!IsOwner || isReloading) return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Time.time < nextTimeToFire) return;

        nextTimeToFire = Time.time + fireRate;
        currentAmmo--;

        // Raycast from center of screen to find target point
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            targetPoint = hit.point;
        else
            targetPoint = ray.GetPoint(1000f);

        // Get the direction from barrel to that target
        Vector3 direction = (targetPoint - bulletSpawn.position).normalized;

        // Call server to spawn bullet in that direction
        SpawnBulletServerRpc(bulletSpawn.position, direction);
    }


    [ServerRpc]
    private void SpawnBulletServerRpc(Vector3 spawnPos, Vector3 forwardDir)
    {
        // Create bullet facing the correct direction
        Quaternion rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, rotation);

        // Spawn it over the network
        NetworkObject netObj = bulletObj.GetComponent<NetworkObject>();
        netObj.Spawn(true);

        // --- Access the Bullet script ---
        if (bulletObj.TryGetComponent(out Bullet bulletScript))
        {
            float bulletSpeed = bulletScript.speed;
            if (bulletObj.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = forwardDir * bulletSpeed;
            }
        }
        else
        {
            Debug.LogWarning("Spawned bullet prefab missing Bullet script!");
        }
    }
    

    private IEnumerator Reload()
    {
        isReloading = true;

        Quaternion targetRotation = Quaternion.Euler(originalRotation.eulerAngles + reloadRotationOffset);
        float halfReloadTime = reloadTime / 2f;

        // --- Rotate gun downward ---
        float elapsedTime = 0f;
        while (elapsedTime < halfReloadTime)
        {
            transform.localRotation = Quaternion.Slerp(originalRotation, targetRotation, elapsedTime / halfReloadTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // --- Animate magazine detach/drop ---
        if (magazine != null)
        {
            float magAnimTime = 0.25f;
            elapsedTime = 0f;
            while (elapsedTime < magAnimTime)
            {
                magazine.localPosition = Vector3.Lerp(magOriginalPos, magOriginalPos + magDropOffset, elapsedTime / magAnimTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Simulate removing mag
            magazine.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);

            // Simulate inserting mag
            magazine.gameObject.SetActive(true);
            elapsedTime = 0f;
            while (elapsedTime < magAnimTime)
            {
                magazine.localPosition = Vector3.Lerp(magOriginalPos + magDropOffset, magOriginalPos, elapsedTime / magAnimTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            magazine.localPosition = magOriginalPos;
        }

        // --- Rotate gun back up ---
        elapsedTime = 0f;
        while (elapsedTime < halfReloadTime)
        {
            transform.localRotation = Quaternion.Slerp(targetRotation, originalRotation, elapsedTime / halfReloadTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentAmmo = magSize;
        isReloading = false;
    }

    public void TryReload()
    {
        if (!isReloading && currentAmmo < magSize)
        {
            StartCoroutine(Reload());
        }
    }
}
