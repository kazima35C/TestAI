using System;
using System.Collections;
using DiceBattle.Core;
using DiceBattle.Data;
using DiceBattle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DiceBattle.Combat
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action<int, int> Changed;
        public void Initialize(int hp) { Maximum = Current = hp; Changed?.Invoke(Current, Maximum); }
        public void Damage(int amount) { Current = Mathf.Max(0, Current - Mathf.Max(0, amount)); Changed?.Invoke(Current, Maximum); }
    }

    public sealed class PlayerCombatController : MonoBehaviour
    {
        RectTransform visual;
        Image body;
        public PlayerHealth Health { get; private set; }
        public void Build(int hp)
        {
            Health = gameObject.AddComponent<PlayerHealth>(); Health.Initialize(hp);
            var go = RuntimeUI.Panel(transform, "HeroVisual", new Color(.12f, .48f, .85f), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(-65,-80), new Vector2(65,80));
            visual = (RectTransform)go.transform; body = go.GetComponent<Image>();
            RuntimeUI.Label(go.transform, "Glyph", "◆", 80, TextAnchor.MiddleCenter, Color.white);
            StartCoroutine(Breathe());
        }
        IEnumerator Breathe()
        {
            while (true) { if (visual) visual.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2f) * .025f); yield return null; }
        }
        public IEnumerator Attack(RectTransform target)
        {
            Vector2 start = visual.anchoredPosition;
            for (float t=0;t<1;t+=Time.deltaTime/.18f) { visual.anchoredPosition = Vector2.Lerp(start, start + Vector2.up*80, Mathf.Sin(t*Mathf.PI)); yield return null; }
            visual.anchoredPosition = start;
        }
        public IEnumerator Hit()
        {
            var original = body.color;
            for(int i=0;i<5;i++){ body.color=i%2==0?Color.white:original; visual.anchoredPosition=UnityEngine.Random.insideUnitCircle*8; yield return new WaitForSeconds(.05f);}
            visual.anchoredPosition=Vector2.zero; body.color=original;
        }
        public IEnumerator Die()
        {
            for(float t=0;t<1;t+=Time.deltaTime/.6f){ visual.localScale=Vector3.one*(1-t); visual.localEulerAngles=new Vector3(0,0,t*90); yield return null;}
        }
    }

    public sealed class EnemyController : MonoBehaviour
    {
        EnemyDefinition definition; Image body; Image selection; Text hpText; Text intentText; RectTransform visual;
        public int CurrentHp { get; private set; }
        public int MaximumHp { get; private set; }
        public int AttackDamage { get; private set; }
        public int CurrentDistance { get; private set; }
        public int MovementAmount => definition.MovementAmount;
        public int Countdown { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsArcher => definition.Behavior == EnemyBehaviorType.Archer;
        public bool IsTargetable => !IsDead;
        public EnemyDefinition Definition => definition;
        public RectTransform Visual => visual;
        public event Action<EnemyController> Selected;

        public void Initialize(EnemySpawnEntry entry)
        {
            definition = entry.definition;
            MaximumHp = entry.hpOverride > 0 ? entry.hpOverride : definition.MaximumHp; CurrentHp = MaximumHp;
            AttackDamage = entry.damageOverride > 0 ? entry.damageOverride : definition.AttackDamage;
            CurrentDistance = entry.distanceOverride > 0 ? entry.distanceOverride : definition.StartingDistance;
            Countdown = definition.AttackCountdown;
            BuildView();
        }
        void BuildView()
        {
            body = GetComponent<Image>() ?? gameObject.AddComponent<Image>(); var button = gameObject.AddComponent<Button>(); button.targetGraphic=body; button.onClick.AddListener(()=>Selected?.Invoke(this));
            body.color=definition.VisualColor; visual=(RectTransform)transform; visual.localScale=Vector3.one*definition.VisualScale;
            selection=RuntimeUI.Panel(transform,"Selection",new Color(.2f,1f,.8f,.35f),Vector2.zero,Vector2.one,new Vector2(-10,-10),new Vector2(10,10)).GetComponent<Image>();
            selection.transform.SetAsFirstSibling(); selection.enabled=false;
            RuntimeUI.Label(transform,"Name",definition.DisplayName,22,TextAnchor.UpperCenter,Color.white);
            hpText=RuntimeUI.Label(transform,"HP","",20,TextAnchor.LowerCenter,Color.white);
            intentText=RuntimeUI.Label(transform,"Intent","",20,TextAnchor.MiddleCenter,Color.yellow);
            Refresh();
        }
        public void SetSelected(bool value) { if(selection) selection.enabled=value; }
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
            Vector2 start=visual.anchoredPosition;
            for(float t=0;t<1;t+=Time.deltaTime/.28f){visual.anchoredPosition=Vector2.Lerp(start,start+Vector2.down*45,Mathf.Sin(t*Mathf.PI));yield return null;} visual.anchoredPosition=start;
        }
        public IEnumerator Hit()
        {
            Color original=body.color;
            for(int i=0;i<4;i++){body.color=i%2==0?Color.white:original;yield return new WaitForSeconds(.05f);} body.color=original;
        }
        public IEnumerator Die()
        {
            MarkDead();
            for(float t=0;t<1;t+=Time.deltaTime/.45f){visual.localScale=Vector3.one*definition.VisualScale*(1-t);body.color=new Color(body.color.r,body.color.g,body.color.b,1-t);yield return null;}
            Destroy(gameObject);
        }
        void Refresh()
        {
            if(hpText) hpText.text=$"{CurrentHp}/{MaximumHp} HP";
            if(intentText) intentText.text=IsArcher?$"RANGED {Countdown}":$"ATTACK {AttackDamage}";
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
