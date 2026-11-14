using DG.Tweening;
using Overlayer.Core;
using RapidGUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overlayer.Utils;

// Warns about a leftover pre-3.50.0 install (Mod Id "Overlayer", before the rename to
// "Overlayer-qwru") still sitting in the Mods folder. The updater only ever copies files into
// THIS mod's own folder, so renaming the Id left that old folder behind forever - and having
// both loaded at once means two mods patching the same scrController/scrPlanet/etc. methods.
// No "don't show again" flag: this re-checks every launch and simply stops firing once the
// user actually deletes the old folder, which is the only thing that actually fixes it. Shown at
// most once per game session (see Main.OnShowGUI), and only after the update popup is closed.
internal class LegacyModPopup : MonoBehaviour {
    private Rect windowRect;
    private List<string> contentLines;
    private bool isInitaialize = false;
    private bool isAnimating = false;
    private bool isSpawn = false;

    public void Initialize(string legacyPath, LegacyModMigration.Result migration) {
        contentLines = migration == LegacyModMigration.Result.Copied
            ? [
                Main.Lang.Get("LEGACY_MOD_COPIED_LINE1", "An old copy of this mod was found. Its settings, profiles and scripts were copied here from:"),
                legacyPath,
                Main.Lang.Get("LEGACY_MOD_COPIED_LINE2", "Check that everything is there, then delete that folder - keeping both installed can cause conflicts.")
            ]
            : [
                Main.Lang.Get("LEGACY_MOD_WARNING_LINE1", "An old copy of this mod was found and is no longer updated by this fork:"),
                legacyPath,
                Main.Lang.Get("LEGACY_MOD_MOVE_FIRST", "Move anything you still need from it (profiles, Scripts, References) into this mod's folder first,"),
                Main.Lang.Get("LEGACY_MOD_WARNING_LINE2", "then delete that folder - keeping both installed can cause conflicts.")
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
        // Wait for the update popup: both are centered and both pull themselves to the front
        // every frame, so showing them together leaves one hidden under the other.
        if(isInitaialize && !Main.IsUpdatePopupOpen) {
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

// Copies the user's data out of a pre-rename install so the popup above can safely say "delete
// it". Everything Overlayer keeps per user lives inside its own mod folder (Settings.json,
// profiles/, Scripts/, References/), and the rename gave the mod a new folder - so without this,
// following the popup's advice would throw all of it away.
internal static class LegacyModMigration {
    public enum Result {
        // Nothing was copied because this install already had its own settings - it's been used
        // before, so the user's data here is newer than the old folder's and must not be mixed
        // with it (and anything deleted here would come back on every launch otherwise).
        Skipped,
        Copied,
        Failed,
    }

    private static readonly string[] Files = ["Settings.json", "Settings.xml"];
    private static readonly string[] Directories = ["profiles", "Scripts", "References"];

    public static Result Run(string legacyPath, string modPath) {
        try {
            if(Path.GetFullPath(legacyPath).TrimEnd(Path.DirectorySeparatorChar) == Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar)) {
                return Result.Skipped;
            }
            if(File.Exists(Path.Combine(modPath, "Settings.json")) || File.Exists(Path.Combine(modPath, "Settings.xml"))) {
                return Result.Skipped;
            }
            bool any = false;
            foreach(var file in Files) {
                var src = Path.Combine(legacyPath, file);
                if(File.Exists(src)) {
                    File.Copy(src, Path.Combine(modPath, file), false);
                    any = true;
                }
            }
            foreach(var dir in Directories) {
                var src = Path.Combine(legacyPath, dir);
                if(Directory.Exists(src)) {
                    CopyMissing(src, Path.Combine(modPath, dir));
                    any = true;
                }
            }
            return any ? Result.Copied : Result.Skipped;
        } catch(Exception e) {
            Main.Logger.Error($"[LegacyModMigration] copying from '{legacyPath}' failed: {e.GetType().Name} - {e.Message}");
            return Result.Failed;
        }
    }

    // Never overwrites: a file already in the new folder (e.g. a default shipped with the mod)
    // wins over the old copy.
    private static void CopyMissing(string src, string dst) {
        Directory.CreateDirectory(dst);
        foreach(var file in Directory.GetFiles(src)) {
            var target = Path.Combine(dst, Path.GetFileName(file));
            if(!File.Exists(target)) {
                File.Copy(file, target, false);
            }
        }
        foreach(var dir in Directory.GetDirectories(src)) {
            CopyMissing(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }
    }
}
