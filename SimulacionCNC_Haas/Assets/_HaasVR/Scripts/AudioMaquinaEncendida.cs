using System.Collections;
using UnityEngine;

public class AudioMaquinaEncendida : MonoBehaviour
{
    [Header("AudioSource con el ruido continuo (Loop = true, PlayOnAwake = false)")]
    public AudioSource fuenteLoopMaquina;
    public float volumenObjetivoLoop = 0.45f;
    public float duracionFadeIn = 1.8f;

    [Header("Sonido único de arranque (relé / ventiladores arrancando)")]
    public AudioSource fuenteEfectos;
    public AudioClip clipArranqueMaquina;

    private bool estaEncendida = false;

    public void EncenderAudioMaquina()
    {
        if (estaEncendida) return;
        estaEncendida = true;

        if (fuenteEfectos != null && clipArranqueMaquina != null)
            fuenteEfectos.PlayOneShot(clipArranqueMaquina);

        if (fuenteLoopMaquina != null)
        {
            fuenteLoopMaquina.loop = true;
            fuenteLoopMaquina.volume = 0f;
            fuenteLoopMaquina.Play();
            StartCoroutine(FadeVolumen(volumenObjetivoLoop, duracionFadeIn));
        }
    }

    public void ApagarAudioMaquina()
    {
        if (!estaEncendida) return;
        estaEncendida = false;

        if (fuenteLoopMaquina != null)
            StartCoroutine(FadeVolumen(0f, 0.6f, detenerAlFinal: true));
    }

    private IEnumerator FadeVolumen(float destino, float duracion, bool detenerAlFinal = false)
    {
        float inicio = fuenteLoopMaquina.volume;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            fuenteLoopMaquina.volume = Mathf.Lerp(inicio, destino, t / duracion);
            yield return null;
        }
        fuenteLoopMaquina.volume = destino;
        if (detenerAlFinal) fuenteLoopMaquina.Stop();
    }
}