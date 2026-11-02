using UnityEngine;

public class SeguidorSuaveVR : MonoBehaviour
{
    [Header("Cámara del XR Origin")]
    public Transform camaraVR;

    [Header("Posición frente al jugador")]
    public float distanciaFrente = 1.15f;
    public float alturaOffset = -0.22f;  
    public float desplazamientoLateral = 0.25f; 
    public float velocidadSeguimiento = 4.5f;

    private void Start()
    {
        if (camaraVR == null && Camera.main != null)
            camaraVR = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (camaraVR == null) return;

        // Proyectamos hacia adelante ignorando si el jugador inclina mucho el cuello arriba/abajo
        Vector3 adelantePlano = Vector3.ProjectOnPlane(camaraVR.forward, Vector3.up).normalized;
        if (adelantePlano.sqrMagnitude < 0.01f) adelantePlano = camaraVR.forward;

        Vector3 derechaPlano = Vector3.Cross(Vector3.up, adelantePlano).normalized;

        Vector3 posicionObjetivo = camaraVR.position
                                 + (adelantePlano * distanciaFrente)
                                 + (Vector3.up * alturaOffset)
                                 + (derechaPlano * desplazamientoLateral);

        // Movimiento suave para no marear en VR
        transform.position = Vector3.Lerp(transform.position, posicionObjetivo, Time.deltaTime * velocidadSeguimiento);

        // Hacer que el texto siempre mire de frente a los ojos del jugador
        Vector3 direccionMirada = transform.position - camaraVR.position;
        if (direccionMirada.sqrMagnitude > 0.001f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionMirada);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, Time.deltaTime * (velocidadSeguimiento * 1.5f));
        }
    }
}