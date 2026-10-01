using System.Collections;
using UnityEngine;

namespace Vampire
{
    // Presentation-only arrival: no boss AI or hitboxes exist until the descent finishes.
    public sealed class BossSummonPresentation : MonoBehaviour
    {
        public const float SignalStepSeconds = .18f;
        public const float ArrivalSeconds = 2f;
        private SpriteRenderer terminal;
        private Sprite standby, active;
        private readonly SpriteRenderer[] arcs = new SpriteRenderer[3];
        private GameObject arrival;
        private bool cakeAltar;
        private Vector3 altarScale;

        public void Initialize(SpriteRenderer target)
        {
            terminal = target;
            var cake=OctoberArt.Get("OctoberUI/BossAltar");
            if(cake!=null)
            {
                var art=new GameObject("Roll cake altar art");art.transform.SetParent(transform,false);
                terminal=art.AddComponent<SpriteRenderer>();terminal.sprite=cake;
                if(target!=null){terminal.sortingLayerID=target.sortingLayerID;terminal.sortingOrder=target.sortingOrder;target.enabled=false;}
                float width=2.35f/cake.bounds.size.x;
                art.transform.localScale=altarScale=new Vector3(width/Mathf.Abs(transform.lossyScale.x),width/Mathf.Abs(transform.lossyScale.y),1);
                art.transform.localPosition=new Vector3(0,1.05f/Mathf.Abs(transform.lossyScale.y),0);
                standby=active=cake;cakeAltar=true;return;
            }
            standby = Resources.Load<Sprite>("BossSummonArt/Standby");
            active = Resources.Load<Sprite>("BossSummonArt/Active");
            if (terminal == null || standby == null || active == null) return;
            terminal.sprite = standby;
            terminal.color = Color.white;
            for (int i = 0; i < arcs.Length; i++)
            {
                var sr = new GameObject("TransmissionArc" + (i + 1)).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(terminal.transform, false);
                // Sprite canvas is 2 world units tall, with its pivot on the feet.
                sr.transform.localPosition = new Vector3(.025f, 1.70f, 0);
                sr.transform.localScale = Vector3.one * .60f;
                sr.sprite = Resources.Load<Sprite>("BossSummonArt/Signal" + (i + 1));
                sr.sortingLayerID = terminal.sortingLayerID;
                sr.sortingOrder = terminal.sortingOrder + 1;
                sr.enabled = false;
                arcs[i] = sr;
            }
        }

        public IEnumerator Transmit()
        {
            if(cakeAltar)
            {
                for(float t=0;t<1.8f;t+=Time.deltaTime)
                {
                    float pulse=Mathf.Max(0,Mathf.Sin(t*12));
                    terminal.color=Color.Lerp(Color.white,new Color(1,.68f,.76f),pulse*.6f);
                    terminal.transform.localScale=altarScale*(1+pulse*.025f);
                    yield return null;
                }
                terminal.color=Color.white;terminal.transform.localScale=altarScale;yield break;
            }
            if (terminal != null && active != null) terminal.sprite = active;
            yield return new WaitForSeconds(.2f);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int step = 0; step < 3; step++)
                {
                    if (arcs[step] != null) arcs[step].enabled = true;
                    yield return new WaitForSeconds(SignalStepSeconds);
                }
                yield return new WaitForSeconds(.12f);
                HideArcs();
                yield return new WaitForSeconds(.12f);
            }
        }

        public static Vector3 ArrivalOffset(float progress, float height)
        {
            float t = Mathf.Clamp01(progress);
            float sway = Mathf.Sin(t * Mathf.PI * 4) * .65f * Mathf.Sin(t * Mathf.PI);
            return new Vector3(sway, height * (1 - Mathf.SmoothStep(0, 1, t)), 0);
        }

        public IEnumerator Descend(GameObject bossPrefab, Vector3 landing, float scaleMultiplier = 1f)
        {
            if (bossPrefab == null) yield break;
            arrival = new GameObject("BossArrivalVisual");
            // Keep cleanup tied to this scene object, but use the prefab's world dimensions.
            arrival.transform.SetParent(transform, false);
            arrival.transform.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1);
            foreach (var source in bossPrefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!source.enabled || source.sprite == null || source.name.Contains("Shadow")) continue;
                bool visible = true;
                for (var p = source.transform; p != null && p != bossPrefab.transform; p = p.parent)
                    if (!p.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                var copy = new GameObject(source.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(arrival.transform, false);
                copy.transform.localPosition = (source.transform.position - bossPrefab.transform.position) * scaleMultiplier;
                copy.transform.localRotation = source.transform.rotation;
                copy.transform.localScale = source.transform.lossyScale * scaleMultiplier;
                copy.sprite = source.sprite; copy.color = source.color;
                copy.sharedMaterial = source.sharedMaterial;
                copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
            }
            float height = 7f;
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
                height = Mathf.Max(height, camera.transform.position.y + camera.orthographicSize + 2 - landing.y);
            for (float elapsed = 0; elapsed < ArrivalSeconds && arrival != null; elapsed += Time.deltaTime)
            {
                float t = elapsed / ArrivalSeconds;
                arrival.transform.position = landing + ArrivalOffset(t, height);
                arrival.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4) * 7 * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (arrival != null) arrival.transform.SetPositionAndRotation(landing, Quaternion.identity);
        }

        // Called immediately after the real pooled boss is ready, avoiding a blank frame.
        public void FinishArrival() { ClearArrival(); }

        public void ResetStandby()
        {
            HideArcs(); ClearArrival();
            if (terminal != null && standby != null) terminal.sprite = standby;
            if(cakeAltar&&terminal!=null){terminal.color=Color.white;terminal.transform.localScale=altarScale;}
        }
        private void HideArcs() { foreach (var sr in arcs) if (sr != null) sr.enabled = false; }
        private void ClearArrival()
        {
            if (arrival != null) { arrival.SetActive(false); Destroy(arrival); arrival = null; }
        }
        private void OnDisable() { ResetStandby(); }
    }
}
