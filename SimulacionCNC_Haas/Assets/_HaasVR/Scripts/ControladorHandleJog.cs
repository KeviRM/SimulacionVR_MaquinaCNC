using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControladorHandleJogHaas : MonoBehaviour
{
    public enum EjeSeleccionadoJog
    {
        Ninguno = 0,
        EjeX_LateralMesa = 1,
        EjeY_ProfundidadMesa = 2,
        EjeZ_AlturaCabezal = 3
    }

    [Header("Conexión con el Controlador Principal")]
    public ControladorEncendidoHaas controladorEncendido;
    public bool modoHandleJogActivo = false;
    public EjeSeleccionadoJog ejeActual = EjeSeleccionadoJog.EjeX_LateralMesa;

    [Header("Volante / Perilla Handle Jog")]
    public Transform ruedaHandleJog; // El dial circular seleccionado en tu captura
    public XRBaseInteractable interactableRuedaJog;
    public Vector3 ejeRotacionLocalRueda = new Vector3(0f, 0f, 1f);
    public float sensibilidadGiro = 350f;
    public float velocidadAvanceMaquina = 0.0008f; // Metros que avanza la mesa por cada grado de giro

    [Header("Límites de Recorrido de los Ejes (Respecto a Posición Inicial)")]
    public Vector2 limitesEjeX_Mesa = new Vector2(-0.35f, 0.35f);
    public Vector2 limitesEjeZ_ProfundidadMesa = new Vector2(-0.18f, 0.18f);
    public Vector2 limitesEjeY_AlturaCabezal = new Vector2(-0.05f, 0.25f);

    [Header("Cubiertas Telescópicas Laterales X (CubiertaMesaX1 a X5)")]
    public Transform[] cubiertasMesaX;
    private Vector3[] posIniCubiertasX;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sonidoBeepTecla;
    public AudioClip sonidoClickEncoderJog;

    private IXRSelectInteractor interactorRueda;
    private Vector3 posManoAnterior;
    private float anguloAcumuladoParaClick = 0f;

    private Vector3 posBaseMesa;
    private Vector3 posBaseCabezal;
    private bool basesInicializadas = false;

    private void Start()
    {
        if (interactableRuedaJog != null)
        {
            interactableRuedaJog.selectEntered.AddListener(AlAgarrarRuedaJog);
            interactableRuedaJog.selectExited.AddListener(AlSoltarRuedaJog);
        }

        if (cubiertasMesaX != null && cubiertasMesaX.Length > 0)
        {
            posIniCubiertasX = new Vector3[cubiertasMesaX.Length];
            for (int i = 0; i < cubiertasMesaX.Length; i++)
            {
                if (cubiertasMesaX[i] != null)
                    posIniCubiertasX[i] = cubiertasMesaX[i].localPosition;
            }
        }
    }

    private void InicializarBasesSiEsNecesario()
    {
        if (basesInicializadas || controladorEncendido == null) return;
        if (controladorEncendido.mesaEjeX != null)
            posBaseMesa = controladorEncendido.mesaEjeX.localPosition;
        if (controladorEncendido.cabezalEjeZ != null)
            posBaseCabezal = controladorEncendido.cabezalEjeZ.localPosition;
        basesInicializadas = true;
    }

    // --- BOTONES DEL TABLERO PARA ACTIVAR HANDLE JOG Y ELEGIR EJE ---

    // Conectar al botón "HANDLE JOG" (o ".01" / ".001")
    public void BotonActivarHandleJog()
    {
        if (controladorEncendido != null && controladorEncendido.pasoActual != ControladorEncendidoHaas.PasoEncendido.MaquinaLista)
        {

            if (GestorSeminarioVR.Instancia != null)
                GestorSeminarioVR.Instancia.MostrarAlertaTemporal("ALERTA JOG:\nPrimero completa el encendido y envía la máquina a Origen (Home).");
            else
                MostrarMensaje("ALERTA JOG:\nPrimero completa el encendido y envía la máquina a Origen (Home).");
            return;
        }

        InicializarBasesSiEsNecesario();
        modoHandleJogActivo = true;
        if (ejeActual == EjeSeleccionadoJog.Ninguno)
            ejeActual = EjeSeleccionadoJog.EjeX_LateralMesa;

        ReproducirSonido(sonidoBeepTecla);
        MostrarMensaje($"MODO [HANDLE JOG] ACTIVO ({ejeActual}).\nElige un eje (+X, +Y, +Z) o gira la perilla HANDLE JOG para mover la máquina.");
    }

    // C botón "+X" 
    public void BotonSeleccionarEjeX()
    {
        BotonActivarHandleJog();
        ejeActual = EjeSeleccionadoJog.EjeX_LateralMesa;
        MostrarMensaje("JOG EJE [ X ] SELECCIONADO (Movimiento lateral de mesa).\nSujeta y gira la rueda HANDLE JOG.");
    }

    //  "+Y" 
    public void BotonSeleccionarEjeY()
    {
        BotonActivarHandleJog();
        ejeActual = EjeSeleccionadoJog.EjeY_ProfundidadMesa;
        MostrarMensaje("JOG EJE [ Y ] SELECCIONADO (Acercar/Alejar mesa).\nSujeta y gira la rueda HANDLE JOG.");
    }

    //  "+Z" o "-Z" (Altura del cabezal)
    public void BotonSeleccionarEjeZ()
    {
        BotonActivarHandleJog();
        ejeActual = EjeSeleccionadoJog.EjeZ_AlturaCabezal;
        MostrarMensaje("JOG EJE [ Z ] SELECCIONADO (Subir/Bajar cabezal).\nSujeta y gira la rueda HANDLE JOG.");
    }

    private void AlAgarrarRuedaJog(SelectEnterEventArgs args)
    {
        if (args.interactorObject == null) return;
        InicializarBasesSiEsNecesario();
        modoHandleJogActivo = true;
        interactorRueda = args.interactorObject;
        posManoAnterior = interactorRueda.transform.position;
    }

    private void AlSoltarRuedaJog(SelectExitEventArgs args)
    {
        interactorRueda = null;
    }

    private void Update()
    {
        if (!modoHandleJogActivo || interactorRueda == null || interactorRueda.transform == null) return;

        // Calculamos cuánto movió la mano el usuario para girar el dial
        Vector3 posManoActual = interactorRueda.transform.position;
        Vector3 deltaMano = posManoActual - posManoAnterior;
        posManoAnterior = posManoActual;

        // Combinamos movimiento horizontal y vertical de la mano para que sea fácil de girar con láser o de cerca
        float impulsoGiro = (Vector3.Dot(deltaMano, transform.right) - Vector3.Dot(deltaMano, transform.up)) * sensibilidadGiro;
        if (Mathf.Abs(impulsoGiro) < 0.001f) return;

        // 1. Rotar visualmente la rueda de HANDLE JOG
        if (ruedaHandleJog != null)
        {
            ruedaHandleJog.localRotation *= Quaternion.AngleAxis(impulsoGiro, ejeRotacionLocalRueda.normalized);
        }

        // Sonido de pasos del encoder cada 10 grados
        anguloAcumuladoParaClick += Mathf.Abs(impulsoGiro);
        if (anguloAcumuladoParaClick >= 10f)
        {
            anguloAcumuladoParaClick = 0f;
            ReproducirSonido(sonidoClickEncoderJog);
        }

        // 2. Mover el eje seleccionado y sus cubiertas telescópicas
        MoverEjeSeleccionado(impulsoGiro * velocidadAvanceMaquina);
    }

    private void MoverEjeSeleccionado(float deltaMetros)
    {
        if (controladorEncendido == null) return;

        switch (ejeActual)
        {
            case EjeSeleccionadoJog.EjeX_LateralMesa:
                if (controladorEncendido.mesaEjeX != null)
                {
                    Vector3 pos = controladorEncendido.mesaEjeX.localPosition;
                    float nuevoDeltaX = Mathf.Clamp((pos.x - posBaseMesa.x) + deltaMetros, limitesEjeX_Mesa.x, limitesEjeX_Mesa.y);
                    pos.x = posBaseMesa.x + nuevoDeltaX;
                    controladorEncendido.mesaEjeX.localPosition = pos;

                    ActualizarCubiertasLateralesX(nuevoDeltaX);
                }
                break;

            case EjeSeleccionadoJog.EjeY_ProfundidadMesa:
                if (controladorEncendido.mesaEjeX != null)
                {
                    Vector3 pos = controladorEncendido.mesaEjeX.localPosition;
                    float nuevoDeltaZ = Mathf.Clamp((pos.z - posBaseMesa.z) + deltaMetros, limitesEjeZ_ProfundidadMesa.x, limitesEjeZ_ProfundidadMesa.y);
                    pos.z = posBaseMesa.z + nuevoDeltaZ;
                    controladorEncendido.mesaEjeX.localPosition = pos;

                    controladorEncendido.ActualizarCubiertasMesa(nuevoDeltaZ);
                }
                break;

            case EjeSeleccionadoJog.EjeZ_AlturaCabezal:
                if (controladorEncendido.cabezalEjeZ != null)
                {
                    Vector3 pos = controladorEncendido.cabezalEjeZ.localPosition;
                    float nuevoDeltaY = Mathf.Clamp((pos.y - posBaseCabezal.y) + deltaMetros, limitesEjeY_AlturaCabezal.x, limitesEjeY_AlturaCabezal.y);
                    pos.y = posBaseCabezal.y + nuevoDeltaY;
                    controladorEncendido.cabezalEjeZ.localPosition = pos;

                    controladorEncendido.ActualizarCubiertasCabezal(nuevoDeltaY);
                }
                break;
        }
    }

    private void ActualizarCubiertasLateralesX(float deltaX)
    {
        if (cubiertasMesaX == null || posIniCubiertasX == null) return;
        int n = cubiertasMesaX.Length;
        for (int i = 0; i < n; i++)
        {
            if (cubiertasMesaX[i] == null) continue;
            float peso = (n > 1) ? 1f - ((float)i / (n - 1)) : 1f;
            cubiertasMesaX[i].localPosition = posIniCubiertasX[i] + new Vector3(deltaX * peso, 0f, 0f);
        }
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