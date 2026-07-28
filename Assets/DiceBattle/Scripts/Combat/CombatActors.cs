using System;
using System.Collections;
using DiceBattle.Core;
using DiceBattle.Data;
using DiceBattle.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DiceBattle.Combat
{
    public class PlayerHealthImplementation : MonoBehaviour
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action<int, int> Changed;
        public void Initialize(int hp) { Maximum = Current = hp; Changed?.Invoke(Current, Maximum); }
        public void Damage(int amount) { Current = Mathf.Max(0, Current - Mathf.Max(0, amount)); Changed?.Invoke(Current, Maximum); }
    }

    public class PlayerCombatControllerImplementation : MonoBehaviour
    {
        [SerializeField] RectTransform visual;
        [SerializeField] Image body;
        Transform worldVisual;
        [SerializeField] PlayerHealth health;
        public PlayerHealth Health
        {
            get
            {
                if(!health)health=GetComponent<PlayerHealth>();
                return health;
            }
        }
        public void Build(int hp)
        {
            health = GetComponent<PlayerHealth>() ?? gameObject.AddComponent<PlayerHealth>(); health.Initialize(hp);
            var go = RuntimeUI.Panel(transform, "HeroVisual", new Color(.12f, .48f, .85f), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(-65,-80), new Vector2(65,80));
            visual = (RectTransform)go.transform; body = go.GetComponent<Image>();
            RuntimeUI.Label(go.transform, "Glyph", "◆", 80, TextAnchor.MiddleCenter, Color.white);
            if(Application.isPlaying)StartBreathing();
        }
        protected void Awake(){health=GetComponent<PlayerHealth>();}
        protected void Start(){if(visual)StartBreathing();}
        void StartBreathing(){DOTween.Kill("heroBreathe");visual.DOScale(1.035f,.8f).SetEase(Ease.InOutSine).SetLoops(-1,LoopType.Yoyo).SetId("heroBreathe").SetLink(gameObject);}
        public void AttachWorldVisual(Transform value){worldVisual=value;if(body)body.enabled=false;worldVisual.DOScale(1.035f,.8f).SetEase(Ease.InOutSine).SetLoops(-1,LoopType.Yoyo).SetLink(worldVisual.gameObject);}
        IEnumerator Breathe()
        {
            while (true) { if (visual) visual.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2f) * .025f); yield return null; }
        }
        public IEnumerator Attack(RectTransform target)
        {
            if(worldVisual){Vector3 p=worldVisual.localPosition;var s=DOTween.Sequence().SetLink(worldVisual.gameObject);s.Append(worldVisual.DOLocalMoveY(p.y+.55f,.09f)).Append(worldVisual.DOLocalMove(p,.12f).SetEase(Ease.OutBack));yield return s.WaitForCompletion();yield break;}
            Vector2 start = visual.anchoredPosition;
            var sequence=DOTween.Sequence().SetLink(gameObject);
            sequence.Append(DOTween.To(()=>visual.anchoredPosition,x=>visual.anchoredPosition=x,start+Vector2.up*86,.09f).SetEase(Ease.OutQuad));
            sequence.Append(DOTween.To(()=>visual.anchoredPosition,x=>visual.anchoredPosition=x,start,.12f).SetEase(Ease.OutBack));
            sequence.Join(visual.DOPunchScale(Vector3.one*.18f,.18f,7,.6f));
            yield return sequence.WaitForCompletion();
        }
        public IEnumerator Hit()
        {
            var original = body.color;
            var sequence=DOTween.Sequence().SetLink(gameObject);
            sequence.Join((worldVisual?worldVisual:visual).DOShakePosition(.22f,worldVisual?.localScale.x*.12f??10f,16,80f,false,true));
            sequence.Join(DOTween.To(()=>body.color,x=>body.color=x,Color.white,.07f).SetLoops(2,LoopType.Yoyo));
            yield return sequence.WaitForCompletion(); body.color=original;
        }
        public IEnumerator Die()
        {
            var sequence=DOTween.Sequence().SetLink(gameObject);
            var targetVisual=worldVisual?worldVisual:visual;
            sequence.Join(targetVisual.DOScale(0,.45f).SetEase(Ease.InBack));
            sequence.Join(targetVisual.DORotate(new Vector3(0,0,90),.45f).SetEase(Ease.InQuad));
            yield return sequence.WaitForCompletion();
        }
    }

    public class EnemyControllerImplementation : MonoBehaviour
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] Image body;
        [SerializeField] Image selection;
        [SerializeField] Text hpText;
        [SerializeField] Text intentText;
        [SerializeField] Text nameText;
        [SerializeField] RectTransform visual;
        [SerializeField] Button button;
        Transform worldVisual;
        public int CurrentHp { get; private set; }
        public int MaximumHp { get; private set; }
        public int AttackDamage { get; private set; }
        public int CurrentDistance { get; private set; }
        public int MovementAmount => definition.MovementAmount;
        public int Countdown { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsArcher => definition != null && definition.Behavior == EnemyBehaviorType.Archer;
        public bool IsTargetable => !IsDead;
        public EnemyDefinition Definition => definition;
        public RectTransform Visual => visual;
        public void AttachWorldVisual(Transform value){worldVisual=value;if(body)body.enabled=false;}
        public event Action<EnemyController> Selected;

        public void Initialize(EnemySpawnEntry entry)
        {
            definition = entry.definition;
            MaximumHp = entry.hpOverride > 0 ? entry.hpOverride : definition.MaximumHp; CurrentHp = MaximumHp;
            AttackDamage = entry.damageOverride > 0 ? entry.damageOverride : definition.AttackDamage;
            CurrentDistance = entry.distanceOverride > 0 ? entry.distanceOverride : definition.StartingDistance;
            Countdown = definition.AttackCountdown;
            if(body==null)BuildView();
            body.color=definition.VisualColor;nameText.text=definition.DisplayName;visual.localScale=Vector3.one*definition.VisualScale;Refresh();
        }
        protected void Awake(){if(!button)button=GetComponent<Button>();if(button)button.onClick.AddListener(HandleSelected);}
        void HandleSelected()=>Selected?.Invoke((EnemyController)this);
        void BuildView()
        {
            body = GetComponent<Image>() ?? gameObject.AddComponent<Image>(); button = GetComponent<Button>() ?? gameObject.AddComponent<Button>(); button.targetGraphic=body;
            body.color=definition?definition.VisualColor:Color.red; visual=(RectTransform)transform;
            selection=RuntimeUI.Panel(transform,"Selection",new Color(.2f,1f,.8f,.35f),Vector2.zero,Vector2.one,new Vector2(-10,-10),new Vector2(10,10)).GetComponent<Image>();
            selection.transform.SetAsFirstSibling(); selection.enabled=false;
            nameText=RuntimeUI.Label(transform,"Name",definition?definition.DisplayName:"Enemy",22,TextAnchor.UpperCenter,Color.white);
            hpText=RuntimeUI.Label(transform,"HP","",30,TextAnchor.LowerCenter,Color.white);
            intentText=RuntimeUI.Label(transform,"Intent","",20,TextAnchor.MiddleCenter,Color.yellow);
            Refresh();
        }
#if UNITY_EDITOR
        public void EditorBuildView(){BuildView();UnityEditor.EditorUtility.SetDirty(this);}
#endif
        public void SetSelected(bool value)
        {
            if(!selection)return;
            DOTween.Kill(selection);
            selection.enabled=value;
            if(!value)return;
            var color=selection.color;
            color.a=.18f;
            selection.color=color;
            DOTween.To(
                    ()=>selection.color,
                    x=>selection.color=x,
                    new Color(color.r,color.g,color.b,.42f),
                    .65f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1,LoopType.Yoyo)
                .SetTarget(selection)
                .SetLink(gameObject);
        }
        public void Damage(int amount) { if(IsDead)return; CurrentHp=Mathf.Max(0,CurrentHp-amount); Refresh(); }
        public void MarkDead(){ IsDead=true; SetSelected(false); }
        public void AdvanceCountdown(){ Countdown--; if(Countdown<=0) Countdown=definition.AttackCountdown; Refresh(); }
        public bool ArcherWillAttack() => Countdown <= 1;
        public IEnumerator Move()
        {
            int before=CurrentDistance; CurrentDistance=Mathf.Max(0,CurrentDistance-MovementAmount);
            Vector2 start=visual.anchoredPosition; Vector2 end=start+Vector2.down*(before-CurrentDistance)*25;
            for(float t=0;t<1;t+=Time.deltaTime/.25f){visual.anchoredPosition=Vector2.Lerp(start,end,t);yield return null;} visual.anchoredPosition=end; Refresh();
        }
        public IEnumerator Attack(RectTransform hero)
        {
            if(worldVisual){Vector3 p=worldVisual.localPosition;var s=DOTween.Sequence().SetLink(worldVisual.gameObject);s.Append(worldVisual.DOLocalMoveY(p.y-.4f,.1f)).Append(worldVisual.DOLocalMove(p,.14f).SetEase(Ease.OutBack));yield return s.WaitForCompletion();yield break;}
            Vector2 start=visual.anchoredPosition;
            var sequence=DOTween.Sequence().SetLink(gameObject);
            sequence.Append(DOTween.To(()=>visual.anchoredPosition,x=>visual.anchoredPosition=x,start+Vector2.down*48,.1f).SetEase(Ease.OutQuad));
            sequence.Append(DOTween.To(()=>visual.anchoredPosition,x=>visual.anchoredPosition=x,start,.14f).SetEase(Ease.OutBack));
            yield return sequence.WaitForCompletion();
        }
        public IEnumerator Hit()
        {
            Color original=body.color;
            var sequence=DOTween.Sequence().SetLink(gameObject);
            sequence.Join((worldVisual?worldVisual:visual).DOShakePosition(.18f,worldVisual?.localScale.x*.12f??12f,18,80,false,true));
            sequence.Join(DOTween.To(()=>body.color,x=>body.color=x,Color.white,.06f).SetLoops(2,LoopType.Yoyo));
            yield return sequence.WaitForCompletion();body.color=original;
        }
        public IEnumerator Die()
        {
            MarkDead();
            var sequence=DOTween.Sequence().SetLink(gameObject);
            var targetVisual=worldVisual?worldVisual:visual;
            sequence.Join(targetVisual.DOScale(0,.4f).SetEase(Ease.InBack));
            sequence.Join(DOTween.To(()=>body.color,x=>body.color=x,new Color(body.color.r,body.color.g,body.color.b,0),.3f));
            sequence.Join(targetVisual.DORotate(new Vector3(0,0,18),.4f));
            yield return sequence.WaitForCompletion();
            if(worldVisual)Destroy(worldVisual.gameObject);
            Destroy(gameObject);
        }
        void Refresh()
        {
            if(hpText) hpText.text=$"{CurrentHp}/{MaximumHp} HP";
            if(intentText) intentText.text=definition==null?"INTENT":IsArcher?$"RANGED {Countdown}":$"ATTACK {AttackDamage}";
        }
    }

    public sealed class EnemyTargetingController
    {
        public EnemyController Selected { get; private set; }
        public void Select(EnemyController enemy)
        {
            if(enemy==null||!enemy.IsTargetable)return;
            Selected?.SetSelected(false); Selected=enemy; Selected.SetSelected(true);
        }
        public EnemyController ValidateOrAuto(System.Collections.Generic.IReadOnlyList<EnemyController> enemies)
        {
            if(Selected!=null&&Selected.IsTargetable)return Selected;
            EnemyController best=null;
            foreach(var enemy in enemies) if(enemy!=null&&enemy.IsTargetable)
            {
                if(best==null) best=enemy;
                else if(best.IsArcher&& !enemy.IsArcher) best=enemy;
                else if(best.IsArcher==enemy.IsArcher && ((enemy.IsArcher&&enemy.CurrentHp<best.CurrentHp)||(!enemy.IsArcher&&(enemy.CurrentDistance<best.CurrentDistance||(enemy.CurrentDistance==best.CurrentDistance&&enemy.CurrentHp<best.CurrentHp))))) best=enemy;
            }
            if(best!=null)Select(best); return best;
        }
    }
}
