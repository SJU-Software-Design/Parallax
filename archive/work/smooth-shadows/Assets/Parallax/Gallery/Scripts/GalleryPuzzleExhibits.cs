using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Render meshes and gameplay hulls are authored independently for ceramic art.
    public static class GalleryPuzzleExhibits
    {
        public const string VesselTitle = "01 · 청화 모란병";
        public const string WindowTitle = "02 · 겹창 조각";
        public const string TextureRelativePath = "GalleryExhibits/porcelain-peony-wrap.png";

        public static void AddTo(GalleryGame game)
        {
            var props = new List<GalleryProp>(game.props);
            for(int i=props.Count-1;i>=0;i--)
            {
                if(props[i].title!="01 · 청화 각병")continue;
                var old=props[i].gameObject;props.RemoveAt(i);old.SetActive(false);
                if(Application.isPlaying)UnityEngine.Object.Destroy(old);else UnityEngine.Object.DestroyImmediate(old);
            }
            bool hasVessel = false, hasWindow = false;
            foreach(var p in props)
            {
                hasVessel|=p.title==VesselTitle;hasWindow|=p.title==WindowTitle;
                if(p.title==VesselTitle&&p.GetComponent<GalleryCurvedCaster>()==null)p.gameObject.AddComponent<GalleryCurvedCaster>();
            }
            if (hasVessel && hasWindow) return;

            Material porcelain = Material("Exhibit porcelain", new Color(.96f,.94f,.87f));
            Material blue = Material("Exhibit cobalt", new Color(.11f,.25f,.48f));
            Material brass = Material("Exhibit brushed brass", new Color(.88f,.65f,.32f));
            Material clay = Material("Exhibit terracotta", new Color(.83f,.37f,.23f));
            Material seal = Material("Exhibit interaction seal", new Color(.21f,.91f,.77f), true);
            Material painted = GalleryCeramicMesh.Glaze("Porcelain peony glaze",Color.white,LoadPorcelain());
            if (!hasVessel) props.Add(Vessel(game.transform, porcelain, blue, painted, seal));
            if (!hasWindow) props.Add(Window(game.transform, brass, clay, blue, seal));
            game.props = props.ToArray();
        }

        static Texture2D LoadPorcelain()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StreamingAssets/" + TextureRelativePath);
                if (asset == null) throw new FileNotFoundException("Missing exhibit texture", TextureRelativePath);
                return asset;
            }
#endif
#if PARALLAX_EXISTING_PLAYER
            // The shipped player strips unused PNG decoding APIs. A lossless raw
            // copy lets the same original image use the already-present SetPixels32.
            string path = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "Parallax_Data/StreamingAssets/"+TextureRelativePath.Replace(".png",".rgba"));
            var bytes=File.ReadAllBytes(path);
            int width=bytes[0]|bytes[1]<<8|bytes[2]<<16|bytes[3]<<24;
            int height=bytes[4]|bytes[5]<<8|bytes[6]<<16|bytes[7]<<24;
            if(width<=0||height<=0||bytes.Length!=8+width*height*4)throw new InvalidOperationException("Invalid raw porcelain texture");
            var texture=new Texture2D(width,height,TextureFormat.RGB24,true);
            var pixels=new Color32[width*height];
            for(int i=0;i<pixels.Length;i++){int b=8+i*4;pixels[i]=new Color32(bytes[b],bytes[b+1],bytes[b+2],bytes[b+3]);}
            texture.SetPixels32(pixels);texture.Apply();
