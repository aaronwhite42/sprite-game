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
    public float temperature = 300f;

    // RMS speed (units/sec) at the reference temperature; speed scales with sqrt(T)
    public float referenceTemperature = 300f;
    public float referenceRmsSpeed = 6f;

    // How quickly the thermostat pulls the gas toward the target temperature
    public float thermostatRate = 2f;

    // Slow atoms map to the left of the gradient, atoms at maxColorSpeed to the right
    public Gradient speedColors = DefaultSpeedColors();
    public float maxColorSpeed = 15f;

    public UIDocument uiDocument;

    private readonly List<Atom> atoms = new List<Atom>();
    private Label temperatureLabel;

    void Start()
    {
        SpawnAtoms();

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
        float rmsSpeed = RmsSpeedAt(temperature);

        for (int i = 0; i < atomCount; i++)
        {
            Vector2 position = FindFreeSpot(container, radius);
            Atom atom = Instantiate(atomPrefab, position, Quaternion.identity, transform);
            // Start every atom at the same speed; collisions spread it into a Maxwell-Boltzmann distribution
            atom.Body.linearVelocity = Random.insideUnitCircle.normalized * rmsSpeed;
            atoms.Add(atom);
        }
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

        // All atoms share the same mass, so mean squared speed stands in for kinetic energy
        float meanSquaredSpeed = 0f;
        foreach (Atom atom in atoms)
        {
            meanSquaredSpeed += atom.Body.linearVelocity.sqrMagnitude;
        }
        meanSquaredSpeed /= atoms.Count;
        if (meanSquaredSpeed < 0.0001f)
        {
            return;
        }

        float targetRmsSpeed = RmsSpeedAt(temperature);
        float fullCorrection = targetRmsSpeed / Mathf.Sqrt(meanSquaredSpeed);
        float blend = 1f - Mathf.Exp(-thermostatRate * Time.fixedDeltaTime);
        float correction = Mathf.Lerp(1f, fullCorrection, blend);

        foreach (Atom atom in atoms)
        {
            atom.Body.linearVelocity *= correction;
        }
    }

    void Update()
    {
        foreach (Atom atom in atoms)
        {
            float speed = atom.Body.linearVelocity.magnitude;
            atom.SetColor(speedColors.Evaluate(speed / maxColorSpeed));
        }

        temperatureLabel.text = "Temperature: " + Mathf.RoundToInt(temperature) + " K";
    }

    float RmsSpeedAt(float kelvin)
    {
        return referenceRmsSpeed * Mathf.Sqrt(kelvin / referenceTemperature);
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
}
