using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AtomSimulation : MonoBehaviour
{
    public Atom atomPrefab;
    public int atomCount = 80;

    // Inner edges of these walls define the container
    public BoxCollider2D wallLeft;
    public BoxCollider2D wallRight;
    public BoxCollider2D wallTop;
    public BoxCollider2D wallBottom;

    public float minTemperature = 50f;
    public float maxTemperature = 1000f;
    // Temperature of the walls; atoms that hit a wall leave with a speed drawn from this temperature
    public float temperature = 300f;

    // RMS speed (units/sec) at the reference temperature; speed scales with sqrt(T)
    public float referenceTemperature = 300f;
    public float referenceRmsSpeed = 6f;

    // Lennard-Jones well depth ε, expressed as a temperature (ε / k). Atoms condense at roughly 0.45 × this.
    public float attractionDepth = 600f;
    // Pair forces are ignored beyond this many σ
    public float attractionRange = 2.5f;

    // Downward acceleration on atoms (units/sec²). Gas thins out with height over kT/(mg), about
    // 18 units at 300 K with the default, so the gas still fills the box while clusters sink to the floor.
    public float gravity = 1f;

    // Time constant (seconds) for smoothing the displayed gas temperature
    public float gasTemperatureSmoothing = 0.5f;

    // Slow atoms map to the left of the gradient, atoms at maxColorSpeed to the right
    public Gradient speedColors = DefaultSpeedColors();
    public float maxColorSpeed = 15f;

    // Wall tint from minTemperature (left of the gradient) to maxTemperature (right)
    public Gradient wallColors = DefaultWallColors();

    public UIDocument uiDocument;

    private readonly List<Atom> atoms = new List<Atom>();
    private Label temperatureLabel;
    private float gasTemperature;
    private SpriteRenderer[] wallRenderers;
    private float contactDistance;
    private Vector2[] positions = new Vector2[0];
    private Vector2[] forces = new Vector2[0];

    void Start()
    {
        SpawnAtoms();

        wallRenderers = new[]
        {
            wallLeft.GetComponent<SpriteRenderer>(),
            wallRight.GetComponent<SpriteRenderer>(),
            wallTop.GetComponent<SpriteRenderer>(),
            wallBottom.GetComponent<SpriteRenderer>(),
        };

        temperatureLabel = uiDocument.rootVisualElement.Q<Label>("TemperatureLabel");
        Slider temperatureSlider = uiDocument.rootVisualElement.Q<Slider>("TemperatureSlider");
        temperatureSlider.lowValue = minTemperature;
        temperatureSlider.highValue = maxTemperature;
        temperatureSlider.value = temperature;
        temperatureSlider.RegisterValueChangedCallback(evt => temperature = evt.newValue);
    }

    void SpawnAtoms()
    {
        Rect container = Rect.MinMaxRect(
            wallLeft.bounds.max.x, wallBottom.bounds.max.y,
            wallRight.bounds.min.x, wallTop.bounds.min.y);
        float radius = atomPrefab.transform.localScale.x * 0.5f;
        contactDistance = radius * 2f;
        float rmsSpeed = RmsSpeedAt(temperature);

        for (int i = 0; i < atomCount; i++)
        {
            Vector2 position = FindFreeSpot(container, radius);
            Atom atom = Instantiate(atomPrefab, position, Quaternion.identity, transform);
            atom.Simulation = this;
            atom.Body.gravityScale = gravity / Physics2D.gravity.magnitude;
            // Start every atom at the same speed; collisions spread it into a Maxwell-Boltzmann distribution
            atom.Body.linearVelocity = Random.insideUnitCircle.normalized * rmsSpeed;
            atoms.Add(atom);
        }

        // Atoms repel each other through the Lennard-Jones force, not the physics engine: the contact
        // solver loses energy when many bodies touch at once, which drains heat out of clusters.
        // Their colliders are kept only for bouncing off the walls.
        for (int i = 0; i < atoms.Count; i++)
        {
            for (int j = i + 1; j < atoms.Count; j++)
            {
                Physics2D.IgnoreCollision(atoms[i].GetComponent<Collider2D>(), atoms[j].GetComponent<Collider2D>());
            }
        }
        gasTemperature = temperature;
    }

    Vector2 FindFreeSpot(Rect container, float radius)
    {
        Vector2 position = Vector2.zero;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            position = new Vector2(
                Random.Range(container.xMin + radius, container.xMax - radius),
                Random.Range(container.yMin + radius, container.yMax - radius));

            bool overlaps = false;
            foreach (Atom other in atoms)
            {
                if (Vector2.Distance(position, other.transform.position) < radius * 2f)
                {
                    overlaps = true;
                    break;
                }
            }
            if (!overlaps)
            {
                break;
            }
        }
        return position;
    }

    void FixedUpdate()
    {
        if (atoms.Count == 0)
        {
            return;
        }

        ApplyLennardJones();

        // All atoms share the same mass, so mean squared speed stands in for kinetic energy
        float meanSquaredSpeed = 0f;
        foreach (Atom atom in atoms)
        {
            meanSquaredSpeed += atom.Body.linearVelocity.sqrMagnitude;
        }
        meanSquaredSpeed /= atoms.Count;

        float measured = TemperatureAt(meanSquaredSpeed);
        float blend = 1f - Mathf.Exp(-Time.fixedDeltaTime / gasTemperatureSmoothing);
        gasTemperature = Mathf.Lerp(gasTemperature, measured, blend);
    }

    // Lennard-Jones force between atom pairs: repulsive up close, attractive further out. σ puts the
    // potential minimum exactly where the sprites touch.
    // The repulsive wall is stiff: a hard collision at 1000 K vibrates at ω ≈ 1000 rad/s, so Fixed
    // Timestep must stay around 1 ms or collisions gain energy and heat the gas.
    void ApplyLennardJones()
    {
        float epsilon = BoltzmannConstant() * attractionDepth;
        float sigma = contactDistance / Mathf.Pow(2f, 1f / 6f);
        float sqrSigma = sigma * sigma;
        // Caps the force if two atoms ever get pushed deep into each other
        float sqrClosest = 0.8f * 0.8f * sqrSigma;
        float sqrCutoff = attractionRange * attractionRange * sqrSigma;
        // Shift the force so it falls to exactly zero at the cutoff instead of jumping there
        float cutoffForce = LennardJonesForceOverR(sqrSigma, sqrCutoff);

        if (positions.Length != atoms.Count)
        {
            positions = new Vector2[atoms.Count];
            forces = new Vector2[atoms.Count];
        }
        for (int i = 0; i < atoms.Count; i++)
        {
            positions[i] = atoms[i].Body.position;
            forces[i] = Vector2.zero;
        }

        for (int i = 0; i < atoms.Count; i++)
        {
            for (int j = i + 1; j < atoms.Count; j++)
            {
                Vector2 offset = positions[j] - positions[i];
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > sqrCutoff)
                {
                    continue;
                }
                sqrDistance = Mathf.Max(sqrDistance, sqrClosest);

                float forceOverR = LennardJonesForceOverR(sqrSigma, sqrDistance)
                    - cutoffForce * Mathf.Sqrt(sqrCutoff / sqrDistance);
                Vector2 force = epsilon * forceOverR * offset;
                forces[i] -= force;
                forces[j] += force;
            }
        }

        for (int i = 0; i < atoms.Count; i++)
        {
            atoms[i].Body.AddForce(forces[i]);
        }
    }

    // F(r)/(ε·r) = 24/r² · (2(σ/r)^12 − (σ/r)^6); positive (pushing apart) inside the minimum, negative beyond it
    static float LennardJonesForceOverR(float sqrSigma, float sqrDistance)
    {
        float s6 = Mathf.Pow(sqrSigma / sqrDistance, 3);
        return 24f * (2f * s6 * s6 - s6) / sqrDistance;
    }

    // Called by an atom after the physics engine has bounced it off something
    public void OnAtomCollision(Atom atom, Collision2D collision)
    {
        if (!IsWall(collision.collider))
        {
            return;
        }

        // Thermal wall: the atom forgets its incoming velocity and leaves with one drawn from
        // the wall's temperature. The normal component follows a Rayleigh distribution (the flux
        // of atoms leaving a surface), the tangential component a Gaussian.
        Vector2 normal = collision.GetContact(0).normal;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        float sigma = RmsSpeedAt(temperature) / Mathf.Sqrt(2f);
        float normalSpeed = sigma * Mathf.Sqrt(-2f * Mathf.Log(1f - Random.value));
        float tangentialSpeed = sigma * Gaussian();
        atom.Body.linearVelocity = normal * normalSpeed + tangent * tangentialSpeed;
    }

    bool IsWall(Collider2D other)
    {
        return other == wallLeft || other == wallRight || other == wallTop || other == wallBottom;
    }

    void Update()
    {
        foreach (Atom atom in atoms)
        {
            float speed = atom.Body.linearVelocity.magnitude;
            atom.SetColor(speedColors.Evaluate(speed / maxColorSpeed));
        }

        Color wallColor = wallColors.Evaluate(Mathf.InverseLerp(minTemperature, maxTemperature, temperature));
        foreach (SpriteRenderer wallRenderer in wallRenderers)
        {
            wallRenderer.color = wallColor;
        }

        temperatureLabel.text = "Walls: " + Mathf.RoundToInt(temperature) + " K    Gas: " + Mathf.RoundToInt(gasTemperature) + " K";
    }

    float RmsSpeedAt(float kelvin)
    {
        return referenceRmsSpeed * Mathf.Sqrt(kelvin / referenceTemperature);
    }

    // Inverse of RmsSpeedAt
    float TemperatureAt(float meanSquaredSpeed)
    {
        return referenceTemperature * meanSquaredSpeed / (referenceRmsSpeed * referenceRmsSpeed);
    }

    // Energy per kelvin in simulation units. In 2D the mean kinetic energy per atom is kT.
    float BoltzmannConstant()
    {
        return atoms[0].Body.mass * referenceRmsSpeed * referenceRmsSpeed / (2f * referenceTemperature);
    }

    // Standard normal sample (Box-Muller)
    static float Gaussian()
    {
        float u1 = 1f - Random.value;
        float u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    static Gradient DefaultSpeedColors()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.15f, 0.35f, 1f), 0f),
                new GradientColorKey(new Color(0.95f, 0.95f, 0.95f), 0.5f),
                new GradientColorKey(new Color(1f, 0.2f, 0.1f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    static Gradient DefaultWallColors()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.1f, 0.05f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
