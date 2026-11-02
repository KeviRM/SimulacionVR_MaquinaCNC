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


    [Header("5. Doble Puerta de la Cabina (Deslizador Cinemático Sin Parpadeo)")]
    public UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable grabPuertaIzquierda;
    public Transform puertaIzquierda;
    public Transform puertaDerecha;
    public float distanciaApertura = 0.28f; // Positivo si Izq abre hacia -X, negativo si abre hacia +X
    public bool sincronizarPuertasEnEspejo = true;

    private Vector3 posCerradaIzq;
    private Vector3 posCerradaDer;
    private bool puertasFueronAbiertas = false;

    // Variables para calcular el deslizamiento limpio con láser o mano
    private UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor interactorManija;
    private float xManoInicial;
    private float xPuertaAlAgarrar;

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
        if (puertaIzquierda != null) posCerradaIzq = puertaIzquierda.localPosition;
        if (puertaDerecha != null) posCerradaDer = puertaDerecha.localPosition;

        if (grabPuertaIzquierda != null)
        {
            // Evita que XR Toolkit mueva o desemparente la puerta por su cuenta
            grabPuertaIzquierda.trackPosition = false;
            grabPuertaIzquierda.trackRotation = false;
            grabPuertaIzquierda.retainTransformParent = true;

            grabPuertaIzquierda.selectEntered.AddListener(AlAgarrarManija);
            grabPuertaIzquierda.selectExited.AddListener(AlSoltarManija);
        }

        if (rendererPantallaCNC != null && matPantallaApagada != null)
            rendererPantallaCNC.material = matPantallaApagada;

        ActualizarTextoGuia("Paso 1: Ve atrás de la máquina y sube el interruptor MAIN POWER a ON.");
    }

    private void AlAgarrarManija(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (puertaIzquierda == null || args.interactorObject == null) return;

        interactorManija = args.interactorObject;
        // Usamos el sistema de coordenadas seguro (el padre si existe, o este mismo objeto)
        Transform espacioRef = puertaIzquierda.parent != null ? puertaIzquierda.parent : transform;

        xManoInicial = espacioRef.InverseTransformPoint(interactorManija.transform.position).x;
        xPuertaAlAgarrar = puertaIzquierda.localPosition.x;
    }

    private void AlSoltarManija(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        interactorManija = null;
    }

    private void Update()
    {
        if (puertaIzquierda == null) return;

        // 1. Si el jugador sujeta la manija, deslizar en X sin depender de .parent nulo
        if (interactorManija != null && interactorManija.transform != null)
        {
            Transform espacioRef = puertaIzquierda.parent != null ? puertaIzquierda.parent : transform;
            float xManoActual = espacioRef.InverseTransformPoint(interactorManija.transform.position).x;
            float deltaX = xManoActual - xManoInicial;

            float minX = Mathf.Min(posCerradaIzq.x, posCerradaIzq.x - distanciaApertura);
            float maxX = Mathf.Max(posCerradaIzq.x, posCerradaIzq.x - distanciaApertura);

            float nuevoX = Mathf.Clamp(xPuertaAlAgarrar + deltaX, minX, maxX);
            puertaIzquierda.localPosition = new Vector3(nuevoX, posCerradaIzq.y, posCerradaIzq.z);
        }

        // 2. Mover la puerta derecha en espejo (solo si está asignada)
        float desplazamientoIzq = Mathf.Abs(puertaIzquierda.localPosition.x - posCerradaIzq.x);
        if (sincronizarPuertasEnEspejo && puertaDerecha != null)
        {
            float direccionEspejo = Mathf.Sign(distanciaApertura);
            puertaDerecha.localPosition = new Vector3(
                posCerradaDer.x + (desplazamientoIzq * direccionEspejo),
                posCerradaDer.y,
                posCerradaDer.z
            );
        }

        // 3. Verificación del Paso 5: Abrir y volver a cerrar las puertas
        if (pasoActual == PasoEncendido.Paso5_CiclarPuertas)
        {
            if (!puertasFueronAbiertas && desplazamientoIzq >= Mathf.Abs(distanciaApertura) * 0.7f)
            {
                puertasFueronAbiertas = true;
                ActualizarTextoGuia("Paso 5: ¡Puertas abiertas! Ahora vuelve a cerrarlas.");
            }
            else if (puertasFueronAbiertas && desplazamientoIzq <= 0.04f)
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