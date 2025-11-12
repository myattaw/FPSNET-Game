using Unity.Netcode;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    public Gun gun;
    private bool isFiring = false;

    public void OnShoot()
    {
        if (!IsOwner) return; // Only the local player can fire
        isFiring = true;
    }

    public void OnShootRelease()
    {
        if (!IsOwner) return;
        isFiring = false;
    }

    public void OnReload()
    {
        if (!IsOwner) return;

        if (gun != null)
        {
            gun.TryReload();
        }
    }

    private void Update()
    {
        if (!IsOwner) return; // <- this is CRITICAL

        // Poll input (client side only)
        if (Input.GetButtonDown("Fire1"))
        {
            OnShoot();
        }
        if (Input.GetButtonUp("Fire1"))
        {
            OnShootRelease();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            OnReload();
        }

        if (isFiring && gun != null)
        {
            gun.Shoot(); // This internally calls a ServerRpc to spawn the bullet
        }
    }
}