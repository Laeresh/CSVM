using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// Has the root viewport's canvas layers of chosen numbers draw into a <see cref="SubViewport"/>
/// instead, through <see cref="CanvasLayer.CustomViewport"/>. Only the drawing moves. Each layer's
/// controls stay in the root's tree, lay out against the root's visible rect and take its input. A
/// stereo root draws no canvas layer, so this is how its 2D reaches a quad in the world. Layers are
/// caught as they enter the tree and handed back as they leave it.
/// Module entry: docs/architecture/UI.md.
/// </summary>
public sealed class CanvasRehost
{
    private readonly Viewport _root;
    private readonly SubViewport _target;
    private readonly Func<int, bool> _hosts;
    private readonly Dictionary<CanvasLayer, Action> _hosted = new();
    private SceneTree? _tree;

    /// <summary>Moves into <paramref name="target"/> every canvas layer of <paramref name="root"/>
    /// whose number <paramref name="hosts"/> accepts, once <see cref="Attach"/> runs.</summary>
    public CanvasRehost(Viewport root, SubViewport target, Func<int, bool> hosts)
    {
        _root = root;
        _target = target;
        _hosts = hosts;
    }

    /// <summary>The layers drawing into the target now.</summary>
    public IReadOnlyCollection<CanvasLayer> Layers => _hosted.Keys;

    /// <summary>Takes every matching layer already in the tree, and each one that enters later.</summary>
    public void Attach()
    {
        if (_tree != null)
        {
            return;
        }

        _tree = _root.GetTree();
        _tree.NodeAdded += OnNodeAdded;
        Sweep(_root);
    }

    /// <summary>Stops taking layers and hands every hosted one back to the root.</summary>
    public void Detach()
    {
        if (_tree != null)
        {
            _tree.NodeAdded -= OnNodeAdded;
            _tree = null;
        }

        foreach (var layer in _hosted.Keys.ToList())
        {
            Return(layer);
        }
    }

    /// <summary>The first hosted layer that draws something and whose number
    /// <paramref name="which"/> accepts, or null. A layer draws when it is visible and so is at
    /// least one canvas item directly under it.</summary>
    public CanvasLayer? Showing(Func<int, bool> which)
    {
        foreach (var layer in _hosted.Keys)
        {
            if (GodotObject.IsInstanceValid(layer) && which(layer.Layer) && Shows(layer))
            {
                return layer;
            }
        }

        return null;
    }

    private static bool Shows(CanvasLayer layer)
    {
        if (!layer.Visible || !layer.IsInsideTree())
        {
            return false;
        }

        foreach (var child in layer.GetChildren())
        {
            if (child is CanvasItem { Visible: true })
            {
                return true;
            }
        }

        return false;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is CanvasLayer layer)
        {
            Take(layer);
        }
    }

    // A layer under another viewport (a splitscreen pane, the cockpit pass) is that viewport's.
    private void Sweep(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasLayer layer)
            {
                Take(layer);
            }

            if (child is not Viewport)
            {
                Sweep(child);
            }
        }
    }

    private void Take(CanvasLayer layer)
    {
        if (_hosted.ContainsKey(layer) || !_hosts(layer.Layer) || layer.GetViewport() != _root)
        {
            return;
        }

        layer.CustomViewport = _target;
        Action leaving = () => Return(layer);
        layer.TreeExiting += leaving;
        _hosted[layer] = leaving;
    }

    // ⚠ Do not let a hosted layer leave the tree still pointed at the target. On entry Godot ties
    // the parent's child_order_changed to the root. On exit it unties it from the current viewport,
    // which fails with an engine error unless the layer points at the root again.
    private void Return(CanvasLayer layer)
    {
        if (!_hosted.Remove(layer, out var leaving) || !GodotObject.IsInstanceValid(layer))
        {
            return;
        }

        layer.TreeExiting -= leaving;
        layer.CustomViewport = _root;
    }
}
