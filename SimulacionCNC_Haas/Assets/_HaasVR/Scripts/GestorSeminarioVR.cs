using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class GestorSeminarioVR : MonoBehaviour
{
    public static GestorSeminarioVR Instancia { get; private set; }

    [Header("1. Panel Especial de Alertas (Rojo con Tiempo de Vida)")]
    [Tooltip("El objeto Panel/Imagen roja de fondo que se encenderá solo cuando haya una alerta")]
    public GameObject contenedorPanelAlerta;
    [Tooltip("El texto TMP dentro del panel rojo")]
    public TMP_Text textoAlertaUI;
    public float tiempoVidaAlerta = 3.5f;
    public AudioSource audioSourceUI;
    public AudioClip sonidoErrorAlerta;

    [Header("2. Métricas de Capacitación Industrial")]
    public int incidentesSeguridad = 0;

    [Header("3. Reinicio Instantáneo y Control de Presentador (PC y VR)")]
    public Key teclaReinicioPC = Key.Z;
    public Key teclaRecentrarJugadorPC = Key.C;
    public Key teclaSaltarEncendidoPC = Key.F1;

    [Tooltip(" Acción de botón en el control VR (ej. botón B o Y) mantenido 1.5s para reiniciar")]
    public InputActionProperty accionBotonReinicioVR;
    public float tiempoMantenerParaReiniciarVR = 1.5f;
    private float cronometroBotonVR = 0f;

    [Header("4. Referencias para Recentrar y Saltos de Etapa")]
    public Transform xrOriginJugador;
    public ControladorEncendidoHaas controladorEncendido;

    private Vector3 posInicialJugador;
    private Quaternion rotInicialJugador;
    private Coroutine corrutinaAlertaActiva;

    private void Awake()
    {
        Instancia = this;
        if (contenedorPanelAlerta != null)
            contenedorPanelAlerta.SetActive(false);
    }

    private void Start()
    {
        if (xrOriginJugador != null)
        {
            posInicialJugador = xrOriginJugador.position;
            rotInicialJugador = xrOriginJugador.rotation;
        }
    }

    private void Update()
    {
        // --- CONTROLES DE TECLADO EN PC PARA EL SEMINARIO ---
        if (Keyboard.current != null)
        {
            // Reiniciar escena al instante
            if (Keyboard.current[teclaReinicioPC].wasPressedThisFrame)
            {
                ReiniciarExperienciaInstantaneo();
            }

            // Recentrar al usuario frente a la CNC
            if (Keyboard.current[teclaRecentrarJugadorPC].wasPressedThisFrame)
            {
                RecentrarJugador();
            }

            // [F1] -> Saltar encendido y dejar la máquina lista para usar ATC / Prensa / Jog
            if (Keyboard.current[teclaSaltarEncendidoPC].wasPressedThisFrame)
            {
                SaltarAEstadoMaquinaLista();
            }
        }

        // --- BOTÓN DE REINICIO EN CONTROL VR (MANTENER 1.5 SEGUNDOS) ---
        if (accionBotonReinicioVR.action != null && accionBotonReinicioVR.action.ReadValue<float>() > 0.5f)
        {
            cronometroBotonVR += Time.deltaTime;
            if (cronometroBotonVR >= tiempoMantenerParaReiniciarVR)
            {
                cronometroBotonVR = 0f;
                ReiniciarExperienciaInstantaneo();
            }
        }
        else
        {
            cronometroBotonVR = 0f;
        }
    }

    // Llamar desde cualquier script cuando el usuario cometa un error de procedimiento
    public void MostrarAlertaTemporal(string mensajeError)
    {
        incidentesSeguridad++;

        if (audioSourceUI != null && sonidoErrorAlerta != null)
            audioSourceUI.PlayOneShot(sonidoErrorAlerta);

        if (corrutinaAlertaActiva != null)
            StopCoroutine(corrutinaAlertaActiva);

        corrutinaAlertaActiva = StartCoroutine(CorrutinaMostrarAlerta(mensajeError));
    }

    private IEnumerator CorrutinaMostrarAlerta(string mensaje)
    {
        if (textoAlertaUI != null)
            textoAlertaUI.text = mensaje;

        if (contenedorPanelAlerta != null)
            contenedorPanelAlerta.SetActive(true);

        yield return new WaitForSeconds(tiempoVidaAlerta);

        if (contenedorPanelAlerta != null)
            contenedorPanelAlerta.SetActive(false);

        corrutinaAlertaActiva = null;
    }

    public void ReiniciarExperienciaInstantaneo()
    {
        Scene escenaActual = SceneManager.GetActiveScene();
        SceneManager.LoadScene(escenaActual.buildIndex);
    }

    public void RecentrarJugador()
    {
        if (xrOriginJugador != null)
        {
            xrOriginJugador.position = posInicialJugador;
            xrOriginJugador.rotation = rotInicialJugador;
        }
    }

    public void SaltarAEstadoMaquinaLista()
    {
        if (controladorEncendido == null) return;

        controladorEncendido.pasoActual = ControladorEncendidoHaas.PasoEncendido.MaquinaLista;
        if (controladorEncendido.rendererPantallaCNC != null && controladorEncendido.matPantallaLista != null)
            controladorEncendido.rendererPantallaCNC.material = controladorEncendido.matPantallaLista;

        if (controladorEncendido.luzPuntualTorreta != null)
            controladorEncendido.luzPuntualTorreta.enabled = false;

        if (controladorEncendido.textoGuiaUI != null)
            controladorEncendido.textoGuiaUI.text = "MODO DEMO ACTIVO (Máquina Lista):\nPuedes usar directamente el cambio de herramienta ATC, la Prensa Kurt o el Handle Jog.";

        controladorEncendido.CambiarObjetivoGuiaPublico(null);
    }
}