using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CSVM.UI.Menu;

/// <summary>
/// Where menu presentations register: one factory per <see cref="PresentationId"/>, filled once
/// at startup. The host asks it for a fresh instance on every activation, which is what makes a
/// presentation switch discard transient presentation state by construction. Availability (the
/// Original presentation's asset manifest) is a separate question answered before asking here;
/// the registry only says what exists in the build.
/// </summary>
public sealed class PresentationRegistry
{
    private readonly Dictionary<PresentationId, Func<IMenuPresentation>> _factories = new();

    /// <summary>Every registered identity, in no promised order.</summary>
    public IReadOnlyCollection<PresentationId> Registered => _factories.Keys;

    /// <summary>Registers one presentation. A second registration of the same id throws: two
    /// rivals for one identity is a wiring error, not an override mechanism.</summary>
    public void Register(PresentationId id, Func<IMenuPresentation> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(id, factory))
        {
            throw new InvalidOperationException($"Presentation '{id}' is already registered.");
        }
    }

    /// <summary>Whether the id names a registered presentation.</summary>
    public bool IsRegistered(PresentationId id) => _factories.ContainsKey(id);

    /// <summary>A fresh instance of the registered presentation, or false for an unknown id,
    /// which the caller answers by falling back rather than throwing at a persisted value.</summary>
    public bool TryCreate(PresentationId id, [NotNullWhen(true)] out IMenuPresentation? presentation)
    {
        if (_factories.TryGetValue(id, out var factory))
        {
            presentation = factory();
            return true;
        }

        presentation = null;
        return false;
    }
}
