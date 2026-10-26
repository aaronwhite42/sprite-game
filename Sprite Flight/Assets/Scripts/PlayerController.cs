using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
public class PlayerController : MonoBehaviour
{

    public float thrustForce = 1f;
    public float maxSpeed = 5f;

    public GameObject boosterFlame;

    public UIDocument uiDocument;
    public GameObject explosionEffect;

    private Rigidbody2D rb;
    private float elapsedTime = 0f;
    private float score = 0f;
    public float scoreMultiplier = 10f;
    private Label scoreText;
    private Button restartButton;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        scoreText = uiDocument.rootVisualElement.Q<Label>("ScoreLabel");
        restartButton = uiDocument.rootVisualElement.Q<Button>("RestartButton");
        restartButton.style.display = DisplayStyle.None;
        restartButton.clicked += ReloadScene;
    }

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        score = Mathf.FloorToInt(elapsedTime * scoreMultiplier);
        scoreText.text = "Score: " + score;

        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.value);
            Vector2 direction = (mousePos - transform.position).normalized;
            transform.up = direction;
            rb.AddForce(direction.normalized * thrustForce);
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            boosterFlame.SetActive(true);
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            boosterFlame.SetActive(false);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Instantiate(explosionEffect, transform.position, transform.rotation);
        Destroy(gameObject);
        restartButton.style.display = DisplayStyle.Flex;
    }

    void ReloadScene()
    {
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
