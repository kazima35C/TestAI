using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using DG.Tweening;
using UnityEngine.UI;

namespace DiceBattle.UI
{
    /// <summary>Shared runtime UI construction helpers used by the generated prototype scene.</summary>
    public static class RuntimeUI
    {
        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static GameObject Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static Text Label(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = Font; text.text = value; text.fontSize = size; text.alignment = alignment; text.color = color;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -4);
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Color color, Action clicked)
        {
            var go = Panel(parent, name, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(() => clicked?.Invoke());
            Label(go.transform, "Label", label, 42, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }

    public sealed class FloatingDamageText : MonoBehaviour
    {
        public void Show(Transform parent, string value, Color color, float scale = 1f)
        {
            transform.SetParent(parent, false);
            var text = GetComponent<Text>(); text.text = value; text.color = color; text.fontSize = Mathf.RoundToInt(46 * scale);
            StartCoroutine(Animate());
        }
        IEnumerator Animate()
        {
            var rt = (RectTransform)transform; var start = rt.anchoredPosition; var text=GetComponent<Text>();
            transform.localScale=Vector3.one*.5f;
            var sequence=DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Join(DOTween.To(()=>rt.anchoredPosition,x=>rt.anchoredPosition=x,start+Vector2.up*110,.7f).SetEase(Ease.OutCubic));
            sequence.Join(transform.DOScale(1.35f,.2f).SetEase(Ease.OutBack));
            sequence.Append(DOTween.To(()=>text.color,x=>text.color=x,new Color(text.color.r,text.color.g,text.color.b,0),.2f));
            yield return sequence.WaitForCompletion();
            Destroy(gameObject);
        }
    }

    public sealed class ScreenShakeController : MonoBehaviour
    {
        public IEnumerator Shake(float strength, float duration)
        {
            var rt = (RectTransform)transform; var original = rt.anchoredPosition;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime) { rt.anchoredPosition = original + UnityEngine.Random.insideUnitCircle * strength; yield return null; }
            rt.anchoredPosition = original;
        }
    }
}
