using UnityEngine;

public class TrackSurfaceTrigger : MonoBehaviour
{
    public SurfaceType surfaceType = SurfaceType.Asphalt;

    // CarController читає цей компонент через OnCollisionStay
    // і застосовує множники з TrackSurfaceProperties
}