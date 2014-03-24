﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class KZLight : MonoBehaviour {
    public bool debug = true;

    //[ basic properties
    /*[Range(-180, 180)]*/ public float direction = 0; 
    /*[Range(0, 720)]*/ public float angleOfView = 90;
    /*[Range(1, 20)]*/ public float radius = 10; 
    
    public Material lightMaterial;
    public Color color = new Color(1, 1, 1, 1); 
    private Color oldColor; //used for live update
    public Color tint = new Color(1, .94f, .59f, 1);
    /*[Range(0, 1)]*/ public float alpha = .5f;

    public bool enableTint = false;
    public bool enableFallOff = true;

    public bool enablePerlin = false;
    public float perlinScale = 5;
    public float perlinStart = 5;

    //[ advanced properties
    public int textureWidth = 128;
    public int textureHeight = 128;
    public int numberOfRays = 128;
    //public float rayDensity = 1;

    //public int eventThreshold = 5; //: TODO

    /*[Range(1, 10)]*/ public int numberOfDuplicates = 1;
    private int oldNumberOfDuplicates;

    /*[Range(0, 5)]*/ public float duplicateDiff = .5f;
    public float duplicateZDiff = .1f;

    //[ private 
    protected bool dynamicUpdate = true;
    protected static float TWO_PI = Mathf.PI * 2;
    protected Mesh[] meshes;
    protected GameObject[] lights;
    protected List<RaycastHit> hits = new List<RaycastHit>();
    protected KZTexture texture;
    protected Texture2D texture2d;
    protected Dictionary<GameObject, int> seenObjects= 
            new Dictionary<GameObject, int>();
    private Dictionary<GameObject, int> lastSeenObjects= 
            new Dictionary<GameObject, int>();

    public void Start() {
        if(lightMaterial == null) {
            //lightMaterial = CreateMaterial();
            Debug.LogError("Please assign a material!");
            return;
        }
        lightMaterial = CreateMaterial(lightMaterial);
        UpdateProperties();
        
        //if(debug) UnitTest();
    }

    public void LateUpdate() {
        if(lightMaterial == null) return;

        if(dynamicUpdate) UpdateProperties();

        SetLightPositions();

        for(int i=0; i<numberOfDuplicates; i++) {
            Vector3 pos = lights[i].transform.position;
            List<RaycastHit> hits = 
                    CircularScan(pos, direction, angleOfView, 
                    radius);
            UpdateLightMesh(meshes[i], pos, hits);
            lightMaterial.mainTexture = CreateTexture(hits);
        }
    }

    //[ private

    private static Material CreateMaterial(Material m) {
        //return m;
        //Material material = (Material)GameObject.Instantiate(m);
        Material material = new Material(m);
        return material;
    }

    public virtual KZTexture Filter(KZTexture texture) {
        if(enableTint) ApplyColorWithTint(texture, color, tint, alpha);
        else ApplyColor(texture, color, alpha);
        if(enableFallOff) ApplyGradient(texture, alpha);
        if(enablePerlin) ApplyPerlin(texture, perlinStart, perlinScale);
        return texture;
    }

    private Texture2D CreateTexture(List<RaycastHit> hits) {
        //[ use a smaller width here to create a blurry effect 
        texture2d = Filter(texture).ToTexture2D(texture2d);
        return texture2d;
    }

    private static void ApplyColor(
            KZTexture texture, Color c, float alphaScale) {
        texture.Clear(new Color(c.r, c.g, c.b, c.a * alphaScale));
    }

    private static void ApplyColorWithTint(
            KZTexture texture, Color c, Color tint, float alphaScale) {
        for(int y=0; y<texture.height; y++) {
            Color t = KZTexture.GetTint(
                    c, tint, (float)y / (texture.height-1));
            for(int x=0; x<texture.width; x++) {
                texture.SetPixel(x, y, 
                        new Color(t.r, t.g, t.b, t.a * alphaScale));
            }
        }
                
    }

    private static void ApplySoftEdges(
            KZTexture texture, int numOfPixels) {
        for(int y=0; y<texture.height; y++) {
            Color color = KZTexture.GetColor(texture.GetPixel(0, y), 0);
            for(int i=0; i<numOfPixels; i++) {
                texture.SetPixel(i, y, color);
                texture.SetPixel(texture.width - 1 - i, y, color);
            }
        }
    }

    private static void ApplyGradient(KZTexture texture, float maxAlpha) {
        for(int y=0; y<texture.height; y++) {
            float a = maxAlpha - ((float)y/(texture.height-1) * maxAlpha);
            for(int x=0; x<texture.width; x++) {
                Color c = texture.GetPixel(x, y);
                texture.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
            }
        }
    }
    private static void ApplyPerlin(
            KZTexture texture, float perlinStart, float perlinScale) {
        for(int x=0; x<texture.width; x++) {
            float perlin = Mathf.PerlinNoise(
                        perlinStart +
                        (float)x / texture.width * perlinScale, 0);
            for(int y=0; y<texture.height; y++) {
                //Debug.Log(perlin);
                Color c = texture.GetPixel(x, y);
                texture.SetPixel(x, y, new Color(
                        c.r, c.g, c.b, Mathf.Min(1, perlin * c.a)));
            }
        }
    }

    private void UpdateProperties() {
        if(IsDirty()) {
            Initialize();
        }
    }

    private bool IsDirty() {
        return oldNumberOfDuplicates != numberOfDuplicates;
    }

    private void Initialize() {
        if(lights != null) {
            for(int i=0; i<lights.Length; i++) {
                GameObject.DestroyImmediate(lights[i]);
            }
        }
        lights = new GameObject[numberOfDuplicates];
        meshes = new Mesh[numberOfDuplicates];
        for(int i=0; i<numberOfDuplicates; i++) {
            lights[i] = new GameObject();
            lights[i].name = "Light-"+i;
            lights[i].transform.parent = transform;
            lights[i].layer = gameObject.layer; 

            MeshRenderer renderer = lights[i].AddComponent<MeshRenderer>();
            renderer.material = lightMaterial;

            MeshFilter filter = lights[i].AddComponent<MeshFilter>();
            meshes[i] = filter.mesh;
            //meshes[i].MarkDynamic();
        }
        oldNumberOfDuplicates = numberOfDuplicates;

        texture = new KZTexture(textureWidth, textureHeight);
        texture2d = new Texture2D(textureWidth, textureHeight, 
                TextureFormat.ARGB32, false);
                //TextureFormat.RGB24, false);
        texture2d.wrapMode = TextureWrapMode.Clamp;
    }
    
    private void SetLightPositions() {
        if(numberOfDuplicates == 1) {
            lights[0].transform.localPosition = Vector3.zero;
        } else {
            PlaceLightsInCircle();
        } 
    }

    private void PlaceLightsInCircle() {
        float angle = 0;
        for(int i=0; i<numberOfDuplicates; i++) {
            Vector3 diff = new Vector3(
                    Mathf.Cos(angle), 
                    Mathf.Sin(angle), 
                    0) * 
                    duplicateDiff;
            diff += new Vector3(0, 0, 
                    transform.position.z + duplicateZDiff * i);
            lights[i].transform.localPosition = diff;
            angle += TWO_PI / numberOfDuplicates;
        }
    }

    private List<RaycastHit> CircularScan(
            Vector3 center, 
            float angleDeg, 
            float viewDeg, 
            float radius) {

        hits.Clear();
        RaycastHit hit;

        float angleRad = angleDeg * Mathf.Deg2Rad;
        float viewRad = viewDeg * Mathf.Deg2Rad;
        float start = angleRad - viewRad * .5f;
        float end = angleRad + viewRad * .5f;

        float angle = end;
        for(int i=0; i<numberOfRays; i++) {
            Vector3 d= ToVector3(angle); 
            if(Physics.Raycast(center, d, out hit, 
                    Mathf.Abs(radius))) {
                hits.Add(hit);
                Track(hit.transform.gameObject);
            } else {
                RaycastHit h = new RaycastHit();
                h.point = center + d*radius;
                h.distance = radius;
                hits.Add(h);
            }
            angle -= viewRad / (numberOfRays-1);
        }

        //TODO: block check
        //hits = SimplifyHits(hits); 
        HandleEvents();
        if(debug) DrawHits(center, hits);
        //hits.Reverse(); //TODO: remove this
        return hits;
    }

    private static Vector3 ToVector3(float angle) {
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
    }

    private void Track(GameObject obj) {
        if(seenObjects.ContainsKey(obj)) {
            int count = seenObjects[obj];
            seenObjects[obj] = count + 1;
        } else {
            seenObjects.Add(obj, 1);
        }
    }

    private void SendLeave(GameObject obj) {
        if(obj) obj.SendMessage("LeaveLight", 
                SendMessageOptions.DontRequireReceiver);
    }
    private void SendEnter(GameObject obj) {
        if(obj) obj.SendMessage("EnterLight", 
                SendMessageOptions.DontRequireReceiver);
    }
    private void HandleEvents() {
        foreach(var obj in seenObjects.Keys) {
            if(!lastSeenObjects.ContainsKey(obj)) {
                SendEnter(obj);
            }
        }
        foreach(var obj in lastSeenObjects.Keys) {
            if(!seenObjects.ContainsKey(obj)) {
                SendLeave(obj);
            }
        }
        Dictionary<GameObject, int> temp = lastSeenObjects;
        lastSeenObjects = seenObjects;
        seenObjects = temp;
        seenObjects.Clear();
    }

    private void DrawHits(Vector3 lightSource, List<RaycastHit> hits) {
        for(int i=0; i<hits.Count; i++) {
            Debug.DrawRay(lightSource, 
                    hits[i].point - lightSource, Color.green);
        }
    }

    //>>> didn't see much improvement, just moved burden from gpu to cpu
    private List<RaycastHit> SimplifyHits(List<RaycastHit> hits) {
        List<RaycastHit> reducedHits = new List<RaycastHit>();
        if(hits.Count > 2) {
            reducedHits.Add(hits[0]);
            reducedHits.Add(hits[1]);
            Vector3 last = hits[1].point - hits[0].point;
            for(int i=2; i<hits.Count; i++) {
                Vector3 diff = hits[i].point - hits[i-1].point;
                if(Similar(Vector3.Angle(diff, last), 0, 0.001f)) {
                    reducedHits.RemoveAt(reducedHits.Count - 1);
                }
                reducedHits.Add(hits[i]);
                last = diff;
            }
        }
        return reducedHits;
    }

    private void UpdateLightMesh(
            Mesh mesh, Vector3 pos, List<RaycastHit> hits) {
//Debug.DrawRay(Camera.main.transform.position, lightSource - Camera.main.transform.position, Color.red);
        if(hits.Count <= 0) {
            Debug.Log("hits nothing!");
            mesh.Clear();
            return;
        }

        
        if(debug) {
            DrawLightPolygon(hits);
        }

        mesh.Clear();
        Vector3[] vertices = CreateVertices(
                hits, pos, direction, angleOfView, radius);
        mesh.vertices = vertices;
        mesh.triangles = CreateTriangles(vertices);
        mesh.normals = CreateNormals(vertices);
        mesh.uv = CreateUV(vertices, hits, radius);

        //mesh.RecalculateNormals();
        //mesh.RecalculateBounds();
        mesh.Optimize();
    }

    private void DrawLightPolygon(List<RaycastHit> hits) {
        Vector3 from = hits[0].point;
        for(int i=1; i<hits.Count; i++) {
            Vector3 to = hits[i].point;
            //Debug.Log(x);
            Debug.DrawLine(from, to, Color.red);
            from = to;
        }
        //if(hits.Count > 1) {
        //    Debug.DrawLine(hits[hits.Count -1], hits[0], Color.red);
        //}
    }
    public virtual Vector3[] CreateVertices(
            List<RaycastHit> hits, Vector3 pos, 
            float direction, float angleOfView, 
            float range) {

        int numTriangles = hits.Count - 1;
        Vector3[] vertices = new Vector3[numTriangles * 3];
        int p = 0;
        int index = 0;
        for(int i=0; i<numTriangles; i++) {
            vertices[index++] = Vector3.zero;
            vertices[index++] = hits[p++].point - pos;
            vertices[index++] = hits[p].point - pos;
        }
        return vertices;
    }
    public virtual int[] CreateTriangles(Vector3[] vertices) {
        int[] triangles = new int[vertices.Length];
        for(int i=0; i<triangles.Length; i++) {
            triangles[i] = i;
        }
        return triangles;
    }
    public virtual Vector2[] CreateUV(
            Vector3[] vertices, List<RaycastHit> hits, float range) {
        Vector2[] uvs = new Vector2[vertices.Length];
        float x = 1;
        int index = 0;
        int hitIndex = 1;
        //float span = uvs.Length / 3; 
        float y = hits[0].distance / range;
        while(index < uvs.Length) {
            uvs[index++] = new Vector2(x, 0);
            uvs[index++] = new Vector2(x, y);
            x -= 1f / uvs.Length;
            y = hits[hitIndex++].distance / range;
            //if(x < 0) Debug.Log("!!! x = "+x);
            uvs[index++] = new Vector2(x, y);
        }
        return uvs;
    }
    public virtual Vector3[] CreateNormals(Vector3[] vertices) {
        Vector3[] normals = new Vector3[vertices.Length];
        for(int i=0; i<normals.Length; i++) {
            normals[i] = -Vector3.forward;
        }
        return normals;
    }

    //[ utilities
    private float GetAngle(Vector3 dir) {
        return Mathf.Atan2(dir.y, dir.x);
    }

    private bool Similar(float a, float b, float err) {
        return Mathf.Abs(a - b) < err;
    }

    //[ tests
    private bool IsWithinAngles(
            float a, float start, float range, float err) {
        a = GetValidRad(a);
        start = GetValidRad(start);
        if(a > start - err && a < start + range + err) return true;
        else return false;
    }

    // return a in -Mathf.PI to Mathf.PI
    private float GetValidRad(float a) {
        float res;
        if(a > Mathf.PI) {
            res = a % TWO_PI - TWO_PI;
        } else if(a < -Mathf.PI){
            res = a % TWO_PI + TWO_PI;
        } else {
            res = a;
        }
        if(res < -Mathf.PI || res > Mathf.PI) Debug.LogError(res + " !!!");
        return res;
    }

    // return a in 0 to 2 * Mathf.PI
    private float GetRadIn2PI(float a) {
        float res;
        if(a > TWO_PI) {
            res = a % TWO_PI;
        } else if(a < 0){
            res = a % TWO_PI + TWO_PI;
        } else {
            res = a;
        }
        if(res < 0 || res > TWO_PI) Debug.LogError(res + " !!!");
        return res;
    }
    private void TestPressure() {
        for(int i=0; i< 100; i++) {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.transform.position = new Vector3(i, 0, 0);
        }
    }
    //[ tests
    private void TestIsWithinAngles() {
        for(float start = -Mathf.PI; start < Mathf.PI; start += Mathf.PI / 180) {
            float end = start + Mathf.PI / 2;
            for(float angle = start; angle < end; angle+=(Mathf.PI / 180)) {
                bool isWithin = IsWithinAngles(angle, start, end, 0.01f);
                if(!isWithin) Debug.LogError(string.Format(
                        "angle = {0}, start={1}, end={2} => {3}",
                        angle * Mathf.Rad2Deg,
                        start * Mathf.Rad2Deg,
                        end * Mathf.Rad2Deg,
                        isWithin));
            }
        }

    }
    private void UnitTest() {
        TestIsWithinAngles();
        TestPressure();
    }

/*
    private bool Similar(Vector3 a, Vector3 b, Vector3 lightSource, float err) {
        return Vector3.Angle(a - lightSource, b - lightSource) < err;
    }

    private Mesh GetMesh(GameObject o) {
        return o.GetComponent<MeshFilter>().meshes;
    }

    private int CompareAngle(float a, float b) {
        return (a - b)>0?1:-1;
    }

    private bool IsVisible(Vector3 p, Vector3 dir, float range) {
        float a = Vector3.Angle(p, dir);
        //if(a < 0) Debug.LogError(a);
        return a < range * .5f + 0.001f;
    }

*/
}