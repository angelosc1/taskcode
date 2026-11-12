using System.Drawing;

namespace Taskcode.Probe
{
    internal sealed class DetailInteraction
    {
        private bool previousLeftDown;
        internal bool Pinned { get; private set; }
        internal void Reset(bool pinned, bool leftDown) { Pinned = pinned; previousLeftDown = leftDown; }
        internal bool ShouldDismiss(Rectangle card, Rectangle anchor, Point pointer, bool leftDown)
        {
            if (Pinned)
            {
                bool outsideClick = leftDown && !previousLeftDown && !card.Contains(pointer);
                previousLeftDown = leftDown;
                return outsideClick;
            }
            if (card.Contains(pointer) || anchor.Contains(pointer)) return false;
            int left = System.Math.Max(card.Left, anchor.Left), right = System.Math.Min(card.Right, anchor.Right);
            if (left < right)
            {
                var bridge = card.Bottom <= anchor.Top ? Rectangle.FromLTRB(left, card.Bottom, right, anchor.Top) :
                    anchor.Bottom <= card.Top ? Rectangle.FromLTRB(left, anchor.Bottom, right, card.Top) : Rectangle.Empty;
                if (bridge.Contains(pointer)) return false;
            }
            // Vertical taskbars open the card beside the indicator; bridge the horizontal gap too.
            int top = System.Math.Max(card.Top, anchor.Top), bottom = System.Math.Min(card.Bottom, anchor.Bottom);
            if (top < bottom)
            {
                var bridge = card.Right <= anchor.Left ? Rectangle.FromLTRB(card.Right, top, anchor.Left, bottom) :
                    anchor.Right <= card.Left ? Rectangle.FromLTRB(anchor.Right, top, card.Left, bottom) : Rectangle.Empty;
                if (bridge.Contains(pointer)) return false;
            }
            return true;
        }
    }
}