#else
            string path = Path.Combine(Application.streamingAssetsPath, TextureRelativePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidDataException("Unable to load exhibit porcelain texture: " + path);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 4;
#endif
            texture.name = "Original blue peony porcelain wrap";
            texture.filterMode = FilterMode.Trilinear;
            return texture;
        }

        static Material Material(string name, Color color, bool flat = false)
        {
            var shader = Shader.Find(flat ? "ShadeLink/Flat" : "Parallax/GallerySurface");
            if (shader == null) throw new InvalidOperationException("Gallery exhibit shader is missing");
            var material = new Material(shader) { name = name, color = color };
            GalleryWorld.Materials.Add(material);
            return material;
        }

        static GalleryProp Root(Transform parent, string title, int zone, Vector3 position, bool platform)
        {
            var go = new GameObject(title);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var prop = go.AddComponent<GalleryProp>();
            prop.title = title; prop.zone = zone; prop.platform = platform;
            prop.minScale = .45f; prop.maxScale = 2.8f;
            return prop;
        }

        static GameObject Solid(GalleryProp prop, string name, Vector3 at, Vector3 size, Material material)
        {
            return GalleryWorld.Box(name, at, size, material, prop.transform);
        }

        static void Seal(GalleryProp prop, Vector3 at, Material seal)
        {
            var go=new GameObject("Movable turquoise seal");
            go.transform.SetParent(prop.transform,false);go.transform.localPosition=at;
            go.transform.localScale=new Vector3(.23f,.055f,.008f);
            go.AddComponent<MeshFilter>().sharedMesh=prop.GetComponentInChildren<MeshFilter>().sharedMesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=seal;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        static GalleryProp Vessel(Transform parent, Material ivory, Material blue, Material painted, Material seal)
        {
            var p = Root(parent, VesselTitle, 0, new Vector3(3.25f,GalleryCeramicMesh.PivotHeight,5.35f), false);
            var line=GalleryCeramicMesh.Glaze("Cobalt rim glaze",new Color(.035f,.11f,.29f),null,.75f);
            GalleryCeramicMesh.Visual(p.transform,"Curved porcelain body",GalleryCeramicMesh.Vase(),painted);
            GalleryCeramicMesh.Visual(p.transform,"Cobalt lip line",GalleryCeramicMesh.Ring(.326f,2.055f,.006f),line);
            GalleryCeramicMesh.Visual(p.transform,"Cobalt foot line",GalleryCeramicMesh.Ring(.301f,.059f,.004f),line);
            GalleryCeramicMesh.Colliders(p);
            p.gameObject.AddComponent<GalleryCurvedCaster>();
            GalleryCeramicMesh.ExhibitLighting(parent);
            p.Remember();
            Plaque(parent,"청화 모란병", "시선으로 크기를 바꾸고 · 곡선의 그늘 만들기",new Vector3(3.25f,.015f,4.35f));
            return p;
        }

        static GalleryProp Window(Transform parent, Material brass, Material clay, Material blue, Material seal)
        {
            // Two interlocking open rectangular frames and one low movable foot.
            var p = Root(parent, WindowTitle, 1, new Vector3(-4.3f,1.40f,17.1f), false);
            Solid(p,"Sculpture foundation",new Vector3(0,-1.25f,0),new Vector3(2.35f,.30f,.90f),blue);
            Frame(p,"Brass outer window",new Vector3(-.18f,.04f,-.13f),1.86f,2.28f,.24f,.25f,brass);
            Frame(p,"Terracotta inner window",new Vector3(.49f,-.10f,.19f),1.32f,1.92f,.21f,.22f,clay);
            Seal(p,new Vector3(.64f,-1.23f,-.456f),seal);
            p.Remember();
            Plaque(parent,"겹창 조각", "돌려서 그늘을 겹치기 · 눕혀서 다리로 쓰기",new Vector3(-4.3f,.015f,15.95f));
            return p;
        }

        static void Frame(GalleryProp p, string label, Vector3 center, float width, float height, float bar, float depth, Material material)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Solid(p,label+" upright",center+Vector3.right*(side*(width-bar)*.5f),new Vector3(bar,height,depth),material);
                Solid(p,label+" horizontal",center+Vector3.up*(side*(height-bar)*.5f),new Vector3(width-2*bar,bar,depth),material);
            }
        }

        static void Plaque(Transform parent,string title,string description,Vector3 position)
        {
            var go=new GameObject("Exhibit plaque · "+title);
            go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(90,0,0);
            var label=go.AddComponent<GalleryLabel>();
            label.caption=title+"\n"+description;label.size=.17f;label.color=new Color(.9f,.77f,.51f);
        }
    }
}
