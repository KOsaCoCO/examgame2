using UnityEngine;

namespace NTGD124
{
    // Marks a Vector2Int field to be drawn as "X"/"Z" instead of Unity's default "X"/"Y" -
    // display only, doesn't change the underlying serialized data (still x/y under the hood),
    // so it's safe to add to an already-filled-in field without losing existing values. See
    // GridAxisLabelsDrawer (Editor/) for the actual drawing.
    public class GridAxisLabelsAttribute : PropertyAttribute { }
}
