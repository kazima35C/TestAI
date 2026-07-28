using System;
using System.Collections;
using DiceBattle.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DiceBattle.Dice
{
    public sealed class DiceView : MonoBehaviour
    {
        static readonly string[][] Faces =
        {
            null, new[]{"●"}, new[]{"●   ●"}, new[]{"●  ●  ●"}, new[]{"● ●\n● ●"},
            new[]{"● ●\n ● \n● ●"}, new[]{"● ●\n● ●\n● ●"}
        };
        [SerializeField] Image background;
        [SerializeField] Text face;
        [SerializeField] Text lockedText;
        [SerializeField] Button button;
        [SerializeField] Vector2 basePosition;
        public int Value { get; private set; } = 1;
        public bool Locked { get; private set; }
        public bool HasRolled { get; private set; }
        public event Action<DiceView> Clicked;

        public void Build()
        {
            background = GetComponent<Image>() ?? gameObject.AddComponent<Image>(); background.color = new Color(.95f, .96f, 1f);
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>(); button.targetGraphic = background;
            face = RuntimeUI.Label(transform, "Face", "?", 48, TextAnchor.MiddleCenter, new Color(.08f, .12f, .2f));
            lockedText = RuntimeUI.Label(transform, "Lock", "", 18, TextAnchor.LowerCenter, new Color(.1f, .9f, .85f));
            basePosition = ((RectTransform)transform).anchoredPosition; SetInteractable(false);
        }
        void Awake()
        {
            if(!background)background=GetComponent<Image>();
            if(!button)button=GetComponent<Button>();
            if(!face)face=transform.Find("Face")?.GetComponent<Text>();
            if(!lockedText)lockedText=transform.Find("Lock")?.GetComponent<Text>();
            if(button)button.onClick.AddListener(HandleClick);
        }
        void HandleClick()=>Clicked?.Invoke(this);
        public void SetValue(int value, bool final)
        {
            Value = value; HasRolled |= final; face.text = Faces[value][0];
            if (!final) return;
            transform.DOKill();
            transform.localScale = Vector3.one * .82f;
            transform.DOScale(1f, .14f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
            transform.DOPunchRotation(new Vector3(0, 0, 12f), .14f, 5, .5f).SetUpdate(true).SetLink(gameObject);
        }
        public void ToggleLock() { if (!button.interactable || !HasRolled) return; Locked = !Locked; Refresh(); }
        public void ResetDie() { Locked = false; HasRolled = false; face.text = "?"; Refresh(); SetInteractable(false); }
        public void SetInteractable(bool value) { if (button) button.interactable = value; }
        public void SetLocked(bool value) { Locked = value; Refresh(); }
        void Refresh()
        {
            if (!background) return;
            background.color = Locked ? new Color(.12f, .55f, .62f) : new Color(.95f, .96f, 1f);
            face.color = Locked ? Color.white : new Color(.08f, .12f, .2f);
            lockedText.text = Locked ? "LOCKED" : "";
            ((RectTransform)transform).anchoredPosition = basePosition + (Locked ? Vector2.up * 16 : Vector2.zero);
        }
        void OnDestroy(){transform.DOKill();if(button)button.onClick.RemoveListener(HandleClick);}
    }
}
