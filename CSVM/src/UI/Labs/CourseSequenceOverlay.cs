using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The course lab's drawing of the sequence over the world. Each used gate's entry numbers float over it,
/// "2, 5" for a gate flown twice. One line joins the gates' opening middles in sequence order.
/// The line carries an arrowhead halfway along each leg, and a short arrow through each entry's gate the
/// way that entry flies it. It is drawn through the ground and the fog, as the selection box is. It is
/// redrawn only when the course changes, so the gates' idle bob of about a metre is not followed.
/// </summary>
public sealed partial class CourseSequenceOverlay : Node3D
{
    // All TUNE at the controls: the arrow through a gate reaches this far each side of its middle, and
    // an arrowhead's barbs are this long.
    private const float CrossArrowM = 15f;
    private const float HeadM = 8f;

    // Legs between gates closer than this (an entry flying the gate just flown) draw no line.
    private const float MinLegM = 1f;

    // A label floats this many pixels over the top of its gate's drawn bounds.
    private const float LabelLiftPx = 18f;

    private static readonly Color PathColour = new(0.35f, 0.9f, 1f);
    private static readonly Color Amber = new(1f, 0.93f, 0.35f);

    private readonly List<Label3D> _labels = new();

    /// <summary>An empty overlay; <see cref="Draw"/> fills it.</summary>
    public CourseSequenceOverlay()
    {
        Name = "course_sequence";
        SetMeta(SelectionService.OverlayMeta, true);
        Path = SelectionService.NewBoxInstance("course_path", PathColour, priority: 9);
        AddChild(Path);
    }

    /// <summary>The sequence line and its arrows, one line-list surface; hidden with no entry.</summary>
    public MeshInstance3D Path { get; }

    /// <summary>One label per pool gate in pool order (pooled, so a longer pool once leaves hidden extras),
    /// shown when the sequence uses that gate. ⚠ Do not photograph their numbers in a one-frame suite;
    /// <see cref="Label3D"/> shapes its text on a deferred call, so that frame draws none.</summary>
    public IReadOnlyList<Label3D> Labels => _labels;

    /// <summary>Draws <paramref name="course"/>'s sequence. <paramref name="gateNodes"/> are its drawn gates in
    /// pool order, in the tree, whose bounds place the labels; the label of <paramref name="selectedId"/> is amber.</summary>
    public void Draw(Course course, IReadOnlyList<Node3D> gateNodes, string? selectedId)
    {
        var mesh = (ImmediateMesh)Path.Mesh;
        mesh.ClearSurfaces();
        Path.Visible = course.Sequence.Count > 0;
        if (course.Sequence.Count > 0)
        {
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            Vector3? previous = null;
            foreach (var entry in course.Sequence)
            {
                var gate = course.Gates.First(g => g.Id == entry.GateId);
                var centre = CourseDraft.Centre(gate);
                var way = CourseDraft.FlownWay(gate, entry);
                Line(mesh, centre - (way * CrossArrowM), centre + (way * CrossArrowM));
                Head(mesh, centre + (way * CrossArrowM), way);
                if (previous is { } from && from.DistanceTo(centre) > MinLegM)
                {
                    Line(mesh, from, centre);
                    Head(mesh, (from + centre) * 0.5f, (centre - from).Normalized());
                }

                previous = centre;
            }

            mesh.SurfaceEnd();
        }

        for (int i = 0; i < course.Gates.Count; i++)
        {
            var gate = course.Gates[i];
            var label = LabelAt(i);
            label.Text = CourseDraft.EntryNumbers(course, gate.Id);
            label.Visible = label.Text.Length > 0;
            var box = SelectionService.SubtreeWorldAabb(gateNodes[i]);
            label.Position = new Vector3(gate.Position.X, box.End.Y, gate.Position.Z);
            label.Modulate = gate.Id == selectedId ? Amber : Colors.White;
        }

        for (int i = course.Gates.Count; i < _labels.Count; i++)
        {
            _labels[i].Visible = false;
        }
    }

    private static void Line(ImmediateMesh mesh, Vector3 a, Vector3 b)
    {
        mesh.SurfaceAddVertex(a);
        mesh.SurfaceAddVertex(b);
    }

    // Two barbs back from the tip, in the plane of the way and the horizontal across it.
    private static void Head(ImmediateMesh mesh, Vector3 tip, Vector3 way)
    {
        var across = way.Cross(Vector3.Up);
        across = across.LengthSquared() < 1e-6f ? Vector3.Right : across.Normalized();
        var back = tip - (way * HeadM);
        Line(mesh, tip, back + (across * (0.6f * HeadM)));
        Line(mesh, tip, back - (across * (0.6f * HeadM)));
    }

    private Label3D LabelAt(int i)
    {
        while (_labels.Count <= i)
        {
            // The node labels' look: a constant on-screen size, drawn through the ground.
            var label = new Label3D
            {
                Name = $"entry_numbers_{_labels.Count}",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FixedSize = true,
                PixelSize = 0.0009f,
                FontSize = 48,
                OutlineSize = 14,
                NoDepthTest = true,
                OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
                RenderPriority = 20,
                OutlineRenderPriority = 19,
                Offset = new Vector2(0f, LabelLiftPx),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            AddChild(label);
            _labels.Add(label);
        }

        return _labels[i];
    }
}
