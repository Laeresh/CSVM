// Fixture namespaces for the dependency scanner's own tests: a stand-in presentation namespace
// and a stand-in shared namespace with one clean type and two offenders, one visible in a
// signature and one hidden inside a method body.
namespace CSVM.Tests.SeamScan.Presentation
{
    /// <summary>The type the shared side must not reach.</summary>
    public sealed class Widget
    {
        /// <summary>Some state, so an offender can also touch a field.</summary>
        public int Frames;
    }
}

namespace CSVM.Tests.SeamScan.Feature
{
    /// <summary>References nothing outside its own namespace and the runtime.</summary>
    public sealed class CleanType
    {
        /// <summary>Plain state.</summary>
        public int Count { get; private set; }

        /// <summary>A body with calls, so a clean body is scanned too.</summary>
        public int Bump() => ++Count;
    }

    /// <summary>Carries the banned type in a public signature.</summary>
    public sealed class SignatureOffender
    {
        /// <summary>The signature-level reference the scanner must see.</summary>
        public Presentation.Widget? Exposed;
    }

    /// <summary>Reaches the banned type only inside a method body: a local, a constructor call
    /// and a field store, none visible to signature reflection.</summary>
    public sealed class BodyOffender
    {
        /// <summary>The body-only reference the scanner must see.</summary>
        public int Probe()
        {
            var widget = new Presentation.Widget();
            widget.Frames = 1;
            return widget.Frames;
        }
    }
}
