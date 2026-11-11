using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControladorATCHaas : MonoBehaviour
{
    public enum FaseOperacionHerramienta
    {
        Fase1_PresionarBotonCabezalAbrir = 0,
        Fase2_ColocarHerramientaEnHusillo = 1,
        Fase3_PresionarBotonCabezalSujetar = 2,
        Fase4_CerrarPuertasParaATC = 3,
        Fase5_PresionarTeclaMDI = 4,
        Fase6_PresionarTeclaT = 5,
        Fase7_DigitarNumeroHerramienta = 6,
        Fase8_PresionarATCFWD = 7,
        EjecutandoMovimientoATC = 8
    }

    public enum EstadoHusillo
    {
        Vacio_Cerrado = 0,
        Vacio_AbiertoParaInsertar = 1,
        HerramientaEnSocket_SinSujetar = 2,
        HerramientaSujetada= 3,
        HerramientaLiberada_ParaSacarConMano = 4
    }

    [Header("Conexión con el Controlador Principal")]
    public ControladorEncendidoHaas controladorEncendido;
    public FaseOperacionHerramienta faseActual = FaseOperacionHerramienta.Fase1_PresionarBotonCabezalAbrir;
    public EstadoHusillo estadoHusillo = EstadoHusillo.Vacio_Cerrado;
    public bool exigirEncendidoCompleto = true;

    [Header("1. Cambio Manual en Cabezal (Video del Profesor)")]
    public Transform botonToolReleaseCabezal; // El botón frontal del cabezal
    public XRSocketInteractor socketHusillo;  // Socket en la nariz del husillo
    public Transform puntoMontajeHusillo;     // Empty hijo del cabezal donde se sujeta el cono

    [Header("2. Referencias de Teclas del Tablero (Para la Flecha Guía)")]
    public Transform teclaMDIDNC;
    public Transform teclaLetraT;
    public Transform contenedorTecladoNumerico; 
    public Transform teclaATCFWD;

    [Header("3. Piezas Móviles del Carrusel (ATC)")]

    public Transform atcConjuntoDeslizable;
    public Transform atcPlatoGiratorio;
    public Transform atcPuertaMovil; 
    public float anguloAperturaPuertaATC = 60f; // Grados que se desliza la compuerta curva al abrir
    public float desplazamientoEntradaATC_X = 0.32f;
    public float alturaCabezalNivelATC_Y = 0.12f;
    public float alturaCabezalArribaLibre_Y = 0.24f;
    public Vector3 ejeGiroPlato = new Vector3(0f, 0f, 1f);

    [Header("4. Estado de Herramientas y Entrada MDI")]
    public int totalPosiciones = 20;
    public int posicionActualCarrusel = 1;
    public string textoBufferMDI = ""; // Guarda lo que el usuario escribe
    public int herramientaSeleccionadaMDI = 0;

    public Transform[] slotsCarrusel = new Transform[20];
    public Transform[] herramientasEnMaquina = new Transform[20];
    public Transform herramientaActualEnHusillo;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sonidoAireToolRelease;
    public AudioClip sonidoBeepTecla;
    public AudioClip sonidoDeslizarATC;
    public AudioClip sonidoGiroPlato;

    private Vector3 posInicialATC;
    private Vector3 posBaseCabezal;
    private bool cambioEnProceso = false;

    private void Start()
    {
        if (atcConjuntoDeslizable != null)
            posInicialATC = atcConjuntoDeslizable.localPosition;

        if (controladorEncendido != null && controladorEncendido.cabezalEjeZ != null)
            posBaseCabezal = controladorEncendido.cabezalEjeZ.localPosition;

        // El socket del husillo inicia apagado hasta que el usuario presione el botón del cabezal
        if (socketHusillo != null)
        {
            socketHusillo.socketActive = false;
            socketHusillo.selectEntered.AddListener(AlInsertarHerramientaManual);
            socketHusillo.selectExited.AddListener(AlRetirarHerramientaDeSocketSinSujetar);
        }

        // Si habia herramienta en el husillo al iniciar
        if(herramientaActualEnHusillo != null)
        {
            FijarHerramientaEnHusillo();
        }
    }

    private void Update()
    {
        //  Detectar cuando el jugador saca con la mano una herramienta que estaba liberada
        if (estadoHusillo == EstadoHusillo.HerramientaLiberada_ParaSacarConMano && herramientaActualEnHusillo != null && puntoMontajeHusillo != null)
        {
            float distanciaAlHusillo = Vector3.Distance(herramientaActualEnHusillo.position, puntoMontajeHusillo.position);
            if (distanciaAlHusillo > 0.06f)
            {
                herramientaActualEnHusillo.SetParent(null, true);
                herramientaActualEnHusillo = null;
                estadoHusillo = EstadoHusillo.Vacio_Cerrado;
                MostrarMensaje("¡HERRAMIENTA RETIRADA CON LA MANO!\nPresiona el botón del cabezal si deseas insertar otra herramienta, o usa el panel MDI.");
            }
        }



        // Si estamos esperando que cierre las puertas después de meter la herramienta manual 
        if (faseActual == FaseOperacionHerramienta.Fase4_CerrarPuertasParaATC && controladorEncendido != null)
        {
            if (controladorEncendido.EstanPuertasCerradas())
            {
                faseActual = FaseOperacionHerramienta.Fase5_PresionarTeclaMDI;
                ApuntarFlecha(teclaMDIDNC);
                MostrarMensaje("PUERTAS CERRADAS.\nAhora ve al tablero y presiona la tecla [MDI / DNC].");
            }
        }
    }





    // =========================================================================

    //Carga y descarga manual en el cabezal

    public void PresionarBotonToolReleaseCabezal()
    {
        if (!ValidarMaquinaEncendida() || cambioEnProceso) return;

        if (botonToolReleaseCabezal != null)
            StartCoroutine(EfectoPulsarBoton(botonToolReleaseCabezal));

        ReproducirSonido(sonidoAireToolRelease);

        switch (estadoHusillo)
        {
            case EstadoHusillo.Vacio_Cerrado:
                // Abrir mordaza para permitir meter una herramienta nueva
                if (socketHusillo != null) socketHusillo.socketActive = true;
                estadoHusillo = EstadoHusillo.Vacio_AbiertoParaInsertar;
                faseActual = FaseOperacionHerramienta.Fase2_ColocarHerramientaEnHusillo;
                ApuntarFlecha(puntoMontajeHusillo);
                MostrarMensaje("AIRE LIBERADO (Mordaza abierta).\nAcerca cualquier portaherramientas con la mano hacia el husillo.");
                break;

            case EstadoHusillo.Vacio_AbiertoParaInsertar:
                // Si volvió a presionar sin meter herramienta, cierra el aire
                if (socketHusillo != null) socketHusillo.socketActive = false;
                estadoHusillo = EstadoHusillo.Vacio_Cerrado;
                ApuntarFlecha(botonToolReleaseCabezal);
                MostrarMensaje("Mordaza cerrada (Husillo vacío).\nPresiona el botón del cabezal para abrir el aire cuando quieras meter una herramienta.");
                break;

            case EstadoHusillo.HerramientaEnSocket_SinSujetar:
                // Sujetar firmemente la herramienta recién insertada
                FijarHerramientaEnHusillo();
                faseActual = FaseOperacionHerramienta.Fase4_CerrarPuertasParaATC;
                if (controladorEncendido != null)
                    ApuntarFlecha(controladorEncendido.puertaIzquierda);
                MostrarMensaje("¡HERRAMIENTA SUJETADA EN EL HUSILLO!\nCierra las puertas para guardarla con MDI, o vuelve a presionar el botón si quieres sacarla con la mano.");
                break;

            case EstadoHusillo.HerramientaSujetada:
                // Liberar la herramienta actual para que el usuario pueda sacarla con la mano
                LiberarHerramientaParaSacarConMano();
                break;

            case EstadoHusillo.HerramientaLiberada_ParaSacarConMano:
                // Si no la sacó y volvió a presionar el botón, la vuelve a sujetar
                FijarHerramientaEnHusillo();
                MostrarMensaje("Herramienta nuevamente sujetada en el husillo.");
                break;
        }
    }


    private void AlInsertarHerramientaManual(SelectEnterEventArgs args)
    {
        if (cambioEnProceso) return;
        herramientaActualEnHusillo = args.interactableObject.transform;
        estadoHusillo = EstadoHusillo.HerramientaEnSocket_SinSujetar;
        faseActual = FaseOperacionHerramienta.Fase3_PresionarBotonCabezalSujetar;
        ApuntarFlecha(botonToolReleaseCabezal);
        MostrarMensaje("Herramienta en posición.\nPresiona de nuevo el botón del cabezal para cerrar el aire y sujetarla.");
    }

    private void AlRetirarHerramientaDeSocketSinSujetar(SelectExitEventArgs args)
    {
        if (estadoHusillo == EstadoHusillo.HerramientaEnSocket_SinSujetar)
        {
            herramientaActualEnHusillo = null;
            estadoHusillo = EstadoHusillo.Vacio_AbiertoParaInsertar;
            MostrarMensaje("Herramienta retirada antes de sujetar. El husillo sigue abierto.");
        }
    }

    private void FijarHerramientaEnHusillo()
    {
        if (herramientaActualEnHusillo == null) return;

        // 1. Guardar referencia local y cambiar el estado ANTES de apagar el socket
        //    para que el evento selectExited NO borre herramientaActualEnHusillo
        Transform herramientaSegura = herramientaActualEnHusillo;
        estadoHusillo = EstadoHusillo.HerramientaSujetada;

        XRGrabInteractable grab = herramientaSegura.GetComponent<XRGrabInteractable>();
        Rigidbody rb = herramientaSegura.GetComponent<Rigidbody>();

        // 2. Ahora sí apagamos el socket sin riesgo de perder la referencia
        if (socketHusillo != null) socketHusillo.socketActive = false;

        herramientaActualEnHusillo = herramientaSegura;

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

        // 3. Emparentar al Socket_Husillo_ y alinear en cero
        if (puntoMontajeHusillo != null)
        {
            herramientaActualEnHusillo.SetParent(puntoMontajeHusillo, true);
            herramientaActualEnHusillo.localPosition = Vector3.zero;
            herramientaActualEnHusillo.localRotation = Quaternion.identity;
        }
    }

    private void LiberarHerramientaParaSacarConMano()
    {
        if (herramientaActualEnHusillo == null) return;

        // Mantener el socket apagado hasta que el usuario retire la herramienta
        if (socketHusillo != null) socketHusillo.socketActive = false;

        XRGrabInteractable grab = herramientaActualEnHusillo.GetComponent<XRGrabInteractable>();
        Rigidbody rb = herramientaActualEnHusillo.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            // La dejamos cinemática mientras cuelga para que no salga disparada por el collider del Cabezal
            rb.isKinematic = true;
        }

        if (grab != null)
        {
            grab.retainTransformParent = false;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.enabled = true;
        }

        estadoHusillo = EstadoHusillo.HerramientaLiberada_ParaSacarConMano;
        ApuntarFlecha(herramientaActualEnHusillo);
        MostrarMensaje("AIRE LIBERADO (Mordaza abierta).\nAgarra la herramienta con tu mano y rétirala del husillo.");
    }




    // =========================================================================
    //  SECUENCIA DE TECLADO MDI -> T -> NÚMERO -> ATC FWD 

    //  Conectar al botón "MDI / DNC"
    public void BotonTeclaMDI()
    {
        if (!ValidarMaquinaEncendida() || cambioEnProceso) return;

        ReproducirSonido(sonidoBeepTecla);
        textoBufferMDI = "";
        herramientaSeleccionadaMDI = 0;

        faseActual = FaseOperacionHerramienta.Fase6_PresionarTeclaT;
        ApuntarFlecha(teclaLetraT);
        MostrarMensaje("MODO [MDI] ACTIVO.\nPresiona la tecla [ T ] en el teclado alfabético.");
    }

    // Conectar al botón de la letra "T"
    public void BotonTeclaLetraT()
    {

        if (!ValidarMaquinaEncendida() || cambioEnProceso) return;

        if (faseActual != FaseOperacionHerramienta.Fase6_PresionarTeclaT &&
            faseActual != FaseOperacionHerramienta.Fase7_DigitarNumeroHerramienta) return;

        ReproducirSonido(sonidoBeepTecla);
        textoBufferMDI = "T";
        herramientaSeleccionadaMDI = 0;

        faseActual = FaseOperacionHerramienta.Fase7_DigitarNumeroHerramienta;
        ApuntarFlecha(contenedorTecladoNumerico);
        MostrarMensaje("COMANDO MDI: [ T_ ]\nDigita el número de herramienta en el teclado numérico (1 al 20).");
    }

    //  Conectar a las teclas numéricas (0 al 9) pasando su número como parámetro int
    public void BotonTeclaNumero(int digito)
    {

        if (!ValidarMaquinaEncendida() || cambioEnProceso) return;

        if (faseActual != FaseOperacionHerramienta.Fase7_DigitarNumeroHerramienta &&
            faseActual != FaseOperacionHerramienta.Fase8_PresionarATCFWD) return;

        ReproducirSonido(sonidoBeepTecla);

        // Permite escribir de 1 a 20 (por ejemplo, si presiona 1 y luego 2 = 12)
        int nuevoValor = (herramientaSeleccionadaMDI * 10) + digito;
        if (nuevoValor > totalPosiciones || nuevoValor <= 0)
            nuevoValor = Mathf.Clamp(digito, 1, totalPosiciones);

        herramientaSeleccionadaMDI = nuevoValor;
        textoBufferMDI = "T" + herramientaSeleccionadaMDI;

        faseActual = FaseOperacionHerramienta.Fase8_PresionarATCFWD;
        ApuntarFlecha(teclaATCFWD);
        MostrarMensaje($"COMANDO MDI: [ {textoBufferMDI} ]\nAhora presiona la tecla [ATC FWD] (o ATC REV) para ejecutar el cambio.");
    }

    // Botón CANCEL por si el usuario se equivoca de número
    public void BotonTeclaCancel()
    {
        if (cambioEnProceso) return;
        ReproducirSonido(sonidoBeepTecla);
        herramientaSeleccionadaMDI = 0;
        textoBufferMDI = "T";
        faseActual = FaseOperacionHerramienta.Fase7_DigitarNumeroHerramienta;
        MostrarMensaje("Número borrado. COMANDO MDI: [ T_ ]\nVuelve a digitar el número de herramienta.");
    }

    // Conectar al botón "ATC FWD" (y "ATC REV")
    public void BotonEjecutarATCFWD()
    {
        if (!ValidarMaquinaEncendida() || cambioEnProceso) return;

        // Si el usuario seleccionó una herramienta con MDI + T + Número, ir a esa herramienta
        int destino = (herramientaSeleccionadaMDI >= 1 && herramientaSeleccionadaMDI <= totalPosiciones)
                      ? herramientaSeleccionadaMDI
                      : (posicionActualCarrusel % totalPosiciones) + 1;

        ReproducirSonido(sonidoBeepTecla);
        ApuntarFlecha(null);
       
        // Abrir la compuerta curva del carrusel antes de entrar al husillo
        StartCoroutine(SecuenciaCompletaATC(destino));
    }

    // =========================================================================
    // CORRUTINA DEL MOVIMIENTO FÍSICO DEL CARRUSEL Y CABEZAL

    // + sopoortar guardar en el mismo slot y vaarias herramientas
    private IEnumerator SecuenciaCompletaATC(int herramientaDestino)
    {
        cambioEnProceso = true;
        faseActual = FaseOperacionHerramienta.EjecutandoMovimientoATC;

        if (herramientaActualEnHusillo != null && estadoHusillo != EstadoHusillo.HerramientaSujetada)
            FijarHerramientaEnHusillo();

        MostrarMensaje($"EJECUTANDO [{textoBufferMDI} ATC FWD]:\nCambiando de T{posicionActualCarrusel} a T{herramientaDestino}...");

        Transform cabezal = controladorEncendido != null ? controladorEncendido.cabezalEjeZ : null;
        int indiceActual = posicionActualCarrusel - 1;

        // 1. Posicionar el cabezal suavemente (1.0s) antes de que entre el carrusel
        if (cabezal != null)
        {
            float alturaInicial = (herramientaActualEnHusillo != null) ? alturaCabezalNivelATC_Y : alturaCabezalArribaLibre_Y;
            yield return StartCoroutine(MoverCabezalConPlacas(cabezal, alturaInicial, 1.0f));
        }

        // 2. Entrada pausada del carrusel hacia el husillo abriendo la puerta curva (1.25s)
        ReproducirSonido(sonidoDeslizarATC);
        Vector3 destinoEntrada = posInicialATC + new Vector3(desplazamientoEntradaATC_X, 0f, 0f);
        yield return StartCoroutine(MoverCarruselYPuertaSimultaneo(destinoEntrada, anguloAperturaPuertaATC, 1.25f));

        // 3. Si hay herramienta en el husillo, liberarla y subir el cabezal suavemente (1.0s)
        Transform herramientaDejadaEnEsteCiclo = null;
        if (herramientaActualEnHusillo != null && atcPlatoGiratorio != null)
        {
            ReproducirSonido(sonidoAireToolRelease);
            yield return new WaitForSeconds(0.35f);

            herramientaDejadaEnEsteCiclo = herramientaActualEnHusillo;
            Transform padreSlot = (slotsCarrusel != null && indiceActual < slotsCarrusel.Length && slotsCarrusel[indiceActual] != null)
                                  ? slotsCarrusel[indiceActual]
                                  : atcPlatoGiratorio;

            herramientaActualEnHusillo.SetParent(padreSlot, true);
            herramientasEnMaquina[indiceActual] = herramientaActualEnHusillo;
            herramientaActualEnHusillo = null;
            estadoHusillo = EstadoHusillo.Vacio_Cerrado;

            // Subida suave del cabezal para librar el cono
            if (cabezal != null)
                yield return StartCoroutine(MoverCabezalConPlacas(cabezal, alturaCabezalArribaLibre_Y, 1.0f));
        }

        // 4. Girar el plato deteniéndose brevemente en cada ranura 
        if (herramientaDestino != posicionActualCarrusel)
        {
            yield return StartCoroutine(CorrutinaGirarPlato(herramientaDestino));
            yield return new WaitForSeconds(0.25f);
        }

        // 5. Bajada suave del cabezal para enganchar la herramienta
        int indiceNuevo = posicionActualCarrusel - 1;
        Transform herramientaEnRanuraDestino = (herramientasEnMaquina != null && indiceNuevo >= 0 && indiceNuevo < herramientasEnMaquina.Length)
                                               ? herramientasEnMaquina[indiceNuevo]
                                               : null;

        if (herramientaEnRanuraDestino != null && herramientaEnRanuraDestino != herramientaDejadaEnEsteCiclo)
        {
            if (cabezal != null)
                yield return StartCoroutine(MoverCabezalConPlacas(cabezal, alturaCabezalNivelATC_Y, 1.1f));

            ReproducirSonido(sonidoAireToolRelease);
            yield return new WaitForSeconds(0.3f);

            herramientaActualEnHusillo = herramientaEnRanuraDestino;
            herramientasEnMaquina[indiceNuevo] = null;
            FijarHerramientaEnHusillo();
        }
        else if (cabezal != null && herramientaDejadaEnEsteCiclo == null)
        {
            yield return StartCoroutine(MoverCabezalConPlacas(cabezal, alturaCabezalNivelATC_Y, 1.1f));
        }

        // 6. Retiro suave del carrusel hacia la izquierda cerrando la puerta curva 
        ReproducirSonido(sonidoDeslizarATC);
        yield return StartCoroutine(MoverCarruselYPuertaSimultaneo(posInicialATC, 0f, 1.3f));

        // 7. Si el cabezal quedó arriba tras guardar una herramienta, bajarlo suavemente a reposo
        if (herramientaActualEnHusillo == null && cabezal != null)
            yield return StartCoroutine(MoverCabezalConPlacas(cabezal, alturaCabezalNivelATC_Y, 1.0f));

        if (herramientaActualEnHusillo != null)
            MostrarMensaje($"¡CAMBIO COMPLETADO!\nHerramienta T{posicionActualCarrusel} montada en el husillo.");
        else
            MostrarMensaje($"¡HERRAMIENTA GUARDADA EN T{posicionActualCarrusel}!\nEl husillo está libre.");

        herramientaSeleccionadaMDI = 0;
        faseActual = FaseOperacionHerramienta.Fase6_PresionarTeclaT;
        cambioEnProceso = false;
    }

    private IEnumerator MoverCarruselYPuertaSimultaneo(Vector3 posDestinoATC, float anguloDestinoPuerta, float duracion)
    {
        Vector3 posIniATC = atcConjuntoDeslizable != null ? atcConjuntoDeslizable.localPosition : Vector3.zero;
        Quaternion rotIniPuerta = atcPuertaMovil != null ? atcPuertaMovil.localRotation : Quaternion.identity;
        Quaternion rotFinPuerta = Quaternion.AngleAxis(anguloDestinoPuerta, ejeGiroPlato.normalized);

        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duracion);

            if (atcConjuntoDeslizable != null)
                atcConjuntoDeslizable.localPosition = Vector3.Lerp(posIniATC, posDestinoATC, p);

            if (atcPuertaMovil != null)
                atcPuertaMovil.localRotation = Quaternion.Slerp(rotIniPuerta, rotFinPuerta, p);

            yield return null;
        }

        if (atcConjuntoDeslizable != null) atcConjuntoDeslizable.localPosition = posDestinoATC;
        if (atcPuertaMovil != null) atcPuertaMovil.localRotation = rotFinPuerta;
    }

    // Gira el plato deteniéndose ranura por ranura (efecto Cruz de Malta de la Haas real)
    private IEnumerator CorrutinaGirarPlato(int herramientaDestino)
    {
        if (atcPlatoGiratorio == null) yield break;

        int diferencia = herramientaDestino - posicionActualCarrusel;
        if (diferencia > totalPosiciones / 2) diferencia -= totalPosiciones;
        if (diferencia < -totalPosiciones / 2) diferencia += totalPosiciones;

        int pasosTotales = Mathf.Abs(diferencia);
        int direccionPaso = (diferencia >= 0) ? 1 : -1;
        float anguloPorRanura = -direccionPaso * (360f / totalPosiciones); // 18° por ranura

        float tiempoGiroUnaRanura = 0.48f; // Lo que tarda en avanzar una ranura
        float pausaEntreRanuras = 0.14f;   // El pequeño freno mecánico en cada ranura

        for (int i = 0; i < pasosTotales; i++)
        {
            ReproducirSonido(sonidoGiroPlato);

            Quaternion rotInicioPaso = atcPlatoGiratorio.localRotation;
            Quaternion rotFinPaso = rotInicioPaso * Quaternion.AngleAxis(anguloPorRanura, ejeGiroPlato.normalized);

            float t = 0f;
            while (t < tiempoGiroUnaRanura)
            {
                t += Time.deltaTime;
                // SmoothStep hace que acelere al salir de la ranura y frene suavemente al llegar a la siguiente
                float p = Mathf.SmoothStep(0f, 1f, t / tiempoGiroUnaRanura);
                atcPlatoGiratorio.localRotation = Quaternion.Slerp(rotInicioPaso, rotFinPaso, p);
                yield return null;
            }

            atcPlatoGiratorio.localRotation = rotFinPaso;

            // Actualizar el número de ranura actual paso a paso
            posicionActualCarrusel += direccionPaso;
            if (posicionActualCarrusel > totalPosiciones) posicionActualCarrusel = 1;
            if (posicionActualCarrusel < 1) posicionActualCarrusel = totalPosiciones;

            // Breve pausa mecánica en cada ranura antes de dar el siguiente paso
            if (i < pasosTotales - 1)
                yield return new WaitForSeconds(pausaEntreRanuras);
        }

        posicionActualCarrusel = herramientaDestino;
    }



    private IEnumerator RotarPuertaATC(float anguloDestino, float duracion)
    {
        Quaternion rotIni = atcPuertaMovil.localRotation;
        Quaternion rotFin = Quaternion.AngleAxis(anguloDestino, ejeGiroPlato.normalized);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            atcPuertaMovil.localRotation = Quaternion.Slerp(rotIni, rotFin, Mathf.SmoothStep(0f, 1f, t / duracion));
            yield return null;
        }
        atcPuertaMovil.localRotation = rotFin;
    }


    private IEnumerator MoverCabezalConPlacas(Transform cabezal, float desplazamientoYDestino, float duracion)
    {
        Vector3 inicio = cabezal.localPosition;
        Vector3 fin = new Vector3(inicio.x, posBaseCabezal.y + desplazamientoYDestino, inicio.z);
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            cabezal.localPosition = Vector3.Lerp(inicio, fin, Mathf.SmoothStep(0f, 1f, t / duracion));
            if (controladorEncendido != null)
                controladorEncendido.ActualizarCubiertasCabezal(cabezal.localPosition.y - posBaseCabezal.y);
            yield return null;
        }
    }

    private IEnumerator MoverObjetoLocal(Transform obj, Vector3 posDestino, float duracion)
    {
        Vector3 inicio = obj.localPosition;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            obj.localPosition = Vector3.Lerp(inicio, posDestino, Mathf.SmoothStep(0f, 1f, t / duracion));
            yield return null;
        }
        obj.localPosition = posDestino;
    }

    private IEnumerator EfectoPulsarBoton(Transform boton)
    {
        Vector3 posOriginal = boton.localPosition;
        Vector3 posHundida = posOriginal + new Vector3(0f, 0f, 0.005f);
        boton.localPosition = posHundida;
        yield return new WaitForSeconds(0.15f);
        boton.localPosition = posOriginal;
    }

    private bool ValidarMaquinaEncendida()
    {
        if (!exigirEncendidoCompleto || controladorEncendido == null) return true;
        if (controladorEncendido.pasoActual != ControladorEncendidoHaas.PasoEncendido.MaquinaLista)
        {
            MostrarMensaje("ALERTA:\nPrimero debes terminar los 7 pasos de encendido de la máquina.");
            return false;
        }
        return true;
    }

    private void ApuntarFlecha(Transform destino)
    {
        if (controladorEncendido != null)
            controladorEncendido.CambiarObjetivoGuiaPublico(destino);
    }

    private void MostrarMensaje(string msg)
    {
        Debug.Log("[ATC Haas] " + msg);
        if (controladorEncendido != null && controladorEncendido.textoGuiaUI != null)
            controladorEncendido.textoGuiaUI.text = msg;
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}