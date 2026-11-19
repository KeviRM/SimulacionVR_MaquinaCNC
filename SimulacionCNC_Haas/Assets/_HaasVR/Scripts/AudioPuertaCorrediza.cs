using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioPuertaCorrediza : MonoBehaviour
{
    public AudioClip clipDeslizamientoLoop;
    public AudioClip clipGolpeTope; // Opcional: sonido corto al frenar la puerta
    public float volumenMaximo = 0.5f;
    public float velocidadMinima = 0.03f;

    private AudioSource fuente;
    private Vector3 posAnterior;
    private bool estabaMoviendose = false;

    private void Start()
    {
        fuente = GetComponent<AudioSource>();
        fuente.clip = clipDeslizamientoLoop;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f; // Sonido 3D saliendo de la puerta
        fuente.volume = 0f;

        if (clipDeslizamientoLoop != null)
            fuente.Play();

        posAnterior = transform.localPosition;
    }

    private void Update()
    {
        float velocidad = Vector3.Distance(transform.localPosition, posAnterior) / Mathf.Max(Time.deltaTime, 0.0001f);
        posAnterior = transform.localPosition;

        bool moviendose = velocidad > velocidadMinima;
        float objetivoVol = moviendose ? volumenMaximo : 0f;

        fuente.volume = Mathf.MoveTowards(fuente.volume, objetivoVol, Time.deltaTime * 6f);

        // Si acaba de frenar después de moverse, reproduce un pequeño golpe metálico
        if (estabaMoviendose && !moviendose && clipGolpeTope != null)
        {
            fuente.PlayOneShot(clipGolpeTope, volumenMaximo * 0.7f);
        }

        estabaMoviendose = moviendose;
    }
}