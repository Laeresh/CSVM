namespace CSVM.Utils;

/// <summary>
/// The Keep Camera Zoom option and the external-view zoom it remembers in the options file. On,
/// which a file that never set it reads as, the zoom survives every reset the original makes. The
/// remembering pilot's zoom is then restored at each camera init. Off, the original's resets apply
/// and nothing is written. Over no store (<c>--det</c>, or before a launch applies one) it reads
/// on, seeds the near end and writes nothing.
/// <c>Flight.Camera.CameraController</c> is the one reader; docs/architecture/Utils.md.
/// </summary>
public sealed class CameraZoomMemory
{
    /// <summary>The zoom axis's rest pose, the near end, which every reset returns to.</summary>
    public const float NearEnd = 0f;

    private OptionsStore? _store;

    // The file's zoom as last read or written, so a seed reads no file and an unchanged zoom writes none.
    private float? _saved;

    /// <summary>The option as <paramref name="store"/> holds it, or the no-file reading where it is
    /// null.</summary>
    public CameraZoomMemory(OptionsStore? store) => Reload(store);

    /// <summary>The memory the running process reads, over no store until a launch applies one. One
    /// instance for the whole process, reloaded in place, so a camera holding it sees every apply.</summary>
    public static CameraZoomMemory Current { get; } = new(null);

    /// <summary>Whether the zoom is kept: on unless the file says off.</summary>
    public bool Keep { get; private set; }

    /// <summary>Reloads <see cref="Current"/> over the user options file, or over none under
    /// <paramref name="det"/>, and logs the option. The launch and every options apply call it.</summary>
    public static void ApplySaved(bool det)
    {
        Current.Reload(det ? null : OptionsStore.UserOptions());
        Log.Info("flight", $"keep camera zoom={Current.Keep} source={(det ? "--det" : OptionsStore.FileName)}");
    }

    /// <summary>Stages an Options apply's switch on <paramref name="options"/>: off holds no zoom, so
    /// the zoom an earlier on saved goes with it.</summary>
    public static void Stage(OptionsDef options, bool? keep)
    {
        options.KeepCameraZoom = keep;
        if (keep == false)
        {
            options.CameraZoom = null;
        }
    }

    /// <summary>Takes the option and the zoom from <paramref name="store"/> in one read, or the no-file
    /// reading where it is null.</summary>
    public void Reload(OptionsStore? store)
    {
        _store = store;
        var options = store?.Load();
        Keep = options?.KeepCameraZoom != false;
        _saved = options?.CameraZoom;
    }

    /// <summary>The zoom a camera init opens on: the saved one while kept, else <see cref="NearEnd"/>.</summary>
    public float Seed() => Keep && _saved is { } zoom ? zoom : NearEnd;

    /// <summary>Writes <paramref name="zoom"/> to the file while kept, and returns whether it wrote.
    /// Off, over no store, or with that zoom already saved it writes nothing. A never-set zoom counts
    /// as the near end. Only a write reads the file.</summary>
    public bool Save(float zoom)
    {
        if (!Keep || _store == null || (_saved ?? NearEnd) == zoom)
        {
            return false;
        }

        var options = _store.Load();
        options.CameraZoom = zoom;
        _store.Save(options);
        _saved = zoom;
        return true;
    }
}
