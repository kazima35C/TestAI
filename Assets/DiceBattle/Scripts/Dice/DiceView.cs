using System;
using System.Collections;
using DiceBattle.UI;
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
        Image background; Text face; Text lockedText; Button button; Vector2 basePosition;
        public int Value { get; private set; } = 1;
        public bool Locked { get; private set; }
        public bool HasRolled { get; private set; }
        public event Action<DiceView> Clicked;

        public void Build()
        {
            background = GetComponent<Image>() ?? gameObject.AddComponent<Image>(); background.color = new Color(.95f, .96f, 1f);
            button = gameObject.AddComponent<Button>(); button.targetGraphic = background; button.onClick.AddListener(() => Clicked?.Invoke(this));
            face = RuntimeUI.Label(transform, "Face", "?", 48, TextAnchor.MiddleCenter, new Color(.08f, .12f, .2f));
            lockedText = RuntimeUI.Label(transform, "Lock", "", 18, TextAnchor.LowerCenter, new Color(.1f, .9f, .85f));
            basePosition = ((RectTransform)transform).anchoredPosition; SetInteractable(false);
        }
        public void SetValue(int value, bool final) { Value = value; HasRolled |= final; face.text = Faces[value][0]; }
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
    }
}
