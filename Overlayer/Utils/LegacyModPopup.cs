using DG.Tweening;
using Overlayer.Core;
using RapidGUI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Overlayer.Utils;

// Warns about a leftover pre-3.50.0 install (Mod Id "Overlayer", before the rename to
// "Overlayer-qwru") still sitting in the Mods folder. The updater only ever copies files into
// THIS mod's own folder, so renaming the Id left that old folder behind forever - and having
// both loaded at once means two mods patching the same scrController/scrPlanet/etc. methods.
// No "don't show again" flag: this re-checks every launch and simply stops firing once the
// user actually deletes the old folder, which is the only thing that actually fixes it.
internal class LegacyModPopup : MonoBehaviour {
    private Rect windowRect;
    private List<string> contentLines;
    private bool isInitaialize = false;
    private bool isAnimating = false;
    private bool isSpawn = false;

    public void Initialize(string legacyPath) {
        contentLines = [
            Main.Lang.Get("LEGACY_MOD_WARNING_LINE1", "An old copy of this mod was found and is no longer updated by this fork:"),
            legacyPath,
            Main.Lang.Get("LEGACY_MOD_WARNING_LINE2", "Please delete that folder - keeping both installed can cause conflicts.")
        ];

        var maxWidth = 0f;
        foreach(var line in contentLines) {
            float lineWidth = GUI.skin.label.CalcSize(new GUIContent(line)).x;
            if(lineWidth > maxWidth) {
                maxWidth = lineWidth;
            }
        }
        float width = maxWidth + 40;
        float height = (contentLines.Count * 20) + 40;
        windowRect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        isInitaialize = true;
    }

    private void OnGUI() {
        if(isInitaialize) {
            if(!isSpawn && Event.current.type == EventType.Repaint) {
                windowRect = GUILayout.Window(121, windowRect, DrawWindow, $"Overlayer - {Main.Lang.Get("LEGACY_MOD_WARNING_TITLE", "Old Install Detected")}", RGUIStyle.darkWindow);
                windowRect.x = (int)((Screen.width * 0.5f) - (windowRect.width * 0.5f));
                windowRect.y = (int)((Screen.height * 0.5f) - (windowRect.height * 0.5f));
                isSpawn = true;
            }
            windowRect = GUILayout.Window(121, windowRect, DrawWindow, $"Overlayer - {Main.Lang.Get("LEGACY_MOD_WARNING_TITLE", "Old Install Detected")}", RGUIStyle.darkWindow);
        }
    }

    private void DrawWindow(int windowID) {
        GUI.BringWindowToFront(windowID);
        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.FlexibleSpace();

        foreach(var line in contentLines) {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(line, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if(Drawer.Button($"<size=18>{Main.Lang.Get("OK", "OK!")}</size>", GUILayout.Width(100), GUILayout.Height(40))) {
            AnimateAndDestroy();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }

    private IEnumerator DestroyCoroutine() {
        yield return new WaitForSecondsRealtime(0.5f);
        Destroy(gameObject);
    }

    private void AnimateAndDestroy() {
        if(isAnimating) {
            return;
        } else {
            isAnimating = true;
        }

        StartCoroutine(DestroyCoroutine());
        DOTween.To(() => windowRect.position, x => windowRect.position = x,
                new Vector2(windowRect.position.x, Screen.height * -1.3f), 0.4f)
            .SetEase(Ease.InBack)
            .SetUpdate(true);
    }
}
