using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 1.5f;

    private float fireTimer;

    void Update()
    {
        fireTimer += Time.deltaTime;

        if (fireTimer >= fireRate)
        {
            Shoot();
            fireTimer = 0f;
        }
    }

    void Shoot()
    {
        float randomAngle = Random.Range(-60f, 60f);

        Quaternion bulletRotation = Quaternion.Euler(0f, 0f, -randomAngle);

        Instantiate(bulletPrefab, transform.position, bulletRotation);
    }
}
