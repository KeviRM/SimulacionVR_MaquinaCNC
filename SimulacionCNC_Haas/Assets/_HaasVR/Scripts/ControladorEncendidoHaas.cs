using System.Collections;
using UnityEngine;
using TMPro;

public class ControladorEncendidoHaas : MonoBehaviour
{
    public enum PasoEncendido
    {
        Paso1_SubirBreakerTrasero = 0,
        Paso2_AbrirValvulaAire = 1,
        Paso3_BotonPowerOn = 2,
        Paso4_LiberarEStop = 3,
        Paso5_CiclarPuertas = 4,
        Paso6_ResetAlarmas = 5,
        Paso7_ZeroReturnHome = 6,
        MaquinaLista = 7
    }

    [Header("Estado Actual")]
    public PasoEncendido pasoActual = PasoEncendido.Paso1_SubirBreakerTrasero;
    public TMP_Text textoGuiaUI;




    [Header("1-2. Gabinete Trasero (Breaker y Aire)")]
    public Transform palancaMainPower;
    public Transform palancaValvulaAire;
    public Transform agujaManometro;

    [Header("3-4. Panel Frontal y Pantalla")]
    public MeshRenderer rendererPantallaCNC;
    public Material matPantallaApagada;
    public Material matPantallaAlarma;
    public Material matPantallaLista;
    public Transform botonEStop;


    [Header("5. Doble Puerta de la Cabina (Con XR Simple Interactable)")]
    public UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable interactableManijaIzq;
    public Transform puertaIzquierda;
    public Transform puertaDerecha;
    public float distanciaApertura = 0.28f; // Positivo o negativo según hacia dónde abra
    public bool sincronizarPuertasEnEspejo = true;

    private Transform padreOriginalIzq;
    private Vector3 posCerradaIzq;
    private Vector3 posCerradaDer;
    private bool puertasFueronAbiertas = false;

    private UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor interactorManija;
    private Vector3 posManoInicialMundo;
    private float aperturaAlAgarrar;
    private float aperturaActual = 0f; // 0 = cerrada, distanciaApertura = abierta




    [Header("7. Ejes y Cubiertas Telescópicas (Home)")]
    public Transform cabezalEjeZ;
    public Transform mesaEjeX;
    public Transform[] placasTelescopicasZ;
    public float alturaZHome = 1.05f;
    public float alturaZBaseFija = 0.22f;

    [Header("Audio (Opcional)")]
    public AudioSource audioSource;
    public AudioClip sonidoBreaker;
    public AudioClip sonidoAire;
    public AudioClip sonidoVentiladorLoop;
    public AudioClip sonidoServosHome;




    private void Start()
    {
        if (puertaIzquierda != null)
        {
            padreOriginalIzq = puertaIzquierda.parent;
            posCerradaIzq = puertaIzquierda.localPosition;
        }
        if (puertaDerecha != null)
        {
            posCerradaDer = puertaDerecha.localPosition;
        }

        if (interactableManijaIzq != null)
        {
            interactableManijaIzq.selectEntered.AddListener(AlAgarrarManija);
            interactableManijaIzq.selectExited.AddListener(AlSoltarManija);
        }

        if (rendererPantallaCNC != null && matPantallaApagada != null)
            rendererPantallaCNC.material = matPantallaApagada;

        ActualizarTextoGuia("Paso 1: Ve atrás de la máquina y sube el interruptor MAIN POWER a ON.");
    }

    private void AlAgarrarManija(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (puertaIzquierda == null || args.interactorObject == null) return;

        interactorManija = args.interactorObject;
        posManoInicialMundo = interactorManija.transform.position;
        aperturaAlAgarrar = aperturaActual;
    }

    private void AlSoltarManija(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        interactorManija = null;
    }

