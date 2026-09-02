using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CSVM.UI.Menu;

/// <summary>
/// The host-owned set of shared menu features, fetched by concrete type so a presentation reads
/// typed state and calls semantic operations directly. One instance outlives every presentation
/// switch; <see cref="DiscardTransient"/> is the whole of what a switch discards, so anything a
/// feature keeps past it is by definition persisted data.
/// </summary>
public sealed class MenuFeatureSet
{
    private readonly Dictionary<Type, IMenuFeature> _features = new();

    /// <summary>Registers one feature under its concrete type. A second add of the same type
    /// throws: two rivals for one feature is a wiring error, not a configuration.</summary>
    public void Add<T>(T feature)
        where T : class, IMenuFeature
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (!_features.TryAdd(typeof(T), feature))
        {
            throw new InvalidOperationException($"Feature {typeof(T).Name} is already registered.");
        }
    }

    /// <summary>The registered feature of the given type. Missing is a wiring error and throws;
    /// a presentation probing for an optional feature uses <see cref="TryGet{T}"/>.</summary>
    public T Get<T>()
        where T : class, IMenuFeature
    {
        if (_features.TryGetValue(typeof(T), out var feature))
        {
            return (T)feature;
        }

        throw new InvalidOperationException($"Feature {typeof(T).Name} is not registered.");
    }

    /// <summary>The registered feature of the given type, or false when there is none.</summary>
    public bool TryGet<T>([NotNullWhen(true)] out T? feature)
        where T : class, IMenuFeature
    {
        if (_features.TryGetValue(typeof(T), out var found))
        {
            feature = (T)found;
            return true;
        }

        feature = null;
        return false;
    }

    /// <summary>Discards every feature's transient state, in registration order. The presentation
    /// switch calls this once, before the next presentation activates at its top level.</summary>
    public void DiscardTransient()
    {
        foreach (var feature in _features.Values)
        {
            feature.Discard();
        }
    }
}
