using CSVM.Mech3;

namespace CSVM.UI.Hangar;

/// <summary>One picture a page asks the shell to draw: a decoded TGA (blueprint, icon, paint
/// preview) plus the caption under it. The page decodes; the shell owns the one Godot texture.</summary>
public sealed record HangarArt(TgaImage Image, string Caption);
