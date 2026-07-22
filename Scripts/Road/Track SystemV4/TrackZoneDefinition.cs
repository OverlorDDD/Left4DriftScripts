using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TrackZoneDefinition",
                 menuName  = "Track/Zone Definition")]
public class TrackZoneDefinition : ScriptableObject
{
    [System.Serializable]
    public class Zone
    {
        public string    name         = "Road";
        public float     innerRadius  = 0f;   // від центру сплайну
        public float     outerRadius  = 5f;
        public ZoneType  type         = ZoneType.Road;
        public Material  material;
        public bool      generateMesh    = true;
        public bool      generatePhysics = true;

        [Header("Поверхня (для машини)")]
        public SurfaceType surfaceType = SurfaceType.Asphalt;
    }

    public List<Zone> zones = new List<Zone>
    {
        new Zone { name="Road",     innerRadius=0,  outerRadius=5,  type=ZoneType.Road,     surfaceType=SurfaceType.Asphalt },
        new Zone { name="Shoulder", innerRadius=5,  outerRadius=8,  type=ZoneType.Shoulder, surfaceType=SurfaceType.Grass   },
        new Zone { name="Barrier",  innerRadius=8,  outerRadius=9,  type=ZoneType.Barrier,  surfaceType=SurfaceType.Barrier, generateMesh=false }
    };
}

public enum ZoneType
{
    Road,
    Shoulder,
    Decoration,
    Barrier,
    Boost,
    Water
}

public enum SurfaceType
{
    Asphalt,
    Grass,
    Dirt,
    Mud,
    Sand,
    Ice,
    Snow,
    Water,
    Boost,
    Barrier
}