using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NESgard.WinForms
{
    public class PictureBoxInterpolation : PictureBox
    {
        public InterpolationMode InterpolationMode { get; set; }

        protected override void OnPaint(PaintEventArgs paintEventArgs)
        {
            paintEventArgs.Graphics.InterpolationMode = InterpolationMode;
            base.OnPaint(paintEventArgs);
        }
    }
}