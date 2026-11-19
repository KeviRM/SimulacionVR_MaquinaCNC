using UnityEngine;

public class PuntoGuiaVR : MonoBehaviour
{
    [Tooltip(" Si arrastras un Empty hijo aquí, la flecha usará exactamente la posición y rotación de ese Empty.")]
    public Transform anclajePersonalizado;

    [Tooltip("Si no usas anclajePersonalizado, desplaza la flecha respecto al botón (en coordenadas del mundo o locales).")]
    public Vector3 offsetPosicion = new Vector3(0f, 0.12f, -0.08f);
    public Vector3 rotacionEulerFlecha = new Vector3(0f, 0f, 0f);
    public bool usarEspacioLocalDelBoton = false;

    [Tooltip("Dirección en la que rebotará la flecha (ej. 0,1,0 para arriba/abajo, o 0,0,-1 para frente/atrás frente al panel)")]
    public Vector3 ejeReboteMundo = Vector3.up;

    public Vector3 ObtenerPosicionBase()
    {
        if (anclajePersonalizado != null)
            return anclajePersonalizado.position;

        return usarEspacioLocalDelBoton
            ? transform.TransformPoint(offsetPosicion)
            : transform.position + offsetPosicion;
    }

    public Quaternion ObtenerRotacion()
    {
        if (anclajePersonalizado != null)
            return anclajePersonalizado.rotation;

        return usarEspacioLocalDelBoton
            ? transform.rotation * Quaternion.Euler(rotacionEulerFlecha)
            : Quaternion.Euler(rotacionEulerFlecha);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = ObtenerPosicionBase();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, 0.025f);
        Gizmos.DrawLine(transform.position, pos);
    }
}