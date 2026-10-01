using System;
using System.Collections.Generic;

namespace Overlayer.Core;

// Unity's GUILayout requires an OnGUI call to emit the exact same sequence of controls across
// every event type in one GUI cycle (Layout runs first and computes the layout; Repaint and the
// rest replay against it). A value read directly from a field another thread can mutate (e.g. an
// async loading flag) can legitimately differ between the Layout call and the Repaint call of the
// same cycle if that thread writes to it in between - and if that value gates whether a GUILayout
// call happens at all, the control-ID stack no longer matches and Unity throws ArgumentException.
// A GuiSnapshot<T> caches its source once per cycle (via GuiSnapshots.RefreshAll, called on the
// Layout event - see Main.OnGUI) so every event type in that cycle reads an identical value no
// matter what the source does in between.
public sealed class GuiSnapshot<T> {
    private readonly Func<T> source;
    public T Value { get; private set; }

    internal GuiSnapshot(Func<T> source) {
        this.source = source;
        Value = source();
    }

    internal void Refresh() => Value = source();
}

public static class GuiSnapshots {
    private static readonly List<Action> refreshers = [];

    // Registers a new snapshot and takes its first reading immediately. Keep the returned
    // GuiSnapshot<T> around (e.g. as a field) and read its .Value instead of the live source
    // anywhere that value decides which/how many GUILayout calls run.
    public static GuiSnapshot<T> Register<T>(Func<T> source) {
        var snapshot = new GuiSnapshot<T>(source);
        refreshers.Add(snapshot.Refresh);
        return snapshot;
    }

    public static void RefreshAll() {
        foreach(var refresh in refreshers) {
            refresh();
        }
    }
}
