using UnityEngine;

public class QuickPlayGlowDebug : MonoBehaviour
{
    private GameObject _lastHit;

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            GameObject hit = null;
            foreach (var h in hits)
            {
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
                hit = h.collider.gameObject;
                break;
            }

            if (hit != _lastHit)
            {
                _lastHit = hit;
                if (hit != null)
                    Debug.Log($"[Hit] {hit.name} | {GetPath(hit)} | BoxCollider={hit.GetComponent<BoxCollider>()!=null} | BoxCollider2D={hit.GetComponent<BoxCollider2D>()!=null}");
                else
                    Debug.Log("[Hit] 无命中");
            }
        }
        else _lastHit = null;
    }

    static string GetPath(GameObject o) { string p=o.name; var t=o.transform.parent; while(t!=null){p=t.name+"/"+p;t=t.parent;} return p; }
}