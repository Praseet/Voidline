using UnityEngine;

[DisallowMultipleComponent]
public sealed class VoidlineAsteroid : MonoBehaviour
{
    private VoidlineGameManager gameManager;
    private float speed;
    private float spinRate;
    private bool motionEnabled = true;
    private bool resolved;
    private bool nearMissChecked;

    public void Configure(VoidlineGameManager manager, float travelSpeed, float size)
    {
        gameManager = manager;
        speed = travelSpeed;
        spinRate = Random.Range(25f, 85f) * (Random.value < 0.5f ? -1f : 1f);
        resolved = false;
        nearMissChecked = false;
        motionEnabled = true;
        transform.localScale = new Vector3(size * 1.05f, size * 0.9f, size);
        transform.rotation = Random.rotation;
    }

    public void SetMotionEnabled(bool enabled) => motionEnabled = enabled;

    private void Update()
    {
        if (!motionEnabled || resolved) return;
        if (gameManager != null) speed = gameManager.WorldSpeed;
        float dt = Time.deltaTime;
        float previousZ = transform.position.z;
        transform.position += Vector3.back * speed * dt;
        transform.Rotate(spinRate * dt, spinRate * 0.63f * dt, spinRate * 0.37f * dt, Space.Self);
        float currentZ = transform.position.z;
        // Score only after clearing the ship collision envelope. A real collision returns
        // this pooled actor first, so it cannot be paid as both a hit and a near miss.
        if (!nearMissChecked && previousZ > -2.4f && currentZ <= -2.4f)
        {
            nearMissChecked = true;
            if (gameManager != null) gameManager.RegisterNearMiss(transform.position);
        }
        if (currentZ < -10f)
        {
            resolved = true;
            if (gameManager != null) gameManager.RegisterAsteroidPassed();
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (resolved) return;
        VoidlinePlayerController controller = other.GetComponentInParent<VoidlinePlayerController>();
        if (controller == null) return;
        resolved = true;
        if (gameManager != null)
        {
            if (controller.IsDodgeActive) gameManager.RegisterAsteroidDodged();
            else gameManager.TakeDamage();
        }
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        motionEnabled = false;
        if (gameManager != null) gameManager.ReturnAsteroid(this);
        else gameObject.SetActive(false);
    }
}
