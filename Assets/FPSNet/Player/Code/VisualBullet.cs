using UnityEngine;

public class VisualBullet : MonoBehaviour
{
    public float speed = 150f;
    public float lifeTime = 3f;
    public GameObject impactVFX;

    private Vector3 dir;
    private Vector3 targetPoint;
    private bool useTarget = false;
    private float traveled = 0f;
    private float maxDistance = 1000f;

    // Initialize to move along a direction for lifeTime
    public void InitializeDirection(Vector3 direction, float spd, float lt)
    {
        dir = direction.normalized;
        speed = spd;
        lifeTime = lt;
        useTarget = false;
        maxDistance = speed * lifeTime;
        Destroy(gameObject, lifeTime + 0.1f);
    }

    // Initialize to move toward a specific target point (impact)
    public void InitializeToTarget(Vector3 hitPoint, float spd, float lt)
    {
        targetPoint = hitPoint;
        speed = spd;
        lifeTime = lt;
        useTarget = true;
        dir = (hitPoint - transform.position).normalized;
        maxDistance = Vector3.Distance(transform.position, hitPoint);
        Destroy(gameObject, lifeTime + 0.1f);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        traveled += step;

        // rotate to face movement direction
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        if (useTarget)
        {
            // reached impact point
            if (Vector3.Distance(transform.position, targetPoint) <= 0.25f || traveled >= maxDistance)
            {
                OnImpact();
            }
        }
        else
        {
            if (traveled >= maxDistance)
                OnImpact();
        }
    }

    private void OnImpact()
    {
        if (impactVFX != null)
            Instantiate(impactVFX, transform.position, Quaternion.LookRotation(-dir));

        Destroy(gameObject);
    }
}

