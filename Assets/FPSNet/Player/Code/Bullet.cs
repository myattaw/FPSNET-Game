using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public class Bullet : NetworkBehaviour
{
    public float speed = 150f;
    public float lifeTime = 3f;
    public int damage = 25;

    public ulong ownerClientId = ulong.MaxValue;

    private Rigidbody rb;
    private Collider col;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // New physics: MUST disable kinematic before setting linearVelocity
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }

        if (IsServer)
            StartCoroutine(DespawnAfterSeconds(lifeTime));
    }

    private IEnumerator DespawnAfterSeconds(float secs)
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

        if (col != null) col.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            // rb.linearVelocity = Vector3.zero;
        }

        var hit = other.collider.GetComponentInParent<NetworkFirstPersonController>();
        if (hit != null && hit.OwnerClientId != ownerClientId)
        {
            hit.ApplyDamage(damage, ownerClientId);
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}