using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private float doubleShotSpread = 8f;
    [SerializeField, Min(1)] private int maxBulletsPerShot = 8;

    private static int bulletsPerShot = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBulletCount()
    {
        bulletsPerShot = 1;
    }

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
        float spread = doubleShotSpread * (bulletsPerShot - 1);
        float startAngle = randomAngle - spread * 0.5f;

        for (int i = 0; i < bulletsPerShot; i++)
        {
            float bulletAngle = startAngle + doubleShotSpread * i;
            Quaternion bulletRotation = Quaternion.Euler(0f, 0f, -bulletAngle);
            Instantiate(bulletPrefab, transform.position, bulletRotation);
        }
    }

    public void DoubleBulletCount()
    {
        bulletsPerShot = Mathf.Min(bulletsPerShot * 2, Mathf.Max(1, maxBulletsPerShot));
    }
}
