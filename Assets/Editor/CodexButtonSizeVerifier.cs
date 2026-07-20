using System;
using System.Reflection;
using MemorialArchive.Framework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CodexButtonSizeVerifier
{
    private const string PrefabPath = "Assets/Prefabs/UI/NewGameConfirmPanel.prefab";

    public static void Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Could not load {PrefabPath}.");
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            VerifyButton(instance.transform.Find("ConfirmButton"), new Vector2(82f, 55f), new Vector2(162f, 137f));
            VerifyButton(instance.transform.Find("CancelButton"), new Vector2(104f, 47f), new Vector2(218f, 137f));
            Debug.Log("CODEX_BUTTON_SIZE_VERIFICATION: passed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void VerifyButton(Transform buttonTransform, Vector2 normalSize, Vector2 selectedSize)
    {
        if (buttonTransform == null)
        {
            throw new InvalidOperationException("Button transform is missing.");
        }

        Image image = buttonTransform.GetComponent<Image>();
        Button button = buttonTransform.GetComponent<Button>();
        SpriteSwapNativeSize nativeSize = buttonTransform.GetComponent<SpriteSwapNativeSize>();
        RectTransform rectTransform = (RectTransform)buttonTransform;
        if (image == null || button == null || nativeSize == null)
        {
            throw new InvalidOperationException($"{buttonTransform.name} is missing a required component.");
        }

        MethodInfo refresh = typeof(SpriteSwapNativeSize).GetMethod("RefreshSize", BindingFlags.Instance | BindingFlags.NonPublic);
        if (refresh == null)
        {
            throw new InvalidOperationException("Could not find SpriteSwapNativeSize.RefreshSize.");
        }

        image.overrideSprite = button.spriteState.selectedSprite;
        refresh.Invoke(nativeSize, new object[] { true });
        AssertSize(buttonTransform.name, "selected", rectTransform.sizeDelta, selectedSize);

        image.overrideSprite = null;
        refresh.Invoke(nativeSize, new object[] { true });
        AssertSize(buttonTransform.name, "normal", rectTransform.sizeDelta, normalSize);
    }

    private static void AssertSize(string buttonName, string state, Vector2 actual, Vector2 expected)
    {
        if ((actual - expected).sqrMagnitude > 0.01f)
        {
            throw new InvalidOperationException(
                $"{buttonName} {state} size is {actual}, expected {expected}.");
        }
    }
}
