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

    [Header("Torreta / Foco Superior de Alarma")]
    public MeshRenderer focoTorretaRenderer; // Opcional: El cilindro blanco superior del gabinete
    public Light luzPuntualTorreta;          // Tu objeto LuzAlerta (Point Light roja)
    public Color colorTorretaAlarma = new Color(1f, 0.05f, 0.05f);
    public Color colorTorretaLista = new Color(0.05f, 0.9f, 0.15f);
    private Color colorOriginalTorreta = Color.white;
    private bool alarmasActivasTorreta = false;

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
    private float aperturaActual = 0f;

    [Header("6. Sistema de 4 Alarmas en Botón RESET")]
    public int pulsacionesResetNecesarias = 4;
    private int pulsacionesResetActuales = 0;
    private readonly string[] nombresAlarmasHaas = new string[]
    {
        "ALARMA 1/4: [102] SERVOS OFF",
        "ALARMA 2/4: [101] DOOR HOLD / INTERLOCK",
        "ALARMA 3/4: [123] LOW LUBE / SPINDLE DRIVE",
        "ALARMA 4/4: [161] AXES NOT ZEROED"
    };

    private Transform objetivoActualGuia;
    private Renderer rendererObjetivoActual;
    private Color colorOriginalObjetivo = Color.white;

    [Header("7. Ejes y Cubiertas Telescópicas (Home en Ejes de Unity)")]
    public Transform cabezalEjeZ;
    public Transform[] placasTelescopicasCabezal;
    public float desplazamientoCabezalHomeY = 0.18f;

    public Transform mesaEjeX;
    public Transform[] placasInferioresMesa;
    public float desplazamientoMesaHomeX = 0.25f;
    public float desplazamientoMesaHomeZ = 0.10f;

    private Vector3 posIniCabezal;
    private Vector3[] posIniPlacasCabezal;
    private Vector3 posIniMesa;
    private Vector3[] posIniPlacasMesa;

    [Header("8. Ayuda Visual (Flecha y Parpadeo de Botones)")]
    public Transform flechaGuia3D;
    public Vector3 offsetFlecha = new Vector3(0f, 0.15f, 0f);
    public Transform botonPowerOnRef;
    public Transform botonResetRef;
    public Transform botonPowerUpRestartRef;
    public Color colorResaltadoBoton = new Color(1f, 0.85f, 0f);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sonidoBreaker;
    public AudioClip sonidoAire;
    public AudioClip sonidoVentiladorLoop;
    public AudioClip sonidoBeepBoton;
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

        // Asegurar que la torreta y LuzAlerta inicien APAGADAS hasta presionar Power On
        alarmasActivasTorreta = false;
        if (focoTorretaRenderer != null && focoTorretaRenderer.material != null && focoTorretaRenderer.material.HasProperty("_Color"))
            colorOriginalTorreta = focoTorretaRenderer.material.color;
        if (luzPuntualTorreta != null)
            luzPuntualTorreta.enabled = false;

        // Guardar posiciones iniciales exactas del cabezal, mesa y placas
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

        CambiarObjetivoGuia(palancaMainPower);
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
        // ¡AQUÍ FALTABA LLAMAR A LA FUNCIÓN DE LA FLECHA Y LA LUZ!
        AnimarAyudasVisualesYTorreta();

        if (puertaIzquierda == null) return;

        if (puertaIzquierda.parent != padreOriginalIzq)
            puertaIzquierda.SetParent(padreOriginalIzq, false);

        if (interactorManija != null && interactorManija.transform != null)
        {
            Vector3 desplazamientoMano = interactorManija.transform.position - posManoInicialMundo;
            float deltaRiel = Vector3.Dot(desplazamientoMano, transform.right);

            float minLim = Mathf.Min(0f, -distanciaApertura);
            float maxLim = Mathf.Max(0f, -distanciaApertura);

            aperturaActual = Mathf.Clamp(aperturaAlAgarrar + deltaRiel, minLim, maxLim);
        }

        puertaIzquierda.localPosition = new Vector3(
            posCerradaIzq.x + aperturaActual,
            posCerradaIzq.y,
            posCerradaIzq.z
        );

        if (sincronizarPuertasEnEspejo && puertaDerecha != null)
        {
            puertaDerecha.localPosition = new Vector3(
                posCerradaDer.x - aperturaActual,
                posCerradaDer.y,
                posCerradaDer.z
            );
        }

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
                pulsacionesResetActuales = 0;
                CambiarObjetivoGuia(botonResetRef);
                ActualizarTextoGuia($"PASO 6:\nPresiona RESET 4 veces para borrar las alarmas.\n({nombresAlarmasHaas[0]})");
            }
        }
    }

    // --- ANIMACIÓN DE TORRETA ROJA, FLECHA 3D Y PARPADEO DE BOTÓN ---
    private void AnimarAyudasVisualesYTorreta()
    {
        // 1. Parpadeo de LuzAlerta y Foco Superior en Rojo (funciona aunque solo asignes LuzAlerta)
        if (alarmasActivasTorreta)
        {
            bool encendido = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;

            if (luzPuntualTorreta != null)
                luzPuntualTorreta.enabled = encendido;

            if (focoTorretaRenderer != null && focoTorretaRenderer.material != null && focoTorretaRenderer.material.HasProperty("_Color"))
                focoTorretaRenderer.material.color = encendido ? colorTorretaAlarma : colorOriginalTorreta;
        }

        // 2. Movimiento flotante de la Flecha Guía y pulso de color en el botón objetivo
        if (objetivoActualGuia != null)
        {
            if (flechaGuia3D != null)
            {
                if (!flechaGuia3D.gameObject.activeSelf) flechaGuia3D.gameObject.SetActive(true);
                Vector3 rebote = Vector3.up * (Mathf.Sin(Time.time * 5f) * 0.03f);
                flechaGuia3D.position = objetivoActualGuia.position + offsetFlecha + rebote;
            }

            if (rendererObjetivoActual != null && rendererObjetivoActual.material != null && rendererObjetivoActual.material.HasProperty("_Color"))
            {
                float pulso = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                rendererObjetivoActual.material.color = Color.Lerp(colorOriginalObjetivo, colorResaltadoBoton, pulso * 0.75f);
            }
        }
        else if (flechaGuia3D != null && flechaGuia3D.gameObject.activeSelf)
        {
            flechaGuia3D.gameObject.SetActive(false);
        }
    }

    private void CambiarObjetivoGuia(Transform nuevoObjetivo)
    {
        if (rendererObjetivoActual != null && rendererObjetivoActual.material != null && rendererObjetivoActual.material.HasProperty("_Color"))
            rendererObjetivoActual.material.color = colorOriginalObjetivo;

        objetivoActualGuia = nuevoObjetivo;
        rendererObjetivoActual = null;

        if (nuevoObjetivo != null)
        {
            rendererObjetivoActual = nuevoObjetivo.GetComponentInChildren<Renderer>();
            if (rendererObjetivoActual != null && rendererObjetivoActual.material != null && rendererObjetivoActual.material.HasProperty("_Color"))
                colorOriginalObjetivo = rendererObjetivoActual.material.color;
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
        CambiarObjetivoGuia(palancaValvulaAire);
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
        CambiarObjetivoGuia(botonPowerOnRef);
        ActualizarTextoGuia("Paso 3: Ve al frente y presiona el botón verde POWER ON.");
    }

    // --- PASO 3: BOTÓN VERDE POWER ON ---
    public void CompletarPaso3_PowerOn()
    {
        if (pasoActual != PasoEncendido.Paso3_BotonPowerOn) return;
        if (rendererPantallaCNC != null && matPantallaAlarma != null)
            rendererPantallaCNC.material = matPantallaAlarma;

        // Activar el parpadeo rojo de LuzAlerta al encender la máquina
        alarmasActivasTorreta = true;

        if (audioSource != null && sonidoVentiladorLoop != null)
        {
            audioSource.clip = sonidoVentiladorLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        pasoActual = PasoEncendido.Paso4_LiberarEStop;
        CambiarObjetivoGuia(botonEStop);
        ActualizarTextoGuia("Paso 4: Libera el botón rojo de Paro de Emergencia (E-STOP).");
    }

    // --- PASO 4: LIBERAR PARO DE EMERGENCIA ---
    public void CompletarPaso4_EStop()
    {
        if (pasoActual != PasoEncendido.Paso4_LiberarEStop) return;
        if (botonEStop != null)
        {
            Vector3 posFinal = botonEStop.localPosition + new Vector3(0f, -0.015f, 0f);
            Quaternion rotGiro = Quaternion.Euler(0f, 0f, 45f);
            Quaternion rotFinal = botonEStop.localRotation * rotGiro;

            StartCoroutine(MoverYRotarSuave(botonEStop, posFinal, rotFinal, 0.25f));
        }

        pasoActual = PasoEncendido.Paso5_CiclarPuertas;
        CambiarObjetivoGuia(puertaIzquierda);
        ActualizarTextoGuia("Paso 5: Abre y vuelve a cerrar las puertas de la cabina.");
    }

    // --- PASO 6: BOTÓN RESET (Se debe presionar 4 veces para borrar las 4 alarmas) ---
    public void CompletarPaso6_Reset()
    {
        if (pasoActual != PasoEncendido.Paso6_ResetAlarmas) return;

        ReproducirSonido(sonidoBeepBoton);
        pulsacionesResetActuales++;

        int restantes = pulsacionesResetNecesarias - pulsacionesResetActuales;

        if (restantes > 0)
        {
            int indiceAlarma = Mathf.Clamp(pulsacionesResetActuales, 0, nombresAlarmasHaas.Length - 1);
            ActualizarTextoGuia($"PASO 6:\nAlarma borrada. Faltan {restantes} pulsaciones de RESET.\n({nombresAlarmasHaas[indiceAlarma]})");
        }
        else
        {
            alarmasActivasTorreta = false;
            if (focoTorretaRenderer != null && focoTorretaRenderer.material != null && focoTorretaRenderer.material.HasProperty("_Color"))
                focoTorretaRenderer.material.color = colorTorretaLista;
            if (luzPuntualTorreta != null)
                luzPuntualTorreta.enabled = false;

            if (rendererPantallaCNC != null && matPantallaLista != null)
                rendererPantallaCNC.material = matPantallaLista;

            pasoActual = PasoEncendido.Paso7_ZeroReturnHome;
            CambiarObjetivoGuia(botonPowerUpRestartRef);
            ActualizarTextoGuia("PASO 7:\n¡4 Alarmas borradas! Presiona POWER UP / RESTART para mandar ejes a Origen.");
        }
    }

    // --- PASO 7: MANDAR EJES A ORIGEN + CUBIERTAS TELESCÓPICAS ---
    public void CompletarPaso7_ZeroReturn()
    {
        if (pasoActual != PasoEncendido.Paso7_ZeroReturnHome) return;
        CambiarObjetivoGuia(null);
        ReproducirSonido(sonidoServosHome);
        StartCoroutine(SecuenciaHomeEjes());
    }

    private IEnumerator SecuenciaHomeEjes()
    {
        ActualizarTextoGuia("Enviando ejes Z, X, Y a Origen (Home)...");
        float duracion = 2.5f;
        float t = 0f;

        Vector3 cabInicio = cabezalEjeZ != null ? cabezalEjeZ.localPosition : Vector3.zero;
        Vector3 cabFin = posIniCabezal + new Vector3(0f, desplazamientoCabezalHomeY, 0f);

        Vector3 mesaInicio = mesaEjeX != null ? mesaEjeX.localPosition : Vector3.zero;
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
        ActualizarTextoGuia("¡Encendido listo! Máquina Haas VF-2 en Origen lista para cambio de herramienta.");
    }

    public void ActualizarCubiertasCabezal(float deltaY)
    {
        if (placasTelescopicasCabezal == null || posIniPlacasCabezal == null) return;
        int n = placasTelescopicasCabezal.Length;
        for (int i = 0; i < n; i++)
        {
            if (placasTelescopicasCabezal[i] == null) continue;
            float peso = (n > 1) ? 1f - ((float)i / (n - 1)) : 1f;
            placasTelescopicasCabezal[i].localPosition = posIniPlacasCabezal[i] + new Vector3(0f, deltaY * peso, 0f);
        }
    }

    public void ActualizarCubiertasMesa(float deltaZ)
    {
        if (placasInferioresMesa == null || posIniPlacasMesa == null) return;
        int n = placasInferioresMesa.Length;
        for (int i = 0; i < n; i++)
        {
            if (placasInferioresMesa[i] == null) continue;
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