using UnityEngine;

public class Atom : MonoBehaviour
{
    public Rigidbody2D Body { get; private set; }
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetColor(Color color)
    {
        spriteRenderer.color = color;
    }
}
