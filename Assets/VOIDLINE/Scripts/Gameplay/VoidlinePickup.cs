using UnityEngine;

[DisallowMultipleComponent]
public sealed class VoidlinePickup : MonoBehaviour
{
    private VoidlineGameManager gameManager;
    private float speed;
    private float baseY;
    private float phase;
    private bool resolved;
    private bool motionEnabled = true;

    public void Configure(VoidlineGameManager manager, float travelSpeed)
    {
        gameManager = manager;
        speed = travelSpeed;
        baseY = transform.position.y;
        phase = Random.Range(0f, Mathf.PI * 2f);
        resolved = false;
        motionEnabled = true;
    }

    public void SetMotionEnabled(bool enabled) => motionEnabled = enabled;

    private void Update()
    {
        if (!motionEnabled || resolved) return;
        if (gameManager != null) speed = gameManager.WorldSpeed;
        float dt = Time.deltaTime;
        transform.position += Vector3.back * speed * dt;
        phase += dt * 2.2f;
        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(phase) * 0.16f;
        transform.position = position;
        transform.Rotate(0f, 78f * dt, 24f * dt, Space.Self);
        if (transform.position.z < -10f)
        {
            resolved = true;
            if (gameManager != null) gameManager.ReturnPickup(this);
            else gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (resolved || other.GetComponentInParent<VoidlinePlayerController>() == null) return;
        resolved = true;
        if (gameManager != null) gameManager.CollectRepairPickup(this);
        else gameObject.SetActive(false);
    }
}
