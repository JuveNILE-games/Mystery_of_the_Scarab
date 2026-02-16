using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public class TestDoTweenUIToolkit : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    void OnEnable()
    {
        StartCoroutine(AnimateAfterFrame());
    }
    
    IEnumerator AnimateAfterFrame()
    {
        // Wait one frame to ensure UI is fully initialized
        yield return null;
        
        var root = uiDocument.rootVisualElement;
        var box = root.Q<VisualElement>("TestElement");
        
        if (box == null)
        {
            Debug.LogError("TestElement not found! Check the name in UI Builder");
            yield break;
        }
        
        Debug.Log("Element found, starting animation");
        
        var tween = DOVirtual.Float(1f, 0f, 0.5f, value => box.style.opacity = value)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => Debug.Log("Fade complete!"));
        tween.Play();
    }
}