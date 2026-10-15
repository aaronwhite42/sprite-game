using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float minSize = 0.3f;
    public float maxSize = 2.0f;
    public float minSpeed = 150f;
    public float maxSpeed = 500f;
    public float maxSpinSpeed = 10f;
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
}
