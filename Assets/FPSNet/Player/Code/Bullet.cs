using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    public float speed = 15f;
    public float lifeTime = 3f;

    private Rigidbody rigidbody;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        rigidbody.linearVelocity = -transform.right * speed;
        Destroy(gameObject, lifeTime);
    }


    private void OnCollisionEnter(Collision other)
    {
        Destroy(gameObject);
    }
}