    private void Update()
    {
        if (puertaIzquierda == null) return;

        // Blindaje: si por alguna razón se desemparentó, devolverla a la máquina de inmediato
        if (puertaIzquierda.parent != padreOriginalIzq)
            puertaIzquierda.SetParent(padreOriginalIzq, false);

        // 1. Calcular cuánto movió la mano el jugador a lo largo del eje horizontal de la máquina
        if (interactorManija != null && interactorManija.transform != null)
        {
            Vector3 desplazamientoMano = interactorManija.transform.position - posManoInicialMundo;
            // Proyectamos el movimiento de la mano sobre el eje X de la máquina (transform.right)
            float deltaRiel = Vector3.Dot(desplazamientoMano, transform.right);

            float minLim = Mathf.Min(0f, -distanciaApertura);
            float maxLim = Mathf.Max(0f, -distanciaApertura);

            aperturaActual = Mathf.Clamp(aperturaAlAgarrar + deltaRiel, minLim, maxLim);
        }

        // 2. Aplicar posición exacta sobre el riel local (nunca se sale de Y ni de Z)
        puertaIzquierda.localPosition = new Vector3(
            posCerradaIzq.x + aperturaActual,
            posCerradaIzq.y,
            posCerradaIzq.z
        );

        // 3. Mover Puerta Derecha en sentido opuesto (espejo)
        if (sincronizarPuertasEnEspejo && puertaDerecha != null)
        {
            puertaDerecha.localPosition = new Vector3(
                posCerradaDer.x - aperturaActual,
                posCerradaDer.y,
                posCerradaDer.z
            );
        }

        // 4. Validar Paso 5 (Abrir y volver a cerrar)
        float distRecorrida = Mathf.Abs(aperturaActual);
        if (pasoActual == PasoEncendido.Paso5_CiclarPuertas)
        {
            if (!puertasFueronAbiertas && distRecorrida >= Mathf.Abs(distanciaApertura) * 0.7f)
            {
                puertasFueronAbiertas = true;
                ActualizarTextoGuia("Paso 5: ¡Puertas abiertas! Ahora vuelve a cerrarlas.");
            }
            else if (puertasFueronAbiertas && distRecorrida <= 0.04f)
            {
                pasoActual = PasoEncendido.Paso6_ResetAlarmas;
                ActualizarTextoGuia("Paso 6: Presiona la tecla naranja RESET en el panel de control.");
            }
        }
    }






    // --- PASO 1: INTERRUPTOR TRASERO ---
    public void CompletarPaso1_Breaker()
    {
        if (pasoActual != PasoEncendido.Paso1_SubirBreakerTrasero) return;
        if (palancaMainPower != null)
            StartCoroutine(RotarSuave(palancaMainPower, Quaternion.Euler(-55f, 0f, 0f), 0.25f));

        ReproducirSonido(sonidoBreaker);
        pasoActual = PasoEncendido.Paso2_AbrirValvulaAire;
        ActualizarTextoGuia("Paso 2: Gira la llave azul trasera para abrir la presión de aire.");
    }

    // --- PASO 2: VÁLVULA DE AIRE AZUL ---
    public void CompletarPaso2_Aire()
    {
        if (pasoActual != PasoEncendido.Paso2_AbrirValvulaAire) return;
        if (palancaValvulaAire != null)
            StartCoroutine(RotarSuave(palancaValvulaAire, Quaternion.Euler(0f, 90f, 0f), 0.35f));
        if (agujaManometro != null)
            StartCoroutine(RotarSuave(agujaManometro, Quaternion.Euler(0f, -65f, 0f), 0.6f));

        ReproducirSonido(sonidoAire);
        pasoActual = PasoEncendido.Paso3_BotonPowerOn;
        ActualizarTextoGuia("Paso 3: Ve al frente y presiona el botón verde POWER ON.");
    }

