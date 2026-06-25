using System.Collections;
using UnityEngine;

/// <summary>
/// Restores one health tick to the player on contact. Add to EnemySpawner as an entry with a
/// low weight (e.g. 0.05) so it appears rarely alongside normal enemy spawns.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [SerializeField] float bounceHeight = 0.35f;
    [SerializeField] float bounceDuration = 0.45f;
    [SerializeField] float floatHeight = 0.15f;
    [SerializeField] float floatSpeed = 1.5f;

    [Tooltip("Played when the player picks this up. Leave empty for no sound.")]
    [SerializeField] AudioClip pickupSound;
    [Range(0f, 1f)] [SerializeField] float pickupVolume = 1f;

    Vector3 _restPos;
    bool _floating;

    void Start()
    {
        StartCoroutine(SpawnBounce());
    }

    void Update()
    {
        if (!_floating) return;
        transform.position = _restPos + Vector3.up * (Mathf.Sin(Time.time * floatSpeed * Mathf.PI * 2f) * floatHeight);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>()
            ?? other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // Persistent manager, so the sound still plays after we destroy ourselves. No-ops on a null
        // clip, so it stays silent until a clip is assigned.
        SfxManager.Play(pickupSound, pickupVolume);

        player.RestoreHealth();
        Destroy(gameObject);
    }

    IEnumerator SpawnBounce()
    {
        Vector3 origin = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / bounceDuration;
            transform.position = origin + Vector3.up * (Mathf.Sin(t * Mathf.PI) * bounceHeight);
            yield return null;
        }
        _restPos = origin;
        _floating = true;
    }
}
