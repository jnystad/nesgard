using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using NESgard.Emulator;

namespace NESgard.WinForms
{
    public partial class PpuNameTableViewer : Form
    {
        Cpu cpu;

        PictureBox ppu;

        Bitmap ppuImage;

        ToolStripLabel status;

        public PpuNameTableViewer(Cpu cpu)
        {
            this.cpu = cpu;

            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(512, 502);
            Text = "PPU Name Table Viewer";

            var statusStrip = new StatusStrip();
            status = new ToolStripLabel();
            status.Text = "PPU";
            statusStrip.Items.Add(status);

            ppu = new PictureBox();
            ppu.Dock = DockStyle.Fill;
            ppu.SizeMode = PictureBoxSizeMode.Zoom;
            ppu.BackColor = Color.Gray;

            ppuImage = new Bitmap(512, 480, PixelFormat.Format32bppArgb);

            Controls.Add(ppu);
            Controls.Add(statusStrip);
        }

        public void Rerender()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            cpu.bus.ppu.GetBackgroundBuffers(ppuImage);
            status.Text = cpu.bus.ppu.GetStatusText();
            ppu.Image = ppuImage;
            base.OnPaint(e);
        }
    }
}
