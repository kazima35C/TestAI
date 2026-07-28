using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DiceBattle.Combat;
using DiceBattle.Data;
using DiceBattle.Dice;
using DiceBattle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace DiceBattle.Core
{
    /// <summary>Coordinates waves and turns while delegating dice, actors, targeting and presentation.</summary>
    public sealed class DiceBattleGameController : MonoBehaviour
    {
        [Header("Generated data")]
        [SerializeField] GameBalanceConfig balance;
        [SerializeField] List<WaveDefinition> waves = new();
        [Header("Testing")]
        [SerializeField] bool useDeterministicSeed;
        [SerializeField] int deterministicSeed = 12345;

        readonly BattleStateMachine state = new();
        readonly EnemyTargetingController targeting = new();
        readonly GameStatisticsTracker statistics = new();
        readonly List<EnemyController> enemies = new();
        [SerializeField] List<DiceView> dice = new();
        IDiceRandom random;
        [Header("Generated scene UI")]
        [SerializeField] PlayerCombatController hero;
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform battlefield, enemyRoot, heroRoot;
        [SerializeField] Text waveText, enemyCountText, playerHpText, previewText, rerollText, primaryText, waveBanner, resultTitleText, resultDetailsText;
        [SerializeField] Button primaryButton, pauseButton, resumeButton, pauseRestartButton, resultRestartButton;
        [SerializeField] GameObject pauseOverlay, resultOverlay, bannerPanel;
        [SerializeField] ScreenShakeController shake;
        [SerializeField] EnemyController enemyPrefab;
        int waveIndex = -1, rerolls;
        bool hasInitialRoll;
        bool skipNextPrimaryReadyAnimation;
        DiceCombinationResult result;
        BattleState stateBeforePause;

        void Awake()
        {
            if (balance == null || waves == null || waves.Count != 10)
            {
                Debug.LogError(
                    $"Dice Battle data is invalid. Balance assigned: {balance != null}, " +
                    $"wave count: {(waves == null ? 0 : waves.Count)}. Expected exactly 10 waves.",
                    this);
                enabled = false;
                return;
            }

            if (canvas == null || hero == null || hero.Health == null || dice.Count != 5 || enemyPrefab == null)
            {
                Debug.LogError(
                    $"Dice Battle scene references are invalid. Canvas: {canvas != null}, " +
                    $"Hero: {hero != null}, Hero Health: {hero != null && hero.Health != null}, " +
                    $"dice count: {dice.Count}, Enemy Prefab: {enemyPrefab != null}.",
                    this);
                enabled = false;
                return;
            }
            random = new DiceRandom(useDeterministicSeed, deterministicSeed);
            primaryButton.onClick.AddListener(OnPrimary);pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);pauseRestartButton.onClick.AddListener(Restart);resultRestartButton.onClick.AddListener(Restart);
            foreach(var die in dice)die.Clicked+=OnDieClicked;
            hero.Health.Initialize(balance.playerMaximumHp);hero.Health.Changed+=OnHeroHealthChanged;OnHeroHealthChanged(hero.Health.Current,hero.Health.Maximum);
            state.Changed += _ => RefreshControls();
        }
        IEnumerator Start()
        {
            state.Set(BattleState.Initializing);
            yield return null;
            StartCoroutine(StartNextWave());
        }

        void BuildInterface()
        {
            RuntimeUI.EnsureEventSystem();
            var canvasGo=new GameObject("DiceBattleCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false);
            canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=1;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var safe=RuntimeUI.Panel(canvas.transform,"SafeArea",new Color(.035f,.055f,.10f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).transform;
            var top=RuntimeUI.Panel(safe,"TopHUD",new Color(.06f,.1f,.18f,.98f),new Vector2(0, .91f),Vector2.one,Vector2.zero,Vector2.zero).transform;
            waveText=CreateAnchoredLabel(top,"Wave","Wave 1/10",32,new Vector2(0,.5f),new Vector2(.27f,1));
            enemyCountText=CreateAnchoredLabel(top,"Enemies","Enemies: 0",26,new Vector2(.27f,.5f),new Vector2(.55f,1));
            playerHpText=CreateAnchoredLabel(top,"HP","HP 120/120",39,new Vector2(.55f,.5f),new Vector2(.82f,1));
            var pauseBox=RuntimeUI.Panel(top,"PauseBox",Color.clear,new Vector2(.84f,.12f),new Vector2(.98f,.88f),Vector2.zero,Vector2.zero);
            pauseButton=RuntimeUI.Button(pauseBox.transform,"Pause","II",new Color(.2f,.3f,.48f),null);

            battlefield=(RectTransform)RuntimeUI.Panel(safe,"Battlefield",new Color(.07f,.12f,.2f),new Vector2(.025f,.39f),new Vector2(.975f,.91f),Vector2.zero,Vector2.zero).transform;
            shake=battlefield.gameObject.AddComponent<ScreenShakeController>();
            enemyRoot=(RectTransform)RuntimeUI.Panel(battlefield,"Enemies",Color.clear,new Vector2(.05f,.43f),new Vector2(.95f,.98f),Vector2.zero,Vector2.zero).transform;
            heroRoot=(RectTransform)RuntimeUI.Panel(battlefield,"Hero",Color.clear,new Vector2(.34f,.02f),new Vector2(.66f,.42f),Vector2.zero,Vector2.zero).transform;
            hero=heroRoot.gameObject.AddComponent<PlayerCombatController>();hero.Build(balance.playerMaximumHp);

            var preview=RuntimeUI.Panel(safe,"DamagePreview",new Color(.08f,.16f,.25f),new Vector2(.04f,.285f),new Vector2(.96f,.385f),Vector2.zero,Vector2.zero).transform;
            previewText=RuntimeUI.Label(preview,"Preview","ROLL THE DICE",42,TextAnchor.MiddleCenter,Color.white);

            var diceRow=RuntimeUI.Panel(safe,"DiceRow",Color.clear,new Vector2(.025f,.17f),new Vector2(.975f,.28f),Vector2.zero,Vector2.zero).transform;
            for(int i=0;i<5;i++)
            {
                var holder=RuntimeUI.Panel(diceRow,$"Die{i+1}",Color.white,new Vector2(i/5f+.012f,.05f),new Vector2((i+1)/5f-.012f,.95f),Vector2.zero,Vector2.zero);
                var view=holder.AddComponent<DiceView>();view.Build();dice.Add(view);
            }
            var rerollBox=RuntimeUI.Panel(safe,"RerollInfo",new Color(.12f,.18f,.28f),new Vector2(.04f,.075f),new Vector2(.43f,.155f),Vector2.zero,Vector2.zero).transform;
            rerollText=RuntimeUI.Label(rerollBox,"RerollCount","3 REROLLS LEFT",30,TextAnchor.MiddleCenter,Color.white);
            var primaryBox=RuntimeUI.Panel(safe,"PrimaryBox",Color.clear,new Vector2(.47f,.055f),new Vector2(.96f,.16f),Vector2.zero,Vector2.zero).transform;
            primaryButton=RuntimeUI.Button(primaryBox,"Primary","ROLL",new Color(.12f,.64f,.45f),null);
            primaryText=primaryButton.GetComponentInChildren<Text>();
            BuildOverlays(safe);
        }

        Text CreateAnchoredLabel(Transform parent,string name,string value,int size,Vector2 min,Vector2 max)
        {
            var box=RuntimeUI.Panel(parent,name+"Box",Color.clear,min,max,Vector2.zero,Vector2.zero);
            return RuntimeUI.Label(box.transform,name,value,size,TextAnchor.MiddleCenter,Color.white);
        }

        void BuildOverlays(Transform safe)
        {
            bannerPanel=RuntimeUI.Panel(safe,"Banner",new Color(.05f,.75f,.68f,.95f),new Vector2(0,.55f),new Vector2(1,.68f),Vector2.zero,Vector2.zero);bannerPanel.SetActive(false);
            waveBanner=RuntimeUI.Label(bannerPanel.transform,"BannerText","WAVE 1",64,TextAnchor.MiddleCenter,Color.white);
            pauseOverlay=RuntimeUI.Panel(safe,"PauseOverlay",new Color(0,0,0,.88f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);pauseOverlay.SetActive(false);
            CreateAnchoredLabel(pauseOverlay.transform,"Title","PAUSED",72,new Vector2(.15f,.62f),new Vector2(.85f,.78f));
            var resume=RuntimeUI.Panel(pauseOverlay.transform,"ResumeBox",Color.clear,new Vector2(.2f,.47f),new Vector2(.8f,.57f),Vector2.zero,Vector2.zero);
            resumeButton=RuntimeUI.Button(resume.transform,"Resume","RESUME",new Color(.12f,.64f,.45f),null);
            var restart=RuntimeUI.Panel(pauseOverlay.transform,"RestartBox",Color.clear,new Vector2(.2f,.34f),new Vector2(.8f,.44f),Vector2.zero,Vector2.zero);
            pauseRestartButton=RuntimeUI.Button(restart.transform,"Restart","RESTART LEVEL",new Color(.65f,.25f,.25f),null);
            resultOverlay=RuntimeUI.Panel(safe,"ResultOverlay",new Color(.02f,.04f,.09f,.97f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);resultOverlay.SetActive(false);
            resultTitleText=CreateAnchoredLabel(resultOverlay.transform,"ResultTitle","LEVEL COMPLETE",72,new Vector2(.1f,.62f),new Vector2(.9f,.78f));
            resultDetailsText=CreateAnchoredLabel(resultOverlay.transform,"Details","",38,new Vector2(.12f,.38f),new Vector2(.88f,.62f));
            var resultBox=RuntimeUI.Panel(resultOverlay.transform,"ResultRestart",Color.clear,new Vector2(.2f,.22f),new Vector2(.8f,.32f),Vector2.zero,Vector2.zero);
            resultRestartButton=RuntimeUI.Button(resultBox.transform,"Restart","RESTART",new Color(.12f,.64f,.45f),null);
        }

        IEnumerator StartNextWave()
        {
            waveIndex++;
            if(waveIndex>=10){Victory();yield break;}
            state.Set(BattleState.WaveIntro);ClearEnemies();
            var wave=waves.OrderBy(w=>w.WaveIndex).ElementAt(waveIndex);
            foreach(var entry in wave.Enemies){if(entry.spawnDelay>0)yield return new WaitForSeconds(entry.spawnDelay);SpawnEnemy(entry);}
            targeting.ValidateOrAuto(enemies);UpdateHud();
            waveBanner.text=$"WAVE {waveIndex+1}";bannerPanel.SetActive(true);yield return FadeBanner(balance.waveIntroDuration);bannerPanel.SetActive(false);
            BeginPlayerTurn();
        }

        void SpawnEnemy(EnemySpawnEntry entry)
        {
            var enemy=Instantiate(enemyPrefab,enemyRoot);var go=enemy.gameObject;go.name=$"{entry.definition.DisplayName}_{enemies.Count+1}";
            var rt=(RectTransform)go.transform;rt.anchorMin=new Vector2(entry.spawnLane/3f+.035f,.12f);rt.anchorMax=new Vector2((entry.spawnLane+1)/3f-.035f,.88f);rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            enemy.Initialize(entry);enemy.Selected+=e=>{if(state.Is(BattleState.PlayerDecision)||state.Is(BattleState.WaitingForInitialRoll))targeting.Select(e);};enemies.Add(enemy);
            go.transform.localScale=Vector3.zero;
            go.transform.DOScale(entry.definition.VisualScale,.28f).SetEase(Ease.OutBack).SetLink(go);
        }
        void BeginPlayerTurn()
        {
            hasInitialRoll=false;rerolls=balance.rerollsPerTurn;result=null;
            foreach(var die in dice)die.ResetDie();
            previewText.text="ROLL THE DICE";state.Set(BattleState.WaitingForInitialRoll);targeting.ValidateOrAuto(enemies);UpdateHud();
        }
        void OnPrimary()
        {
            if(state.Is(BattleState.WaitingForInitialRoll))StartCoroutine(RollDice(false));
            else if(state.Is(BattleState.PlayerDecision))StartCoroutine(PlayerAttack());
        }
        IEnumerator RollDice(bool consumes)
        {
            state.Set(BattleState.RollingDice);
            var rolling=dice.Where(d=>!d.Locked).ToList();
            if(!hasInitialRoll)rolling=dice.ToList();
            if(consumes&&rolling.Count>0)rerolls--;
            float elapsed=0;
            while(elapsed<balance.diceRollDuration){foreach(var die in rolling)die.SetValue(random.NextDie(),false);elapsed+=Time.unscaledDeltaTime;yield return new WaitForSecondsRealtime(.06f);}
            foreach(var die in rolling)die.SetValue(random.NextDie(),true);
            hasInitialRoll=true;result=DiceCombinationEvaluator.Evaluate(dice.Select(d=>d.Value).ToArray(),balance.heroAttackMultiplier);
            previewText.text=$"DAMAGE: {result.FinalDamage}\n{result.DisplayName}  x{result.Multiplier:0.#}     Dice Sum: {result.DiceSum}";
            state.Set(BattleState.PlayerDecision);
        }
        void OnDieClicked(DiceView die)
        {
            if(state.Is(BattleState.PlayerDecision)&&rerolls>0)StartCoroutine(RollSingleDie(die));
        }
        IEnumerator RollSingleDie(DiceView die)
        {
            skipNextPrimaryReadyAnimation=true;
            state.Set(BattleState.RollingDice);
            rerolls--;
            float elapsed=0;
            while(elapsed<balance.diceRollDuration)
            {
                die.SetValue(random.NextDie(),false);
                elapsed+=Time.unscaledDeltaTime;
                yield return new WaitForSecondsRealtime(.02f);
            }
            die.SetValue(random.NextDie(),true);
            result=DiceCombinationEvaluator.Evaluate(dice.Select(d=>d.Value).ToArray(),balance.heroAttackMultiplier);
            previewText.text=$"DAMAGE: {result.FinalDamage}\n{result.DisplayName}  x{result.Multiplier:0.#}     Dice Sum: {result.DiceSum}";
            state.Set(BattleState.PlayerDecision);
        }

        IEnumerator PlayerAttack()
        {
            var target=targeting.ValidateOrAuto(enemies);if(target==null||result==null)yield break;
            state.Set(BattleState.PlayerAttacking);statistics.Record(result);
            if(result.Type>=DiceCombinationType.FullHouse){waveBanner.text=result.DisplayName.ToUpperInvariant();bannerPanel.SetActive(true);}
            yield return hero.Attack(target.Visual);target.Damage(result.FinalDamage);SpawnDamage(target.transform,result.FinalDamage,result.Type>=DiceCombinationType.FourOfAKind?1.55f:1f);
            StartCoroutine(target.Hit());
            if(result.Type>=DiceCombinationType.FourOfAKind)yield return shake.Shake(result.Type==DiceCombinationType.FiveOfAKind?22:12,.28f);
            if(result.Type==DiceCombinationType.FiveOfAKind){Time.timeScale=.08f;yield return new WaitForSecondsRealtime(.11f);Time.timeScale=1f;}
            bannerPanel.SetActive(false);
            if(target.CurrentHp<=0)yield return target.Die();
            UpdateHud();
            if(enemies.All(e=>e==null||e.IsDead)){state.Set(BattleState.WaveComplete);yield return new WaitForSeconds(.8f);StartCoroutine(StartNextWave());yield break;}
            StartCoroutine(EnemyTurn());
        }
        IEnumerator EnemyTurn()
        {
            state.Set(BattleState.EnemyTurn);
            foreach(var enemy in enemies.ToArray())
            {
                if(enemy==null||enemy.IsDead)continue;
                bool attack=false;
                if(enemy.IsArcher){attack=enemy.ArcherWillAttack();enemy.AdvanceCountdown();}
                else attack=true;
                if(attack){yield return enemy.Attack(heroRoot);hero.Health.Damage(enemy.AttackDamage);SpawnDamage(heroRoot,enemy.AttackDamage,1f);yield return hero.Hit();if(hero.Health.IsDead){yield return hero.Die();Defeat();yield break;}}
                yield return new WaitForSeconds(balance.enemyActionGap);
            }
            BeginPlayerTurn();
        }
        void SpawnDamage(Transform parent,int damage,float scale)
        {
            var go=new GameObject("FloatingDamage",typeof(RectTransform),typeof(Text),typeof(FloatingDamageText));
            var rt=(RectTransform)go.transform;rt.sizeDelta=new Vector2(250,100);rt.anchoredPosition=Vector2.zero;
            var text=go.GetComponent<Text>();text.font=RuntimeUI.Font;text.alignment=TextAnchor.MiddleCenter;
            go.GetComponent<FloatingDamageText>().Show(parent,$"-{damage}",new Color(1f,.82f,.2f),scale);
        }
        IEnumerator FadeBanner(float duration)
        {
            var group=bannerPanel.GetComponent<CanvasGroup>();if(group==null)group=bannerPanel.AddComponent<CanvasGroup>();
            group.alpha=0;bannerPanel.transform.localScale=Vector3.one*.78f;
            var sequence=DOTween.Sequence().SetUpdate(true).SetLink(bannerPanel);
            sequence.Append(DOTween.To(()=>group.alpha,x=>group.alpha=x,1,.16f));
            sequence.Join(bannerPanel.transform.DOScale(1,.22f).SetEase(Ease.OutBack));
            sequence.AppendInterval(Mathf.Max(.15f,duration-.42f));
            sequence.Append(DOTween.To(()=>group.alpha,x=>group.alpha=x,0,.2f));
            yield return sequence.WaitForCompletion();group.alpha=1;
        }
        void OnHeroHealthChanged(int current,int max){if(playerHpText)playerHpText.text=$"HP {current}/{max}";}
        void UpdateHud(){waveText.text=$"Wave {waveIndex+1}/10";enemyCountText.text=$"Enemies: {enemies.Count(e=>e!=null&&!e.IsDead)}";}
        void RefreshControls()
        {
            bool decision=state.Is(BattleState.PlayerDecision), initial=state.Is(BattleState.WaitingForInitialRoll);
            foreach(var die in dice)die.SetInteractable(decision);
            primaryButton.interactable=decision||initial;primaryText.text=initial?"ROLL":decision?"ATTACK":"WAIT";
            primaryButton.transform.DOKill();
            primaryButton.transform.localScale=Vector3.one;
            if(skipNextPrimaryReadyAnimation&&decision)skipNextPrimaryReadyAnimation=false;
            else if(decision||initial)primaryButton.transform.DOPunchScale(Vector3.one*.08f,.22f,6,.5f).SetUpdate(true).SetLink(primaryButton.gameObject);
            rerollText.text=$"{rerolls} REROLLS LEFT\nTAP A DIE TO REROLL";
            pauseButton.interactable=!state.Is(BattleState.Victory)&&!state.Is(BattleState.Defeat);
        }
        void TogglePause()
        {
            if(state.Is(BattleState.Victory)||state.Is(BattleState.Defeat))return;
            if(state.Is(BattleState.Paused)){Time.timeScale=1f;pauseOverlay.SetActive(false);state.Resume();}
            else{Time.timeScale=0f;pauseOverlay.SetActive(true);state.Set(BattleState.Paused);}
        }
        void Victory(){state.Set(BattleState.Victory);ShowResult("LEVEL COMPLETE",$"Turns: {statistics.Turns}\nTotal Damage: {statistics.TotalDamage}\nHighest: {DisplayCombination(statistics.Highest)}");}
        void Defeat(){state.Set(BattleState.Defeat);ShowResult("DEFEATED",$"Wave Reached: {waveIndex+1}/10\nTurns: {statistics.Turns}");}
        string DisplayCombination(DiceCombinationType type)=>System.Text.RegularExpressions.Regex.Replace(type.ToString(),"([a-z])([A-Z])","$1 $2").Replace("Of A","of a");
        void ShowResult(string title,string details)
        {
            resultTitleText.text=title;resultDetailsText.text=details;resultOverlay.SetActive(true);
        }
        void Restart(){Time.timeScale=1f;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        void ClearEnemies(){foreach(var enemy in enemies)if(enemy)Destroy(enemy.gameObject);enemies.Clear();}

#if UNITY_EDITOR
        public void EditorConfigure(GameBalanceConfig config,List<WaveDefinition> definitions,EnemyController generatedEnemyPrefab){balance=config;waves=definitions;enemyPrefab=generatedEnemyPrefab;}
        public void EditorBuildInterface()
        {
            dice.Clear();
            BuildInterface();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
