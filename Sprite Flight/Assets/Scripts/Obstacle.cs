using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
public class Obstacle : MonoBehaviour
{
    public float minSize = 0.3f;
    public float maxSize = 2.0f;
    public float minSpeed = 150f;
    public float maxSpeed = 500f;
    public float maxSpinSpeed = 10f;
    public GameObject bounceEffectPrefab;
    Rigidbody2D rigidBody;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        float randomSize = Random.Range(minSize, maxSize);
        float randomSpeed = Random.Range(minSpeed, maxSpeed) / randomSize;
        Vector2 randomDirection = Random.insideUnitCircle;


        transform.localScale = new Vector3(randomSize, randomSize, 1);
        rigidBody = GetComponent<Rigidbody2D>();
        rigidBody.AddForce(randomDirection * randomSpeed);

        float randomTorque = Random.Range(-maxSpinSpeed, maxSpinSpeed);
        rigidBody.AddTorque(randomTorque);
    }

    // Update is called once per frame
    void Update()
    {

    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 contactPoint = collision.GetContact(0).point; 
        GameObject bounceEffect = Instantiate(bounceEffectPrefab, contactPoint, Quaternion.identity);

        // Destroy the effect after 1 second
        Destroy(bounceEffect, 1f);
    }
}
