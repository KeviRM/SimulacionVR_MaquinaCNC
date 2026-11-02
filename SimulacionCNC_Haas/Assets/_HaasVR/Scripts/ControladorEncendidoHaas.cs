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
    public float distanciaApertura = 0.28f; 
    public bool sincronizarPuertasEnEspejo = true;

    private Transform padreOriginalIzq;
    private Vector3 posCerradaIzq;
    private Vector3 posCerradaDer;
    private bool puertasFueronAbiertas = false;

    private UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor interactorManija;
    private Vector3 posManoInicialMundo;
    private float aperturaAlAgarrar;
    private float aperturaActual = 0f; // 0 = cerrada, distanciaApertura = abierta






    [Header("7. Ejes y Cubiertas Telescópicas (Home en Ejes de Unity)")]
    public Transform cabezalEjeZ;
    public Transform[] placasTelescopicasCabezal; // De arriba (pegada al cabezal) hacia abajo
    public float desplazamientoCabezalHomeY = 0.18f; // Flecha Verde en Unity (Subir cabezal)

    public Transform mesaEjeX;
    public Transform[] placasInferioresMesa; // De arriba (bajo la mesa) hacia abajo
    public float desplazamientoMesaHomeX = 0.25f; // Flecha Roja en Unity (Mover mesa a la izq)
    public float desplazamientoMesaHomeZ = 0.10f; // Flecha Azul en Unity (Mover mesa y placas abajo)

    private Vector3 posIniCabezal;
    private Vector3[] posIniPlacasCabezal;
    private Vector3 posIniMesa;
    private Vector3[] posIniPlacasMesa;



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

        // Guardar posiciones iniciales exactas del cabezal, mesa y placaas para mover sin desajustar ejes
        if (cabezalEjeZ != null) posIniCabezal = cabezalEjeZ.localPosition;
        if (placasTelescopicasCabezal != null)
        {
            posIniPlacasCabezal = new Vector3[placasTelescopicasCabezal.Length];
            for (int i = 0; i < placasTelescopicasCabezal.Length; i++)
                if (placasTelescopicasCabezal[i] != null)
                    posIniPlacasCabezal[i] = placasTelescopicasCabezal[i].localPosition;
        }

        if (mesaEjeX != null) posIniMesa = mesaEjeX.localPosition;
        if (placasInferioresMesa != null)
        {
            posIniPlacasMesa = new Vector3[placasInferioresMesa.Length];
            for (int i = 0; i < placasInferioresMesa.Length; i++)
                if (placasInferioresMesa[i] != null)
                    posIniPlacasMesa[i] = placasInferioresMesa[i].localPosition;
        }
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

        // Calcular cuánto movió la mano el jugador a lo largo del eje horizontal de la máquina
        if (interactorManija != null && interactorManija.transform != null)
        {
            Vector3 desplazamientoMano = interactorManija.transform.position - posManoInicialMundo;
            // Proyectamos el movimiento de la mano sobre el eje X de la máquina (transform.right)
            float deltaRiel = Vector3.Dot(desplazamientoMano, transform.right);

            float minLim = Mathf.Min(0f, -distanciaApertura);
            float maxLim = Mathf.Max(0f, -distanciaApertura);

            aperturaActual = Mathf.Clamp(aperturaAlAgarrar + deltaRiel, minLim, maxLim);
        }

        // Aplicar posición exacta sobre el riel local (nunca se sale de Y ni de Z)
        puertaIzquierda.localPosition = new Vector3(
            posCerradaIzq.x + aperturaActual,
            posCerradaIzq.y,
            posCerradaIzq.z
        );

        // Mover Puerta Derecha en sentido opuesto (espejo)
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
            StartCoroutine(RotarSuave(palancaMainPower, Quaternion.Euler(90f, 0f, 0f), 0.25f));

        ReproducirSonido(sonidoBreaker);
        pasoActual = PasoEncendido.Paso2_AbrirValvulaAire;
        ActualizarTextoGuia("Paso 2: Gira la llave azul trasera para abrir la presión de aire.");
    }

    // --- PASO 2: VÁLVULA DE AIRE AZUL ---
    public void CompletarPaso2_Aire()
    {
        if (pasoActual != PasoEncendido.Paso2_AbrirValvulaAire) return;
        if (palancaValvulaAire != null)
            StartCoroutine(RotarSuave(palancaValvulaAire, Quaternion.Euler(-90f, 0f, 0f), 0.35f));
        if (agujaManometro != null)
            StartCoroutine(RotarSuave(agujaManometro, Quaternion.Euler(-65f, 0f, 90f), 0.6f));

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
            // Rotación RELATIVA: multiplicamos la rotación actual por el ángulo deseado
            Quaternion rotGiro = Quaternion.Euler(0f, 0f, 45f);
            Quaternion rotFinal = botonEStop.localRotation * rotGiro;

            StartCoroutine(MoverYRotarSuave(botonEStop, posFinal, rotFinal, 0.25f));
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

        Vector3 cabInicio = cabezalEjeZ != null ? cabezalEjeZ.localPosition : Vector3.zero;
        // En Unity el Cabezal sube en el eje Y (Flecha Verde)
        Vector3 cabFin = posIniCabezal + new Vector3(0f, desplazamientoCabezalHomeY, 0f);

        Vector3 mesaInicio = mesaEjeX != null ? mesaEjeX.localPosition : Vector3.zero;
        // La mesa se mueve a la izquierda (Eje X) y hacia adelante/atrás (Eje Z)
        Vector3 mesaFin = posIniMesa + new Vector3(desplazamientoMesaHomeX, 0f, desplazamientoMesaHomeZ);

        while (t < duracion)
        {
            t += Time.deltaTime;
            float progreso = Mathf.SmoothStep(0f, 1f, t / duracion);

            if (cabezalEjeZ != null)
            {
                cabezalEjeZ.localPosition = Vector3.Lerp(cabInicio, cabFin, progreso);
                float deltaYActual = cabezalEjeZ.localPosition.y - posIniCabezal.y;
                ActualizarCubiertasCabezal(deltaYActual);
            }

            if (mesaEjeX != null)
            {
                mesaEjeX.localPosition = Vector3.Lerp(mesaInicio, mesaFin, progreso);
                float deltaZActual = mesaEjeX.localPosition.z - posIniMesa.z;
                ActualizarCubiertasMesa(deltaZActual);
            }

            yield return null;
        }

        pasoActual = PasoEncendido.MaquinaLista;
        ActualizarTextoGuia("¡Encendido listo! Máquina Haas VF-2 en Origen.");
    }

    // Mueve las placas del cabezal solo en el eje vertical Y (Flecha Verde)
    public void ActualizarCubiertasCabezal(float deltaY)
    {
        if (placasTelescopicasCabezal == null || posIniPlacasCabezal == null) return;
        int n = placasTelescopicasCabezal.Length;
        for (int i = 0; i < n; i++)
        {
            if (placasTelescopicasCabezal[i] == null) continue;
            // La placa 0 (arriba) se mueve el 100% con el cabezal; la última (abajo) se mueve 0%
            float peso = (n > 1) ? 1f - ((float)i / (n - 1)) : 1f;
            placasTelescopicasCabezal[i].localPosition = posIniPlacasCabezal[i] + new Vector3(0f, deltaY * peso, 0f);
        }
    }

    // Mueve las placas debajo de la mesa en cascada en el eje Z (Flecha Azul)
    public void ActualizarCubiertasMesa(float deltaZ)
    {
        if (placasInferioresMesa == null || posIniPlacasMesa == null) return;
        int n = placasInferioresMesa.Length;
        for (int i = 0; i < n; i++)
        {
            if (placasInferioresMesa[i] == null) continue;
            // La placa superior bajo la mesa se mueve al 100%; la inferior queda fija en la base
            float peso = (n > 1) ? 1f - ((float)i / (n - 1)) : 1f;
            placasInferioresMesa[i].localPosition = posIniPlacasMesa[i] + new Vector3(0f, 0f, deltaZ * peso);
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