using UnityEngine;

namespace WaveLab
{
    public enum ImmersionState { Exposed, PartiallySubmerged, FullySubmerged }

    // A complete underwater base and a softly revealed upper pass. The base prevents
    // a transparent seam while the exposed portion emerges through a curved meniscus.
    public sealed class ShoreObjectState : MonoBehaviour
    {
        [SerializeField] MeshRenderer submergedRenderer;
        [SerializeField] MeshRenderer exposedRenderer;
        [SerializeField] MeshRenderer contactFoam;
        [SerializeField] Texture2D silhouetteAtlas;
        [SerializeField] Vector4 silhouetteUV;
        [SerializeField] Vector4 silhouetteLocalRect;
        [SerializeField] float silhouetteDistanceRange;
        [SerializeField, Range(.5f,2)] float heightScale=1.35f;
        [SerializeField] float submergedFraction;
        [SerializeField] float waterDepth;
        [SerializeField] float surfaceY;
        [SerializeField] float foamStrength;
        [SerializeField] ImmersionState state;
        MaterialPropertyBlock objectProperties, foamProperties;
        MeshFilter sourceMesh;
        public float SubmergedFraction=>submergedFraction;
        public float WaterDepth=>waterDepth;
        public float SurfaceY=>surfaceY;
        public float FoamStrength=>foamStrength;
        public ImmersionState State=>state;
        public MeshRenderer SubmergedRenderer=>submergedRenderer;
        public MeshRenderer ExposedRenderer=>exposedRenderer;
        public MeshRenderer ContactFoam=>contactFoam;
        public Texture2D SilhouetteAtlas=>silhouetteAtlas;
        public Vector4 SilhouetteUV=>silhouetteUV;
        public Vector4 SilhouetteLocalRect=>silhouetteLocalRect;
        public float SilhouetteDistanceRange=>silhouetteDistanceRange;

        public void ConfigureSilhouette(Texture2D atlas,Vector4 uv,Vector4 localRect,float distanceRange)
        {
            silhouetteAtlas=atlas;silhouetteUV=uv;silhouetteLocalRect=localRect;silhouetteDistanceRange=distanceRange;
            contactFoam.transform.localPosition=new Vector3(localRect.x,localRect.y,0);
            contactFoam.transform.localRotation=Quaternion.identity;
            contactFoam.transform.localScale=new Vector3(localRect.z*.5f,localRect.w*.5f,1);
        }

        public void Configure(MeshRenderer lower,Material underwater,Material above,Material foam,Mesh foamQuad)
        {
            submergedRenderer=lower;submergedRenderer.sharedMaterial=underwater;
            if(!exposedRenderer)
            {
                var top=new GameObject("Above water · clipped upper portion",typeof(MeshFilter),typeof(MeshRenderer));
                top.transform.SetParent(transform,false);
                top.GetComponent<MeshFilter>().sharedMesh=GetComponent<MeshFilter>().sharedMesh;
                exposedRenderer=top.GetComponent<MeshRenderer>();
            }
            exposedRenderer.sharedMaterial=above;
            if(!contactFoam)
            {
                var rim=new GameObject("Contact foam · surface intersection",typeof(MeshFilter),typeof(MeshRenderer));
                rim.transform.SetParent(transform,false);rim.GetComponent<MeshFilter>().sharedMesh=foamQuad;
                contactFoam=rim.GetComponent<MeshRenderer>();
            }
            contactFoam.sharedMaterial=foam;
            foreach(var renderer in new[]{submergedRenderer,exposedRenderer,contactFoam})
            { renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false; }
        }

        public void Refresh(WaveLabController controller)
        {
            if(!submergedRenderer||!exposedRenderer||!contactFoam)return;
            if(objectProperties==null)objectProperties=new MaterialPropertyBlock();
            if(foamProperties==null)foamProperties=new MaterialPropertyBlock();
            Bounds bounds=submergedRenderer.bounds;
            Vector2 ground=transform.position;
            if(!sourceMesh)sourceMesh=GetComponent<MeshFilter>();
            // Use unrotated art height so a lean cannot change the depth state.
            float artHeight=sourceMesh?sourceMesh.sharedMesh.bounds.size.y*Mathf.Abs(transform.lossyScale.y):bounds.size.y;
            float height=Mathf.Max(.44f,artHeight*heightScale)*controller.objectHeight;
            waterDepth=SurfMath.ObjectWaterDepth(ground,controller.beachSlope);
            submergedFraction=Mathf.Clamp01(waterDepth/height);
            state=submergedFraction>=.97f?ImmersionState.FullySubmerged:
                submergedFraction<=.06f?ImmersionState.Exposed:ImmersionState.PartiallySubmerged;
            float extent=Mathf.Max(.15f,bounds.size.y);
            surfaceY=Mathf.Lerp(bounds.min.y-extent*.24f,bounds.max.y+extent*.24f,submergedFraction);
            float excessDepth=Mathf.Max(0,waterDepth-height);
            int order=500+Mathf.RoundToInt((5-ground.y)*10);
            bool active=controller.showObjectImmersion;
            submergedRenderer.sortingOrder=active?order-400:order;
            exposedRenderer.sortingOrder=order;
            exposedRenderer.enabled=active;

            objectProperties.SetFloat("_StateEnabled",active?1:0);
            objectProperties.SetFloat("_ObjectImmersion",submergedFraction);
            objectProperties.SetFloat("_ObjectSurfaceY",surfaceY);
            objectProperties.SetFloat("_ObjectExcessDepth",excessDepth);
            objectProperties.SetFloat("_ObjectFeather",extent*controller.waterlineSoftness);
            Vector4 shape=new Vector4(bounds.center.x,Mathf.Max(.05f,bounds.extents.x),extent,bounds.min.y);
            float objectSeed=height*13.71f;
            objectProperties.SetVector("_ObjectShape",shape);
            objectProperties.SetFloat("_ObjectSeed",objectSeed);
            submergedRenderer.SetPropertyBlock(objectProperties);exposedRenderer.SetPropertyBlock(objectProperties);

            foamStrength=SurfMath.ContactFoam(submergedFraction)*controller.objectFoamAmount;
            contactFoam.enabled=active&&controller.showFoam&&foamStrength>.008f&&silhouetteAtlas;
            contactFoam.sortingOrder=order+1;
            // The UV field follows the sprite transform, including rotation. Only its
            // water intersection moves; there is no expanding circular foam geometry.
            if(silhouetteAtlas)foamProperties.SetTexture("_SilhouetteAtlas",silhouetteAtlas);
            foamProperties.SetVector("_SilhouetteUV",silhouetteUV);
            foamProperties.SetFloat("_SdfRange",silhouetteDistanceRange*Mathf.Abs(transform.lossyScale.x));
            foamProperties.SetFloat("_FoamWidth",Mathf.Clamp(extent*.022f,.009f,.025f)*controller.contactFoamWidth);
            foamProperties.SetFloat("_ObjectImmersion",submergedFraction);
            foamProperties.SetFloat("_ObjectSurfaceY",surfaceY);
            foamProperties.SetFloat("_ObjectFeather",extent*controller.waterlineSoftness);
            foamProperties.SetFloat("_ObjectSeed",objectSeed);
            foamProperties.SetVector("_ObjectShape",shape);
            foamProperties.SetFloat("_ContactStrength",foamStrength);
            foamProperties.SetFloat("_ContactSeed",transform.position.x*3.1f+transform.position.y*.7f);
            contactFoam.SetPropertyBlock(foamProperties);
        }
    }
}
