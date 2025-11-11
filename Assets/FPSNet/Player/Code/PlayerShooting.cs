using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public Gun gun;
    private bool isFiring = false;

    public void onShoot()
    {
        isFiring = true;
        Debug.Log("Firing");
    }

    public void onShootRelease()
    {
        isFiring = false;
    }

    public void onReload()
    {
        if (gun != null)
        {
            gun.TryReload();
        }
    }


    // Update is called once per frame
    void Update()
    {
        // Poll input as a fallback (works with the old Input system)
        if (Input.GetButtonDown("Fire1"))
        {
            onShoot();
        }
        if (Input.GetButtonUp("Fire1"))
        {
            onShootRelease();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            onReload();
        }

        // Show whether we have a gun assigned
        if (isFiring)
        {
            if (gun != null)
            {
                gun.Shoot();
            }
            else
            {
                Debug.Log("Attempted to fire but gun is null");
            }
        }
    }
    
}