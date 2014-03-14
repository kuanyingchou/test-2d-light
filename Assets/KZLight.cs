﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class KZLight : MonoBehaviour {
    public float distance = 10;
    public GameObject[] targets;
    public float direction = 0, angleOfView = 60;
    public bool debug = true;
    private Mesh mesh;
    private GameObject light;
    private List<Vector3> hits = new List<Vector3>();
    private Vector3 lastHit;
    private float scale = 1.01f;

    public void Start() {
        light = GameObject.CreatePrimitive(PrimitiveType.Quad);
        light.name = "Light";
        light.transform.position = transform.position;
        Material lightMaterial =
                Resources.Load("Light", typeof(Material)) as Material;
        light.renderer.material = lightMaterial;
        mesh = light.GetComponent<MeshFilter>().mesh;
        mesh.MarkDynamic();
        //Debug.Log(Mathf.PI);
        //if(debug) UnitTest();
    }
    public void Update() {
        light.transform.position = transform.position;
        Vector3 lightSource = light.transform.position;
        List<Vector3> hits = Flash(lightSource, direction, angleOfView);
        UpdateLightMesh(lightSource, hits, mesh);
        //FindShadow();
    }
    public void FixedUpdate() {
    }

    public List<Vector3> Flash(Vector3 lightSource, float angleDeg, float viewDeg) {
        hits.Clear();
        RaycastHit hit;

        float angleRad = angleDeg * Mathf.Deg2Rad;
        float viewRad = viewDeg * Mathf.Deg2Rad;
        float start = angleRad - viewRad * .5f;
        float end = angleRad + viewRad * .5f;

        Vector3 dir = new Vector3(
                Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);

        Vector3 startDir = new Vector3(
                Mathf.Cos(start), Mathf.Sin(start), 0);
        if(Physics.Raycast(lightSource, startDir, out hit, distance)) {
            hits.Add(hit.point);
        } else {
            hits.Add(lightSource + startDir * distance);
        }

        Vector3 endDir = new Vector3(
                Mathf.Cos(end), Mathf.Sin(end), 0);
        if(Physics.Raycast(lightSource, endDir, out hit, distance)) {
            hits.Add(hit.point);
        } else {
            hits.Add(lightSource + endDir * distance);
        }

        for(int i = 0; i<targets.Length; i++) {
            GameObject o = targets[i];
            Transform t = o.transform;
            Collider c = o.collider;
            Mesh mesh = GetMesh(o);
            Vector3[] vertices = mesh.vertices;
            for(int j=0; j<vertices.Length; j++) {
                Vector3 vv = vertices[j];
                Vector3 ov = t.TransformPoint(vv);
                Vector3 ov2 = new Vector3(ov.x, ov.y, lightSource.z);
                Vector3 v = t.TransformPoint(new Vector3(
                        vv.x * scale, vv.y * scale, 0));
                Vector3 v2 = new Vector3(v.x, v.y, lightSource.z);

                if(Physics.Raycast(lightSource, v2 - lightSource, out hit, distance)) {
if(debug) Debug.DrawRay(lightSource, hit.point - lightSource, Color.green);
                    hits.Add(hit.point);
                    float hm = (hit.point - lightSource).sqrMagnitude;
                    float vm2 = (ov2 - lightSource).sqrMagnitude;
                    if(hm > vm2) {
                        hits.Add(ov2);
                    }
                }

            }
        }


        hits.RemoveAll(x => !IsVisible(x - lightSource, dir, viewDeg));
        //hits = hits.Distinct().ToList();
        //hits.Sort((a, b) => 
        //        Vector3.Angle(a - lightSource, startDir) >
        //        Vector3.Angle(b - lightSource, startDir) ? 
        //        1: -1);
        hits = hits.OrderBy(
                x => Vector3.Angle(x - lightSource, startDir)).ToList();
        return hits;
    }

    public void UpdateLightMesh(Vector3 lightSource, List<Vector3> hits, Mesh mesh) {
//Debug.DrawRay(Camera.main.transform.position, lightSource - Camera.main.transform.position, Color.red);
        if(hits.Count <= 0) {
            Debug.Log("what?");
            return;
        }
        if(debug) {
            Vector3 from = hits[0];
            for(int i=1; i<hits.Count; i++) {
                Vector3 to = hits[i];
                //Debug.Log(x);
                Debug.DrawLine(from, to, Color.red);
                from = to;
            }
            if(hits.Count > 1) {
                Debug.DrawLine(hits[hits.Count -1], hits[0], Color.red);
            }
        }

        //[ vertices
        Vector3[] vertices = new Vector3[hits.Count + 1];
        vertices[0] = Vector3.zero;
        for(int i=1; i<vertices.Length; i++) {
            vertices[i] = hits[i-1] - lightSource;
        }
        mesh.Clear();
        //Mesh mesh = new Mesh();
        mesh.vertices = vertices;

        //[ triangles
        int numTriangles = hits.Count - 1;
        int index = 0;
        int p = 1, q = 2;
        int[] triangles = new int[numTriangles * 3];
        for(int i=0; i<numTriangles; i++) {
            triangles[index++] = 0;
            triangles[index++] = p++;
            triangles[index++] = q++;
            //if(q >= vertices.Length) q = 1;
        }
        mesh.triangles = triangles;

/*
for(int i=0; i<triangles.Length; i++) {
    Debug.Log(triangles[i]);
}
*/

        Vector3[] normals = new Vector3[vertices.Length];
        for(int i=0; i<normals.Length; i++) {
            normals[i] = -Vector3.forward;
        }
        mesh.normals = normals;
/*
        Vector2[] uvs = new Vector2[vertices.Length];
        Bounds bounds = mesh.bounds;
        for(int i=0; i<uvs.Length; i++) {
            uvs[i] = new Vector2(
                    (bounds.size.x - vertices[i].x) / bounds.size.x,
                    (bounds.size.y - vertices[i].y) / bounds.size.y);
            //uvs[i] = Vector2.zero;
        }
        mesh.uv = uvs;
*/
        //mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();
    }



    private bool Similar(Vector3 a, Vector3 b, Vector3 lightSource, float err) {
        return Vector3.Angle(a - lightSource, b - lightSource) < err;
    }

    private Mesh GetMesh(GameObject o) {
        return o.GetComponent<MeshFilter>().mesh;
    }
    private float GetAngle(Vector3 dir) {
        float angle = Mathf.Atan2(dir.y, dir.x);
        //Debug.Log(angle);
        return angle;
        //return GetRadIn2PI(angle);
    }

    private int CompareAngle(float a, float b) {
        return (a - b)>0?1:-1;
    }

    private bool IsVisible(Vector3 p, Vector3 dir, float range) {
        float a = Vector3.Angle(p, dir);
if(a < 0) Debug.LogError(a);
        return a < range * .5f + 0.001f;
    }

    private static float TWO_PI = Mathf.PI * 2;

    private bool IsWithinAngles(float a, float start, float range, float err) {
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
    }
}
