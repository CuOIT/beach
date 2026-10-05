using System;
using UnityEngine;

namespace WaveLab
{
    // Editor-baked data, shared by authored scenes and procedurally created controllers.
    public sealed class WaveLabImmersionCatalog : ScriptableObject
    {
        public const string ResourcePath = "WaveLab/ImmersionCatalog";
        public Material underwater, exposed, foam;
        public Mesh foamQuad;
        public Texture2D atlas;
        public Slice[] slices;

        [Serializable] public sealed class Slice
        {
            public string meshName;
            public Vector4 uv, localRect;
            public float distanceRange;
        }

        public void Configure(MeshRenderer renderer)
        {
            string meshName=renderer.GetComponent<MeshFilter>().sharedMesh.name;
            foreach(var slice in slices)
            {
                if(slice.meshName!=meshName)continue;
                var state=renderer.GetComponent<ShoreObjectState>();
                if(!state)state=renderer.gameObject.AddComponent<ShoreObjectState>();
                state.Configure(renderer,underwater,exposed,foam,foamQuad);
                state.ConfigureSilhouette(atlas,slice.uv,slice.localRect,slice.distanceRange);
                return;
            }
            throw new InvalidOperationException("Missing baked WaveLab silhouette: "+meshName+". Run Wave Lab > Upgrade Object Immersion.");
        }
    }
}
