namespace Frost.Core;
public sealed record CursorRole(string Name, string RegistryName, uint SystemId, string Label, double HotX, double HotY, bool Animated = false)
{
    public string FileName => Name + (Animated ? ".ani" : ".cur");
}
public static class CursorRoles
{
    public static readonly CursorRole[] All =
    [
        new("Normal", "Arrow", 32512, "Normal", 4, 3),
        new("Help", "Help", 32651, "Help", 4, 3),
        new("Working", "AppStarting", 32650, "Background", 4, 3, true),
        new("Busy", "Wait", 32514, "Busy", 16, 16, true),
        new("Precision", "Crosshair", 32515, "Precision", 16, 16),
        new("Text", "IBeam", 32513, "Text", 16, 16),
        new("Handwriting", "NWPen", 32631, "Handwriting", 5, 26),
        new("Unavailable", "No", 32648, "Unavailable", 16, 16),
        new("Vertical", "SizeNS", 32645, "Vertical resize", 16, 16),
        new("Horizontal", "SizeWE", 32644, "Horizontal resize", 16, 16),
        new("DiagonalNWSE", "SizeNWSE", 32642, "Diagonal NW-SE", 16, 16),
        new("DiagonalNESW", "SizeNESW", 32643, "Diagonal NE-SW", 16, 16),
        new("Move", "SizeAll", 32646, "Move", 16, 16),
        new("Alternate", "UpArrow", 32516, "Alternate", 16, 3),
        new("Link", "Hand", 32649, "Link", 12, 3)
    ];
}
