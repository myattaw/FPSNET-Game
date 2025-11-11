using System.Collections;
using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header("Gun Settings")]
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magSize = 20;

    [Header("References")]
    public GameObject bullet;
    public Transform bulletSpawn;
    public Transform magazine; // Assign your "Magazine" child in Inspector

    private int currentAmmo;
    private bool isReloading = false;
    private float nextTimeToFire = 0f;

    private Quaternion originalRotation;
    private Vector3 originalPosition;

    private Vector3 reloadRotationOffset = new Vector3(60f, 50f, 50f);

    // Magazine animation settings
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
        if (isReloading)
            return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + fireRate;
            Instantiate(bullet, bulletSpawn.position, bulletSpawn.rotation);
            currentAmmo--;
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

            // Simulate removing the mag
            magazine.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);

            // Simulate inserting a new mag
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

        // --- Rotate gun back to normal ---
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
