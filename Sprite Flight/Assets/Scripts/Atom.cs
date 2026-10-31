using UnityEngine;

public class Atom : MonoBehaviour
{
    public Rigidbody2D Body { get; private set; }
    public AtomSimulation Simulation { get; set; }
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Simulation.OnAtomCollision(this, collision);
    }

    public void SetColor(Color color)
    {
        spriteRenderer.color = color;
    }
}