    // --- PASO 3: BOTÓN VERDE POWER ON ---
    public void CompletarPaso3_PowerOn()
    {
        if (pasoActual != PasoEncendido.Paso3_BotonPowerOn) return;
        if (rendererPantallaCNC != null && matPantallaAlarma != null)
            rendererPantallaCNC.material = matPantallaAlarma;

        if (audioSource != null && sonidoVentiladorLoop != null)
        {
            audioSource.clip = sonidoVentiladorLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        pasoActual = PasoEncendido.Paso4_LiberarEStop;
        ActualizarTextoGuia("Paso 4: Libera el botón rojo de Paro de Emergencia (E-STOP).");
    }

    // --- PASO 4: LIBERAR PARO DE EMERGENCIA ---
    public void CompletarPaso4_EStop()
    {
        if (pasoActual != PasoEncendido.Paso4_LiberarEStop) return;
        if (botonEStop != null)
        {
            Vector3 posFinal = botonEStop.localPosition + new Vector3(0f, -0.015f, 0f);
            StartCoroutine(MoverYRotarSuave(botonEStop, posFinal, Quaternion.Euler(0f, 45f, 0f), 0.25f));
        }

        pasoActual = PasoEncendido.Paso5_CiclarPuertas;
        ActualizarTextoGuia("Paso 5: Abre y vuelve a cerrar las puertas de la cabina.");
    }

    // --- PASO 6: BOTÓN RESET ---
    public void CompletarPaso6_Reset()
    {
        if (pasoActual != PasoEncendido.Paso6_ResetAlarmas) return;
        if (rendererPantallaCNC != null && matPantallaLista != null)
            rendererPantallaCNC.material = matPantallaLista;

        pasoActual = PasoEncendido.Paso7_ZeroReturnHome;
        ActualizarTextoGuia("Paso 7: Presiona POWER UP / RESTART para enviar los ejes a Origen (Home).");
    }

    // --- PASO 7: MANDAR EJES A ORIGEN + CUBIERTAS TELESCÓPICAS ---
    public void CompletarPaso7_ZeroReturn()
    {
        if (pasoActual != PasoEncendido.Paso7_ZeroReturnHome) return;
        ReproducirSonido(sonidoServosHome);
        StartCoroutine(SecuenciaHomeEjes());
    }

    private IEnumerator SecuenciaHomeEjes()
    {
        ActualizarTextoGuia("Enviando ejes Z, X, Y a Origen (Home)...");
        float duracion = 2.5f;
        float t = 0f;

        Vector3 zInicio = cabezalEjeZ != null ? cabezalEjeZ.localPosition : Vector3.zero;
        Vector3 zFin = new Vector3(zInicio.x, zInicio.y, alturaZHome);

        Vector3 xInicio = mesaEjeX != null ? mesaEjeX.localPosition : Vector3.zero;
        Vector3 xFin = new Vector3(0f, xInicio.y, xInicio.z);

        while (t < duracion)
        {
            t += Time.deltaTime;
            float progreso = Mathf.SmoothStep(0f, 1f, t / duracion);

            if (cabezalEjeZ != null)
            {
                cabezalEjeZ.localPosition = Vector3.Lerp(zInicio, zFin, progreso);
                ActualizarCubiertasZ(cabezalEjeZ.localPosition.z);
            }
            if (mesaEjeX != null)
                mesaEjeX.localPosition = Vector3.Lerp(xInicio, xFin, progreso);

            yield return null;
        }

        pasoActual = PasoEncendido.MaquinaLista;
        ActualizarTextoGuia("¡Encendido listo! Máquina Haas VF-2 en Origen.");
    }

    public void ActualizarCubiertasZ(float zActualCabezal)
    {
        if (placasTelescopicasZ == null || placasTelescopicasZ.Length == 0) return;
        for (int i = 0; i < placasTelescopicasZ.Length; i++)
        {
            if (placasTelescopicasZ[i] == null) continue;
            float factor = (float)i / (placasTelescopicasZ.Length - 1);
            Vector3 p = placasTelescopicasZ[i].localPosition;
            p.z = Mathf.Lerp(zActualCabezal, alturaZBaseFija, factor);
            placasTelescopicasZ[i].localPosition = p;
        }
    }

    private IEnumerator RotarSuave(Transform obj, Quaternion rotFinal, float duracion)
    {
        Quaternion rotIni = obj.localRotation;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            obj.localRotation = Quaternion.Slerp(rotIni, rotFinal, t / duracion);
            yield return null;
        }
    }

    private IEnumerator MoverYRotarSuave(Transform obj, Vector3 posIniObj, Quaternion rotFinal, float duracion)
    {
        Vector3 posIni = obj.localPosition;
        Quaternion rotIni = obj.localRotation;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            obj.localPosition = Vector3.Lerp(posIni, posIniObj, t / duracion);
            obj.localRotation = Quaternion.Slerp(rotIni, rotFinal, t / duracion);
            yield return null;
        }
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void ActualizarTextoGuia(string mensaje)
    {
        Debug.Log("[Haas VR] " + mensaje);
        if (textoGuiaUI != null)
            textoGuiaUI.text = mensaje;
    }






}