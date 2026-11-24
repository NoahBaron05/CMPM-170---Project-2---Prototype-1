using UnityEngine;

public class Bullet : MonoBehaviour
{
    Rigidbody2D bullet;
    [SerializeField] private float bulletSpeed;

    private void Awake()
    {
        bullet = GetComponent<Rigidbody2D>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bullet.linearVelocity = transform.up * bulletSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position.y > 10f)
        {
            Destroy(gameObject);
        }
    }
    private void onTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.name.StartsWith("Centipede Segment"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }
}
