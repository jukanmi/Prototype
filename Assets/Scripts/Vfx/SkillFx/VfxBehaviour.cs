using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 VFX 컴포넌트들이 똑같이 복사해 쓰던 도우미 모음.
/// 머티리얼·난수·로컬 평면 변환·동적 메시 버퍼처럼 효과마다 다르지 않은 것만 둔다.
/// </summary>
public abstract class VfxBehaviour : MonoBehaviour
{
    protected Material mat;
    protected System.Random rng;

    // 메시 하나를 채우는 버퍼 묶음. uv3 은 쓰는 효과만 채운다 (비어 있으면 채널을 넘기지 않음)
    protected class Buffers
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector2> uv0 = new List<Vector2>();
        public readonly List<Vector4> uv1 = new List<Vector4>();
        public readonly List<Vector4> uv2 = new List<Vector4>();
        public readonly List<Vector4> uv3 = new List<Vector4>();
        public readonly List<int> tris = new List<int>();

        public void Clear()
        {
            verts.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); uv3.Clear(); tris.Clear();
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            m.SetVertices(verts);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetUVs(2, uv2);
            if (uv3.Count > 0) m.SetUVs(3, uv3);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
        }
    }

    /// <summary>캐릭터가 보는 방향에 맞춰 좌우 반전합니다. (캐릭터를 scale.x 로 뒤집는다면 필요 없음)</summary>
    public void SetFacing(bool facingRight)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        transform.localScale = s;
    }

    protected float Rand(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    protected static float SStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    protected static Vector3 Polar(float angle, float length)
    {
        return new Vector3(Mathf.Cos(angle) * length, Mathf.Sin(angle) * length, 0f);
    }

    /// <summary>월드 위치 -> 이 오브젝트의 로컬 XY 평면 (로컬 Z 를 따라 눌러 붙임)</summary>
    protected Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    protected static Mesh NewMesh(string meshName)
    {
        Mesh m = new Mesh();
        m.name = meshName;
        m.hideFlags = HideFlags.DontSave;
        m.MarkDynamic();
        return m;
    }

    protected void SetupRenderer(MeshRenderer r, string layerName, int order)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sortingLayerName = layerName;
        r.sortingOrder = order;
        r.enabled = false;
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    protected static void AddQuad(Buffers b, Vector3 center, Vector3 ax, Vector3 ay, Vector4 mode, Vector4 prm)
    {
        int q = b.verts.Count;
        AddVertex(b, center - ax - ay, new Vector2(0f, 0f), mode, prm);
        AddVertex(b, center - ax + ay, new Vector2(0f, 1f), mode, prm);
        AddVertex(b, center + ax - ay, new Vector2(1f, 0f), mode, prm);
        AddVertex(b, center + ax + ay, new Vector2(1f, 1f), mode, prm);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 메시를 짜는 좌표가 이미 로컬 평면이므로 z 만 눌러 붙임
    protected static void AddVertex(Buffers b, Vector3 local, Vector2 uv, Vector4 mode, Vector4 prm)
    {
        local.z = 0f;
        b.verts.Add(local);
        b.uv0.Add(uv);
        b.uv1.Add(mode);
        b.uv2.Add(prm);
    }
}
