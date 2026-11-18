using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControladorPrensaKurt : MonoBehaviour
{
    public enum EstadoPrensa
    {
        Paso1_CargarPrensaALaMesa = 0,
        Paso2_InsertarTornillosEnRendijas = 1,
        Paso3_AjustarTornillosConManivela = 2,
        Paso4_ColocarPiezaEnMordazas = 3,
        Paso5_ApretarMordazaConManivela = 4,
        Paso6_PiezaSujetadaFirme = 5
    }

    [Header("Conexión con el Controlador Principal")]
    public ControladorEncendidoHaas controladorEncendido;
    public EstadoPrensa estadoActual = EstadoPrensa.Paso1_CargarPrensaALaMesa;

    [Header("1. Montaje de la Prensa en la Mesa (Socket Automático Normal)")]
    public Transform prensaCuerpoFijo;            // Prensa_Cuerpo_Fijo
    public XRSocketInteractor socketPrensaEnMesa; // Socket en la mesa
    public bool prensaIniciaYaMontadaEnMesa = false;

    private XRGrabInteractable grabPrensa;
    private Rigidbody rbPrensa;

    [Header("2. Sistema de 2 Tornillos en Rendijas T")]
    public XRSocketInteractor socketEntradaRanuraFrente;
    public XRSocketInteractor socketEntradaRanuraAtras;
    public Transform destinoFinalTornilloFrente;
    public Transform destinoFinalTornilloAtras;

    public float duracionDeslizamientoTornillo = 0.85f;
    public float distanciaDeteccionHerramienta = 0.14f;

    private Transform tornilloFrenteActual;
    private Transform tornilloAtrasActual;
    private bool tornilloFrenteEnCanal = false;
    private bool tornilloAtrasEnCanal = false;
    private bool tornilloFrenteApretado = false;
    private bool tornilloAtrasApretado = false;
    private bool animandoTornillo = false;

    [Header("3. Manivela Dual (Prensa_Manivela)")]
    public Transform herramientaManivela;           // Prensa_Manivela
    public XRBaseInteractable interactableManivela; // XRGrabInteractable de Prensa_Manivela
    public Transform mordazaMovil;                  // Prensa_Mordaza_Movil
    public Transform husilloHexPrensa;              // Prensa_Husillo_Hex
    private XRGrabInteractable grabManivela;
    private Rigidbody rbManivela;

    [Header("4. Pieza de Trabajo (PiezaParaPrensa)")]
    public XRSocketInteractor socketPieza;          // Socket_Pieza_Prensa
    public Transform puntoMontajePieza;

    [Header("5. Configuración de Recorrido de Mordaza")]
    public float recorridoCierreMordaza = 0.045f;
    public Vector3 ejeDeslizamientoMordaza = new Vector3(0f, 0f, -1f);
    public Vector3 ejeGiroManivela = new Vector3(0f, 0f, 1f);
    public float vueltasTotalesManivela = 2.5f;
    public float sensibilidadManivela = 4.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sonidoMontarPrensaPesada;
    public AudioClip sonidoDeslizarTornillo;
    public AudioClip sonidoTrinqueteManivela;
    public AudioClip sonidoColocarMetal;
    public AudioClip sonidoAprieteFinal;

    private Vector3 posAbiertaMordaza;
    private Vector3 posLocalOriginalManivela;
    private Quaternion rotLocalOriginalManivela;
    private Transform padreOriginalManivela;
    private Quaternion rotInicialHusilloHex;
    private Transform piezaActual;

    private IXRSelectInteractor interactorManivela;
    private Vector3 posManoInicial;
    private float aprieteAlAgarrar = 0f;
    [Range(0f, 1f)]
    public float nivelAprieteActual = 0f;

    private void Awake()
    {
        if (herramientaManivela != null)
        {
            padreOriginalManivela = herramientaManivela.parent;
            posLocalOriginalManivela = herramientaManivela.localPosition;
            rotLocalOriginalManivela = herramientaManivela.localRotation;
            grabManivela = herramientaManivela.GetComponent<XRGrabInteractable>();
            rbManivela = herramientaManivela.GetComponent<Rigidbody>();

            if (rbManivela != null)
            {
                rbManivela.useGravity = false;
                rbManivela.isKinematic = true;
            }
        }
    }

    private void Start()
    {
        if (prensaCuerpoFijo != null)
        {
            grabPrensa = prensaCuerpoFijo.GetComponent<XRGrabInteractable>();
            rbPrensa = prensaCuerpoFijo.GetComponent<Rigidbody>();
        }

        if (mordazaMovil != null)
            posAbiertaMordaza = mordazaMovil.localPosition;

        if (husilloHexPrensa != null)
            rotInicialHusilloHex = husilloHexPrensa.localRotation;

        if (grabManivela != null)
            interactableManivela = grabManivela;

        if (interactableManivela != null)
        {
            interactableManivela.selectEntered.AddListener(AlAgarrarManivela);
            interactableManivela.selectExited.AddListener(AlSoltarManivela);
        }

        if (socketPrensaEnMesa != null)
        {
            socketPrensaEnMesa.selectEntered.AddListener(AlColocarPrensaEnMesa);
            socketPrensaEnMesa.selectExited.AddListener(AlRetirarPrensaDeMesa);
        }

        if (socketEntradaRanuraFrente != null)
            socketEntradaRanuraFrente.selectEntered.AddListener(AlMeterTornilloEnOrillaFrente);

        if (socketEntradaRanuraAtras != null)
            socketEntradaRanuraAtras.selectEntered.AddListener(AlMeterTornilloEnOrillaAtras);

        if (socketPieza != null)
        {
            socketPieza.selectEntered.AddListener(AlColocarPiezaEnSocket);
            socketPieza.selectExited.AddListener(AlRetirarPiezaDeSocket);
        }

        if (prensaIniciaYaMontadaEnMesa)
        {
            ActivarSocketsEntradaTornillos(true);
            if (socketPieza != null) socketPieza.socketActive = false;
            estadoActual = EstadoPrensa.Paso2_InsertarTornillosEnRendijas;
            ActualizarBloqueoAgarrePrensa();
        }
        else
        {
            ActivarSocketsEntradaTornillos(false);
            if (socketPieza != null) socketPieza.socketActive = false;
        }
    }

    // Cambia únicamente si la manivela se traslada en la mano (Paso 3: apretar tornillos)
    // o si se queda anclada en su eje frontal para girar la mordaza (Paso 5: apretar prensa)
    private void CambiarModoTraslacionManivela(bool permitirTrasladarHaciaTornillos)
    {
        if (grabManivela == null) return;

        if (!permitirTrasladarHaciaTornillos)
        {
            AcoplarManivelaEnHusilloPrensa();
        }

        grabManivela.trackPosition = permitirTrasladarHaciaTornillos;
        grabManivela.trackRotation = permitirTrasladarHaciaTornillos;
        grabManivela.retainTransformParent = true;
        grabManivela.enabled = true;
    }

    // =========================================================================
    // PASO 1: SOCKET NORMAL PARA LA PRENSA (NO SE APAGA EL SOCKET NI SE ROTA POR CÓDIGO)
    // =========================================================================

    private void AlColocarPrensaEnMesa(SelectEnterEventArgs args)
    {
        if (args.interactableObject.transform == herramientaManivela) return;

        ReproducirSonido(sonidoMontarPrensaPesada);

        // El XRSocketInteractor de la mesa se encarga solito de colocar y mantener la prensa en su lugar.
        // Nosotros solo activamos los sockets de las rendijas para los tornillos.
        ActivarSocketsEntradaTornillos(true);
        ActualizarBloqueoAgarrePrensa();

        estadoActual = EstadoPrensa.Paso2_InsertarTornillosEnRendijas;
        ApuntarFlecha(socketEntradaRanuraFrente != null ? socketEntradaRanuraFrente.transform : destinoFinalTornilloFrente);
        MostrarMensaje("PRENSA EN LA MESA.\nColoca los 2 tornillos en los extremos de las rendijas de la mesa.");
    }

    private void AlRetirarPrensaDeMesa(SelectExitEventArgs args)
    {
        if (!tornilloFrenteEnCanal && !tornilloAtrasEnCanal)
        {
            ActivarSocketsEntradaTornillos(false);
            if (socketPieza != null) socketPieza.socketActive = false;
            estadoActual = EstadoPrensa.Paso1_CargarPrensaALaMesa;
            MostrarMensaje("Prensa retirada de la mesa.\nVuelve a colocarla sobre la mesa ranurada.");
        }
    }

    // En lugar de apagar el socket o el Grab (lo que hacía saltar la prensa),
    // bloqueamos trackPosition y trackRotation cuando hay tornillos puestos para que nadie pueda levantarla
    private void ActualizarBloqueoAgarrePrensa()
    {
        if (grabPrensa == null) return;

        bool puedeMoverPrensa = !tornilloFrenteEnCanal && !tornilloAtrasEnCanal && (piezaActual == null);
        grabPrensa.trackPosition = puedeMoverPrensa;
        grabPrensa.trackRotation = puedeMoverPrensa;
    }

    private void ActivarSocketsEntradaTornillos(bool activo)
    {
        if (socketEntradaRanuraFrente != null)
            socketEntradaRanuraFrente.socketActive = activo && !tornilloFrenteEnCanal;
        if (socketEntradaRanuraAtras != null)
            socketEntradaRanuraAtras.socketActive = activo && !tornilloAtrasEnCanal;
    }

    // =========================================================================
    // PASO 2: ENTRADA Y RETIRO DE TORNILLOS EN LAS RENDIJAS DE LA MESA
    // =========================================================================

    private void AlMeterTornilloEnOrillaFrente(SelectEnterEventArgs args)
    {
        Transform obj = args.interactableObject.transform;
        if (obj == herramientaManivela || obj == prensaCuerpoFijo || tornilloFrenteEnCanal) return;

        tornilloFrenteActual = obj;
        tornilloFrenteEnCanal = true;
        tornilloFrenteApretado = false;

        ActualizarBloqueoAgarrePrensa();
        StartCoroutine(CorrutinaDeslizarTornilloAlCanal(
            tornilloFrenteActual,
            socketEntradaRanuraFrente,
            destinoFinalTornilloFrente
        ));
    }

    private void AlMeterTornilloEnOrillaAtras(SelectEnterEventArgs args)
    {
        Transform obj = args.interactableObject.transform;
        if (obj == herramientaManivela || obj == prensaCuerpoFijo || tornilloAtrasEnCanal) return;

        tornilloAtrasActual = obj;
        tornilloAtrasEnCanal = true;
        tornilloAtrasApretado = false;

        ActualizarBloqueoAgarrePrensa();
        StartCoroutine(CorrutinaDeslizarTornilloAlCanal(
            tornilloAtrasActual,
            socketEntradaRanuraAtras,
            destinoFinalTornilloAtras
        ));
    }

    private IEnumerator CorrutinaDeslizarTornilloAlCanal(Transform tornillo, XRSocketInteractor socketOrilla, Transform destinoEnPrensa)
    {
        animandoTornillo = true;

        XRGrabInteractable grabTornillo = tornillo.GetComponent<XRGrabInteractable>();
        Rigidbody rbTornillo = tornillo.GetComponent<Rigidbody>();

        if (grabTornillo != null)
        {
            grabTornillo.selectEntered.RemoveListener(AlSacarTornilloConMano);
            grabTornillo.enabled = false;
        }

        if (socketOrilla != null) socketOrilla.socketActive = false;

        yield return null;

        if (rbTornillo != null)
        {
            if (!rbTornillo.isKinematic)
            {
                rbTornillo.velocity = Vector3.zero;
                rbTornillo.angularVelocity = Vector3.zero;
            }
            rbTornillo.isKinematic = true;
        }

        if (destinoEnPrensa != null)
        {
            Vector3 posInicioMundo = socketOrilla != null ? socketOrilla.transform.position : tornillo.position;
            Quaternion rotInicioMundo = tornillo.rotation;

            tornillo.SetParent(destinoEnPrensa, true);
            ReproducirSonido(sonidoDeslizarTornillo);

            float t = 0f;
            while (t < duracionDeslizamientoTornillo)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duracionDeslizamientoTornillo);
                tornillo.position = Vector3.Lerp(posInicioMundo, destinoEnPrensa.position, p);
                tornillo.rotation = Quaternion.Slerp(rotInicioMundo, destinoEnPrensa.rotation, p);
                yield return null;
            }

            tornillo.localPosition = Vector3.zero;
            tornillo.localRotation = Quaternion.identity;
        }

        yield return null;

        if (grabTornillo != null)
        {
            grabTornillo.retainTransformParent = false;
            grabTornillo.enabled = true;
            grabTornillo.selectEntered.RemoveListener(AlSacarTornilloConMano);
            grabTornillo.selectEntered.AddListener(AlSacarTornilloConMano);
        }

        animandoTornillo = false;
        EvaluarProgresoTornillos();
    }

    private void AlSacarTornilloConMano(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;

        Transform t = args.interactableObject.transform;
        XRGrabInteractable grabT = t.GetComponent<XRGrabInteractable>();

        if (t == tornilloFrenteActual && !tornilloFrenteApretado)
        {
            if (grabT != null) grabT.selectEntered.RemoveListener(AlSacarTornilloConMano);
            tornilloFrenteEnCanal = false;
            tornilloFrenteActual = null;
            t.SetParent(null, true);

            if (socketEntradaRanuraFrente != null) socketEntradaRanuraFrente.socketActive = true;
            estadoActual = EstadoPrensa.Paso2_InsertarTornillosEnRendijas;
            ActualizarBloqueoAgarrePrensa();
            MostrarMensaje("Tornillo frontal retirado.\nColócalo en el extremo de la rendija o retira ambos para mover la prensa.");
        }
        else if (t == tornilloAtrasActual && !tornilloAtrasApretado)
        {
            if (grabT != null) grabT.selectEntered.RemoveListener(AlSacarTornilloConMano);
            tornilloAtrasEnCanal = false;
            tornilloAtrasActual = null;
            t.SetParent(null, true);

            if (socketEntradaRanuraAtras != null) socketEntradaRanuraAtras.socketActive = true;
            estadoActual = EstadoPrensa.Paso2_InsertarTornillosEnRendijas;
            ActualizarBloqueoAgarrePrensa();
            MostrarMensaje("Tornillo trasero retirado.\nColócalo en el extremo de la rendija o retira ambos para mover la prensa.");
        }
    }

    private void EvaluarProgresoTornillos()
    {
        if (tornilloFrenteEnCanal && tornilloAtrasEnCanal)
        {
            if (!tornilloFrenteApretado || !tornilloAtrasApretado)
            {
                estadoActual = EstadoPrensa.Paso3_AjustarTornillosConManivela;
                // Permitir despegar la manivela para llevarla a los dos tornillos
                CambiarModoTraslacionManivela(permitirTrasladarHaciaTornillos: true);

                Transform siguienteTornillo = !tornilloFrenteApretado ? tornilloFrenteActual : tornilloAtrasActual;
                ApuntarFlecha(siguienteTornillo);
                MostrarMensaje("¡2 TORNILLOS EN POSICIÓN!\nAgarra la manivela y acércala a cada tornillo para apretarlos.");
            }
        }
        else
        {
            Transform faltaSocket = !tornilloFrenteEnCanal
                ? (socketEntradaRanuraFrente != null ? socketEntradaRanuraFrente.transform : destinoFinalTornilloFrente)
                : (socketEntradaRanuraAtras != null ? socketEntradaRanuraAtras.transform : destinoFinalTornilloAtras);
            ApuntarFlecha(faltaSocket);
            MostrarMensaje("1/2 tornillos colocados.\nIntroduce el segundo tornillo por la rendija del extremo de la mesa.");
        }
    }

    // =========================================================================
    // PASO 3: APRETAR LOS TORNILLOS AL ACERCAR LA MANIVELA
    // =========================================================================

    private void VerificarProximidadHerramientaATornillos()
    {
        if (herramientaManivela == null || animandoTornillo) return;

        if (tornilloFrenteEnCanal && tornilloFrenteActual != null && !tornilloFrenteApretado)
        {
            float distFrente = Vector3.Distance(herramientaManivela.position, tornilloFrenteActual.position);
            if (distFrente <= distanciaDeteccionHerramienta)
            {
                StartCoroutine(CorrutinaApretarTornilloConHerramienta(tornilloFrenteActual, esFrente: true));
                return;
            }
        }

        if (tornilloAtrasEnCanal && tornilloAtrasActual != null && !tornilloAtrasApretado)
        {
            float distAtras = Vector3.Distance(herramientaManivela.position, tornilloAtrasActual.position);
            if (distAtras <= distanciaDeteccionHerramienta)
            {
                StartCoroutine(CorrutinaApretarTornilloConHerramienta(tornilloAtrasActual, esFrente: false));
                return;
            }
        }
    }

    private IEnumerator CorrutinaApretarTornilloConHerramienta(Transform tornillo, bool esFrente)
    {
        animandoTornillo = true;

        if (esFrente) tornilloFrenteApretado = true;
        else tornilloAtrasApretado = true;

        XRGrabInteractable grabTornillo = tornillo.GetComponent<XRGrabInteractable>();
        if (grabTornillo != null)
        {
            grabTornillo.selectEntered.RemoveListener(AlSacarTornilloConMano);
            grabTornillo.enabled = false;
        }

        ReproducirSonido(sonidoTrinqueteManivela);


        Vector3 posIni = tornillo.position;
        Vector3 posApretado = posIni + (Vector3.down * 0.002f); // Baja 2mm verticales hacia la mesa

        float duracion = 0.55f;
        float t = 0f;
        float anguloAnterior = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = t / duracion;
            float anguloActual = p * 360f;


            tornillo.Rotate(Vector3.up, anguloActual - anguloAnterior, Space.World);
            anguloAnterior = anguloActual;

            tornillo.position = Vector3.Lerp(posIni, posApretado, p);
            yield return null;
        }

        ReproducirSonido(sonidoAprieteFinal);
        animandoTornillo = false;

        if (tornilloFrenteApretado && tornilloAtrasApretado)
        {
            // Regresar la manivela al eje frontal de la prensa y fijarla ahí (trackPosition = false)
            // SIN apagar grabManivela.enabled, para que puedas agarrarla y girarla cuando quieras
            CambiarModoTraslacionManivela(permitirTrasladarHaciaTornillos: false);

            if (socketPieza != null) socketPieza.socketActive = true;

            if (piezaActual != null)
            {
                estadoActual = EstadoPrensa.Paso5_ApretarMordazaConManivela;
                ApuntarFlecha(herramientaManivela);
                MostrarMensaje("¡TORNILLOS APRETADOS!\nAhora sujeta la manivela en el frente y gírala para apretar la pieza.");
            }
            else
            {
                estadoActual = EstadoPrensa.Paso4_ColocarPiezaEnMordazas;
                ApuntarFlecha(puntoMontajePieza != null ? puntoMontajePieza : (socketPieza != null ? socketPieza.transform : transform));
                MostrarMensaje("¡PRENSA FIJADA CON AMBOS TORNILLOS!\nAhora coloca PiezaParaPrensa entre las mordazas.");
            }
        }
        else
        {
            Transform faltaApretar = !tornilloFrenteApretado ? tornilloFrenteActual : tornilloAtrasActual;
            ApuntarFlecha(faltaApretar);
            MostrarMensaje("1/2 tornillos apretados.\nAcerca la manivela al otro tornillo para terminar de fijar la prensa.");
        }
    }

    private void AcoplarManivelaEnHusilloPrensa()
    {
        if (herramientaManivela == null || padreOriginalManivela == null) return;

        herramientaManivela.SetParent(padreOriginalManivela, false);
        herramientaManivela.localPosition = posLocalOriginalManivela;
        float anguloTotal = nivelAprieteActual * vueltasTotalesManivela * 360f;
        herramientaManivela.localRotation = rotLocalOriginalManivela * Quaternion.AngleAxis(anguloTotal, ejeGiroManivela.normalized);
    }

    // =========================================================================
    // PASOS 4, 5 y 6: COLOCAR LA PIEZA Y GIRAR LA MANIVELA EN EL FRENTE
    // =========================================================================

    private void AlColocarPiezaEnSocket(SelectEnterEventArgs args)
    {
        Transform obj = args.interactableObject.transform;
        if (obj == herramientaManivela || obj == prensaCuerpoFijo) return;

        piezaActual = obj;
        ReproducirSonido(sonidoColocarMetal);

        // Asegurar que la manivela esté acoplada al frente lista para girar (trackPosition = false)
        CambiarModoTraslacionManivela(permitirTrasladarHaciaTornillos: false);

        estadoActual = EstadoPrensa.Paso5_ApretarMordazaConManivela;
        ApuntarFlecha(herramientaManivela);
        MostrarMensaje("Pieza colocada en la prensa.\nSujeta la manivela frontal y mueve la mano para cerrar la mordaza.");
    }

    private void AlRetirarPiezaDeSocket(SelectExitEventArgs args)
    {
        if (estadoActual == EstadoPrensa.Paso5_ApretarMordazaConManivela && nivelAprieteActual < 0.9f)
        {
            piezaActual = null;
            estadoActual = EstadoPrensa.Paso4_ColocarPiezaEnMordazas;
            MostrarMensaje("Pieza retirada.\nVuelve a colocar la pieza entre las mordazas de la prensa.");
        }
    }

    private void AlAgarrarManivela(SelectEnterEventArgs args)
    {
        if (args.interactorObject == null || args.interactorObject is XRSocketInteractor) return;
        interactorManivela = args.interactorObject;
        posManoInicial = interactorManivela.transform.position;
        aprieteAlAgarrar = nivelAprieteActual;

        // Si ya están apretados los 2 tornillos, asegurar que al agarrarla permanezca en el eje frontal
        if (tornilloFrenteApretado && tornilloAtrasApretado)
        {
            CambiarModoTraslacionManivela(permitirTrasladarHaciaTornillos: false);
        }
    }

    private void AlSoltarManivela(SelectExitEventArgs args)
    {
        interactorManivela = null;
        AcoplarManivelaEnHusilloPrensa();
    }

    private void Update()
    {
        // Permitir apretar los tornillos siempre que estén en el canal y aún falte alguno por apretar
        if (tornilloFrenteEnCanal || tornilloAtrasEnCanal)
        {
            if (!tornilloFrenteApretado || !tornilloAtrasApretado)
            {
                VerificarProximidadHerramientaATornillos();
            }
        }

        // Giro de la manivela en el frente de la prensa: funciona en cuanto tienes la manivela seleccionada
        // y ambos tornillos ya están apretados
        if (tornilloFrenteApretado && tornilloAtrasApretado &&
            interactorManivela != null && interactorManivela.transform != null)
        {
            Vector3 deltaMano = interactorManivela.transform.position - posManoInicial;

            // Usamos la cámara del jugador como referencia de izquierda/derecha (o magnitud si quiere apretar fácil)
            Transform refVista = Camera.main != null ? Camera.main.transform : transform;
            float avanceHorizontal = Vector3.Dot(deltaMano, refVista.right);
            float avanceVertical = Vector3.Dot(deltaMano, -refVista.up);
            float avanceMano = (avanceHorizontal + avanceVertical) * sensibilidadManivela;

            float nuevoApriete = Mathf.Clamp01(aprieteAlAgarrar + avanceMano);

            if (Mathf.Abs(nuevoApriete - nivelAprieteActual) > 0.08f)
                ReproducirSonido(sonidoTrinqueteManivela);

            nivelAprieteActual = nuevoApriete;
            ActualizarVisualMordazaYManivela();

            if (piezaActual != null && nivelAprieteActual >= 0.95f && estadoActual != EstadoPrensa.Paso6_PiezaSujetadaFirme)
            {
                nivelAprieteActual = 1f;
                ActualizarVisualMordazaYManivela();
                BloquearPiezaEnPrensa();
            }
            else if (piezaActual != null && nivelAprieteActual <= 0.30f && estadoActual == EstadoPrensa.Paso6_PiezaSujetadaFirme)
            {
                DesbloquearPiezaDePrensa();
            }
        }
    }

    private void ActualizarVisualMordazaYManivela()
    {
        if (mordazaMovil != null)
        {
            mordazaMovil.localPosition = posAbiertaMordaza + (ejeDeslizamientoMordaza.normalized * (recorridoCierreMordaza * nivelAprieteActual));
        }

        float anguloTotal = nivelAprieteActual * vueltasTotalesManivela * 360f;
        Quaternion rotGiro = Quaternion.AngleAxis(anguloTotal, ejeGiroManivela.normalized);

        if (herramientaManivela != null)
        {
            herramientaManivela.localPosition = posLocalOriginalManivela;
            herramientaManivela.localRotation = rotLocalOriginalManivela * rotGiro;
        }

        if (husilloHexPrensa != null)
            husilloHexPrensa.localRotation = rotInicialHusilloHex * rotGiro;
    }

    private void BloquearPiezaEnPrensa()
    {
        if (piezaActual == null) return;

        Transform piezaSegura = piezaActual;
        estadoActual = EstadoPrensa.Paso6_PiezaSujetadaFirme;

        XRGrabInteractable grab = piezaSegura.GetComponent<XRGrabInteractable>();
        Rigidbody rb = piezaSegura.GetComponent<Rigidbody>();

        if (socketPieza != null) socketPieza.socketActive = false;
        piezaActual = piezaSegura;

        if (grab != null) grab.enabled = false;
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        Transform destino = puntoMontajePieza != null ? puntoMontajePieza : (socketPieza != null ? socketPieza.transform : transform);
        piezaActual.SetParent(destino, true);
        piezaActual.localPosition = Vector3.zero;
        piezaActual.localRotation = Quaternion.identity;

        ReproducirSonido(sonidoAprieteFinal);
        ApuntarFlecha(null);
        MostrarMensaje("¡PIEZA SUJETADA CON TORQUE FIRME (100%)!\nPrensa atornillada y pieza bloqueada lista para el maquinado.");
    }

    private void DesbloquearPiezaDePrensa()
    {
        if (piezaActual == null) return;

        XRGrabInteractable grab = piezaActual.GetComponent<XRGrabInteractable>();
        Rigidbody rb = piezaActual.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (grab != null)
        {
            grab.retainTransformParent = false;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.enabled = true;
        }

        estadoActual = EstadoPrensa.Paso5_ApretarMordazaConManivela;
        ApuntarFlecha(piezaActual);
        MostrarMensaje("Mordaza aflojada.\nYa puedes retirar la pieza de la prensa con tu mano.");
    }

    private void ApuntarFlecha(Transform destino)
    {
        if (controladorEncendido != null)
            controladorEncendido.CambiarObjetivoGuiaPublico(destino);
    }

    private void MostrarMensaje(string msg)
    {
        if (controladorEncendido != null && controladorEncendido.textoGuiaUI != null)
            controladorEncendido.textoGuiaUI.text = msg;
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}