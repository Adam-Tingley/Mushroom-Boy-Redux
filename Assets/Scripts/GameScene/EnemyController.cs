using UnityEngine;
using System.Collections;

public class BaseEnemy : MonoBehaviour
{
    public virtual void Die()
    {
        Debug.Log("Enemy unalived :O");
        Destroy(gameObject);
    }
}

public class EnemyController : BaseEnemy {

    private Rigidbody2D rb;
    public Transform[] patrolPoints;
    public float speed = 2f;
    private int currentPointIndex = 0;
    private bool isFrozen = false;
    private float originalGravityScale;
    public int health = 1;

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;
    }

    void Update() {
        if (!isFrozen) {
            Patrol();
        }
    }

    void Patrol() {
        if (patrolPoints.Length == 0) return;

        Transform targetPoint = patrolPoints[currentPointIndex];
        Vector2 direction = (targetPoint.position - transform.position).normalized;
        rb.velocity = direction * speed;

        if (Vector2.Distance(transform.position, targetPoint.position) < 0.1f) {
            currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        }
    }

    public void TakeDamage(int damage) { 
        health -= damage;
        if (health <= 0) {
            Die();
        }
    }

    public void Freeze(float time) {
        StartCoroutine(FreezeRoutine(time));
    }

    private IEnumerator FreezeRoutine(float time) {
        isFrozen = true;
        rb.gravityScale = 0;
        rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(time);
        rb.gravityScale = originalGravityScale;
        isFrozen = false;
    }
}