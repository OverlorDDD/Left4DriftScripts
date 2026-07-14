using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceProperties",
                 menuName  = "Track/Surface Properties")]
public class TrackSurfaceProperties : ScriptableObject
{
    [System.Serializable]
    public class SurfaceEntry
    {
        public SurfaceType type;

        [Range(0f, 1f)]  public float gripMultiplier      = 1f;
        [Range(0f, 2f)]  public float speedMultiplier     = 1f;
        [Range(0f, 1f)]  public float tractionMultiplier  = 1f;
        public float                  boostMultiplier     = 1f;
        public bool                   isSlippery          = false;
        public bool                   slowsDown           = false;
        public Color                  debugColor          = Color.white;
    }

    public SurfaceEntry[] surfaces = new SurfaceEntry[]
    {
        new SurfaceEntry { type=SurfaceType.Asphalt, gripMultiplier=1f,   speedMultiplier=1f,    debugColor=Color.grey   },
        new SurfaceEntry { type=SurfaceType.Grass,   gripMultiplier=0.8f, speedMultiplier=0.85f, debugColor=Color.green  },
        new SurfaceEntry { type=SurfaceType.Dirt,    gripMultiplier=0.7f, speedMultiplier=0.75f, debugColor=new Color(0.5f,0.3f,0f) },
        new SurfaceEntry { type=SurfaceType.Mud,     gripMultiplier=0.4f, speedMultiplier=0.5f,  slowsDown=true, debugColor=new Color(0.4f,0.2f,0f) },
        new SurfaceEntry { type=SurfaceType.Sand,    gripMultiplier=0.6f, speedMultiplier=0.65f, debugColor=Color.yellow },
        new SurfaceEntry { type=SurfaceType.Ice,     gripMultiplier=0.2f, speedMultiplier=1f,    isSlippery=true, debugColor=Color.cyan },
        new SurfaceEntry { type=SurfaceType.Snow,    gripMultiplier=0.5f, speedMultiplier=0.8f,  isSlippery=true, debugColor=Color.white },
        new SurfaceEntry { type=SurfaceType.Water,   gripMultiplier=0.3f, speedMultiplier=0.6f,  slowsDown=true,  debugColor=Color.blue  },
        new SurfaceEntry { type=SurfaceType.Boost,   gripMultiplier=1f,   speedMultiplier=1f,    boostMultiplier=1.5f, debugColor=Color.magenta },
        new SurfaceEntry { type=SurfaceType.Barrier, gripMultiplier=0f,   speedMultiplier=0f,    slowsDown=true,  debugColor=Color.red   },
    };

    public SurfaceEntry Get(SurfaceType type)
    {
        foreach (var s in surfaces)
            if (s.type == type) return s;
        return surfaces[0];
    }
}