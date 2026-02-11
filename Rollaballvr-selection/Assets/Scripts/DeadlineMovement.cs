using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class DeadlineMovement : MonoBehaviour
{
    public Transform player;
    public GameObject destructionEffect;
    public float lifetimeSeconds = 60f;
    public ParticleSystem despawnParticles;
    public float despawnDelay = 1.5f;
    public float dissolveSpeed = 1f;

    private NavMeshAgent navMeshAgent;
    private Material material;
    private bool isDying = false;
    private float dissolveAmount = 0f;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        // Get the material from the child mesh (Meshy_AI_Deadline_Face...)
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(DespawnAfterLifetime());
    }

    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetimeSeconds);

        if (despawnParticles != null)
        {
            despawnParticles.transform.SetParent(null, true);
            despawnParticles.Play();
            Destroy(despawnParticles.gameObject, despawnDelay);
        }

        Destroy(gameObject);
    }

    void Update()
    {
        if (isDying)
        {
            dissolveAmount += Time.deltaTime * dissolveSpeed;
            material.SetFloat("_DissolveAmount", dissolveAmount);

            if (dissolveAmount >= 1f)
            {
                Destroy(gameObject);
            }
            return; // Stop moving when dying
        }

        if (player != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.SetDestination(player.position);
        }
    }

    public void Die()
    {
        if (isDying) return;
        isDying = true;
        navMeshAgent.isStopped = true; // Stop moving
    }

    private void OnDestroy()
    {
        if (destructionEffect != null && gameObject.scene.isLoaded)
        {
            GameObject fx = Instantiate(destructionEffect, transform.position, transform.rotation);
            Destroy(fx, 2.0f);
        }
    }
}