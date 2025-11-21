using Unity.Netcode;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson; // for NetworkFirstPersonController

public class Bullet : NetworkBehaviour
{
    public float speed = 150f;
    public float lifeTime = 3f;
    public int damage = 25; // new: how much damage this bullet deals

    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearVelocity = transform.forward * speed; // fixed property

        if (IsServer)
            StartCoroutine(DespawnAfterSeconds(lifeTime));
    }

    private System.Collections.IEnumerator DespawnAfterSeconds(float secs)
    {
        yield return new WaitForSeconds(secs);
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!IsServer) return;
        // Try to find a player component on the hit object (or its parents)
        var hitPlayer = other.collider.GetComponentInParent<NetworkFirstPersonController>();
        if (hitPlayer != null)
        {
            // Apply damage server-side directly
            hitPlayer.ApplyDamage(damage);
        }

        // Despawn the bullet over the network
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}