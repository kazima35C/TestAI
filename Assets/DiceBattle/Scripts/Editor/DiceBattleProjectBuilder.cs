#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using DiceBattle.Core;
using DiceBattle.Combat;
using DiceBattle.Data;
using DiceBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DiceBattle.Editor
{
    /// <summary>Idempotently creates every asset and the fully-wired gameplay scene.</summary>
    public static class DiceBattleProjectBuilder
    {
        const string Root="Assets/DiceBattle";
        const string ScenePath=Root+"/Scenes/DiceBattleGameplay.unity";

        [MenuItem("Tools/Dice Battle/Build Complete Prototype")]
        public static void Build()
        {
            EnsureFolders();
            var balance=LoadOrCreate<GameBalanceConfig>(Root+"/Data/GameBalance.asset");
            balance.playerMaximumHp=120;balance.heroAttackMultiplier=1f;balance.rerollsPerTurn=3;balance.diceRollDuration=.1f;balance.enemyActionGap=.3f;balance.waveIntroDuration=1.1f;
            EditorUtility.SetDirty(balance);
            var grunt=Enemy("Grunt","Grunt",EnemyBehaviorType.Grunt,35,5,3,1,2,new Color(.78f,.18f,.2f),1f);
            var runner=Enemy("Runner","Runner",EnemyBehaviorType.Runner,30,4,4,2,2,new Color(1f,.42f,.08f),.82f);
            var tank=Enemy("Tank","Tank",EnemyBehaviorType.Tank,85,9,4,1,2,new Color(.25f,.29f,.38f),1.18f);
            var archer=Enemy("Archer","Archer",EnemyBehaviorType.Archer,40,7,4,0,2,new Color(.52f,.22f,.72f),.92f);
            var waves=new List<WaveDefinition>
            {
                Wave(1,E(grunt,35,5,3,1)),
                Wave(2,E(grunt,40,5,3,0),E(grunt,40,5,4,2)),
                Wave(3,E(runner,30,4,4,0),E(grunt,45,6,4,2)),
                Wave(4,E(runner,35,5,4,0),E(grunt,50,6,4,1),E(runner,35,5,4,2)),
                Wave(5,E(tank,85,9,4,0),E(grunt,55,7,3,2)),
                Wave(6,E(archer,40,7,4,0),E(runner,40,6,4,1),E(archer,40,7,4,2)),
                Wave(7,E(grunt,55,7,3,0),E(tank,95,10,4,1),E(grunt,55,7,4,2)),
                Wave(8,E(tank,90,10,4,0),E(runner,45,6,4,1),E(tank,90,10,4,2)),
                Wave(9,E(archer,55,8,4,0),E(grunt,60,8,3,0),E(grunt,60,8,4,2),E(archer,55,8,4,2)),
                Wave(10,E(runner,55,7,4,0),E(tank,115,12,4,1),E(archer,65,9,4,2),E(runner,55,7,4,2))
            };
            CreatePlaceholderTexture();
            CreatePrefabs();
            var combatEnemyPrefab=CreateCombatEnemyPrefab();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.03f,.05f,.09f);camera.transform.position=new Vector3(0,0,-10);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var root=new GameObject("DiceBattleGame");var controller=root.AddComponent<DiceBattleGameController>();controller.EditorConfigure(balance,waves,combatEnemyPrefab);controller.EditorBuildInterface();EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,Root+"/Prefabs/CombatUI.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var buildScenes=new List<EditorBuildSettingsScene>();
            foreach(var existing in EditorBuildSettings.scenes)if(existing.path!=ScenePath)buildScenes.Add(existing);
            buildScenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=buildScenes.ToArray();
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Dice Battle prototype built successfully: "+ScenePath);
        }

        static EnemySpawnEntry E(EnemyDefinition d,int hp,int damage,int distance,int lane)=>new(){definition=d,hpOverride=hp,damageOverride=damage,distanceOverride=distance,spawnLane=lane,spawnDelay=0};
        static WaveDefinition Wave(int index,params EnemySpawnEntry[] entries)
        {
            var path=$"{Root}/Data/Wave_{index:00}.asset";var wave=LoadOrCreate<WaveDefinition>(path);wave.Configure(index,new List<EnemySpawnEntry>(entries));EditorUtility.SetDirty(wave);return wave;
        }
        static EnemyDefinition Enemy(string id,string display,EnemyBehaviorType type,int hp,int damage,int distance,int movement,int countdown,Color color,float scale)
        {
            var asset=LoadOrCreate<EnemyDefinition>($"{Root}/Data/Enemy_{id}.asset");asset.Configure(id,display,type,hp,damage,distance,movement,countdown,color,scale);EditorUtility.SetDirty(asset);return asset;
        }
        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset)return asset;asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static void EnsureFolders()
        {
            string[] folders={"Scenes","Scripts","Scripts/Core","Scripts/Combat","Scripts/Dice","Scripts/Enemies","Scripts/UI","Scripts/Data","Scripts/Editor","Prefabs","Data","Art","Materials","Tests/Editor"};
            if(!AssetDatabase.IsValidFolder(Root))AssetDatabase.CreateFolder("Assets","DiceBattle");
            foreach(var relative in folders){string current=Root;foreach(var part in relative.Split('/')){string next=current+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,part);current=next;}}
        }
        static void CreatePlaceholderTexture()
        {
            string path=Root+"/Art/PrototypePalette.png";if(File.Exists(path))return;
            var texture=new Texture2D(4,1,TextureFormat.RGBA32,false);texture.SetPixels(new[]{new Color(.12f,.48f,.85f),new Color(.78f,.18f,.2f),new Color(1f,.42f,.08f),new Color(.52f,.22f,.72f)});texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        }
        static void CreatePrefabs()
        {
            Prefab("Hero",new Color(.12f,.48f,.85f),"◆");
            Prefab("Enemy_Grunt",new Color(.78f,.18f,.2f),"G");Prefab("Enemy_Runner",new Color(1f,.42f,.08f),"R");
            Prefab("Enemy_Tank",new Color(.25f,.29f,.38f),"T");Prefab("Enemy_Archer",new Color(.52f,.22f,.72f),"A");
            Prefab("DiceUI",Color.white,"●");Prefab("FloatingDamageText",Color.clear,"-10");
        }
        static EnemyController CreateCombatEnemyPrefab()
        {
            string path=Root+"/Prefabs/EnemyCombatant.prefab";
            var go=new GameObject("EnemyCombatant",typeof(RectTransform),typeof(Image));
            ((RectTransform)go.transform).sizeDelta=new Vector2(220,210);
            var controller=go.AddComponent<EnemyController>();controller.EditorBuildView();
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<EnemyController>();
        }
        static void Prefab(string name,Color color,string glyph)
        {
            string path=$"{Root}/Prefabs/{name}.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.GetComponent<Image>().color=color;((RectTransform)go.transform).sizeDelta=new Vector2(150,150);
            RuntimeUI.Label(go.transform,"Label",glyph,48,TextAnchor.MiddleCenter,color==Color.white?Color.black:Color.white);
            if(existing)PrefabUtility.SaveAsPrefabAssetAndConnect(go,path,InteractionMode.AutomatedAction);else PrefabUtility.SaveAsPrefabAsset(go,path);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
