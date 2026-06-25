using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 挂在 MissileProjectile 预制体上。
/// 飞行过程中沿轨迹生成半透明方块尾迹，方块向四周扩散并缩小淡出。
/// </summary>
public class MissileTrail : MonoBehaviour
{
    [Header("材质")]
    [Tooltip("方块使用的材质（Custom/HollowSquare Shader）")]
    public Material trailMaterial;

    [Header("生成频率")]
    [Tooltip("每隔多少距离生成一批方块。值越小方块越密集，建议 0.03~0.1")]
    public float spawnInterval = 0.05f;

    [Header("每批生成数量（随机区间）")]
    [Tooltip("每次生成方块数量的最小值")]
    public int spawnCountMin = 2;
    [Tooltip("每次生成方块数量的最大值")]
    public int spawnCountMax = 6;

    [Header("方块大小（随机区间）")]
    [Tooltip("方块初始大小的最小值（世界单位）")]
    public float squareSizeMin = 0.03f;
    [Tooltip("方块初始大小的最大值（世界单位）")]
    public float squareSizeMax = 0.08f;

    [Header("扩散范围（随机区间）")]
    [Tooltip("方块从生成点向四周偏移的最小距离（世界单位）")]
    public float spreadRadiusMin = 0.0f;
    [Tooltip("方块从生成点向四周偏移的最大距离（世界单位）")]
    public float spreadRadiusMax = 0.2f;

    [Header("漂移速度（随机区间）")]
    [Tooltip("方块生成后持续漂移速度的最小值（世界单位/秒）")]
    public float driftSpeedMin = 0.05f;
    [Tooltip("方块生成后持续漂移速度的最大值（世界单位/秒）")]
    public float driftSpeedMax = 0.35f;

    [Header("存活时长（随机区间）")]
    [Tooltip("方块从生成到完全消失的最短时间（秒）")]
    public float lifeDurationMin = 0.25f;
    [Tooltip("方块从生成到完全消失的最长时间（秒）")]
    public float lifeDurationMax = 0.5f;

    [Header("颜色")]
    [Tooltip("品红色（与蓝色交替出现）")]
    public Color colorMagenta = new Color(1f, 0f, 0.8f, 1f);
    [Tooltip("蓝色（与品红色交替出现）")]
    public Color colorBlue    = new Color(0f, 0.6f, 1f, 1f);

    // ─────────────────────────────────────────────────

    private Vector3 _lastSpawnPos;
    private bool    _colorToggle = false;
    private bool    _active      = false;

    private struct SquareData
    {
        public GameObject obj;
        public Material   mat;
        public float      elapsed;
        public float      duration;
        public float      initSize;
        public Vector3    velocity;
    }

    private List<SquareData> _squares = new List<SquareData>();

    // ─────────────────────────────────────────────────

    public void StartTrail()
    {
        _lastSpawnPos = transform.position;
        _active       = true;
    }

    public void StopTrail()
    {
        _active = false;
    }

    void Update()
    {
        if (_active)
        {
            float dist = Vector3.Distance(transform.position, _lastSpawnPos);
            if (dist >= spawnInterval)
            {
                int count = Random.Range(spawnCountMin, spawnCountMax + 1);
                for (int i = 0; i < count; i++)
                    SpawnSquare(transform.position);
                _lastSpawnPos = transform.position;
            }
        }

        for (int i = _squares.Count - 1; i >= 0; i--)
        {
            var s = _squares[i];
            s.elapsed += Time.deltaTime;

            float t     = Mathf.Clamp01(s.elapsed / s.duration);
            float size  = Mathf.Lerp(s.initSize, 0f, t);
            float alpha = Mathf.Lerp(1f, 0f, t);

            if (s.obj != null)
            {
                s.obj.transform.localScale = Vector3.one * size;
                s.obj.transform.position  += s.velocity * Time.deltaTime;
                if (s.mat != null)
                    s.mat.SetFloat("_Alpha", alpha);
            }

            _squares[i] = s;

            if (s.elapsed >= s.duration)
            {
                if (s.obj != null) Destroy(s.obj);
                _squares.RemoveAt(i);
            }
        }
    }

    private void SpawnSquare(Vector3 pos)
    {
        if (trailMaterial == null) return;

        // 随机扩散偏移（XY平面内）
        Vector2 randDir  = Random.insideUnitCircle.normalized;
        float   randDist = Random.Range(spreadRadiusMin, spreadRadiusMax);
        Vector3 offset   = new Vector3(randDir.x, randDir.y, 0f) * randDist;

        GameObject sq = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(sq.GetComponent<Collider>());

        float size = Random.Range(squareSizeMin, squareSizeMax);
        sq.transform.position   = pos + offset;
        sq.transform.localScale = Vector3.one * size;
        sq.transform.rotation   = Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f));

        // 交替品红/蓝
        Color col    = _colorToggle ? colorMagenta : colorBlue;
        _colorToggle = !_colorToggle;

        Material mat = new Material(trailMaterial);
        mat.SetColor("_Color", col);
        mat.SetFloat("_Alpha", 1f);
        mat.renderQueue = 4000;
        var renderer = sq.GetComponent<Renderer>();
        renderer.sortingLayerName = "For3DEffects";
        renderer.sortingOrder     = 0;
        renderer.material         = mat;

        // 随机漂移速度（XY平面内）
        float   speed = Random.Range(driftSpeedMin, driftSpeedMax);
        Vector3 vel   = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            0f).normalized * speed;

        _squares.Add(new SquareData
        {
            obj      = sq,
            mat      = mat,
            elapsed  = 0f,
            duration = Random.Range(lifeDurationMin, lifeDurationMax),
            initSize = size,
            velocity = vel
        });
    }

    void OnDestroy()
    {
        foreach (var s in _squares)
            if (s.obj != null) Destroy(s.obj);
        _squares.Clear();
    }
}