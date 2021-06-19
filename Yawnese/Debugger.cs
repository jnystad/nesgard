using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yawnese.Emulator;

namespace Yawnese
{
    public partial class Debugger : Form
    {
        Cpu cpu;

        TextBox memory;

        PictureBox ppu;

        Bitmap ppuImage;

        public Debugger(Cpu cpu)
        {
            this.cpu = cpu;

            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1024, 1000);
            Text = "Yawnese - Debugger";

            memory = new TextBox();
            memory.Multiline = true;
            memory.Height = 1000 - 240;
            memory.Width = 1024;
            memory.ScrollBars = ScrollBars.Vertical;
            memory.Text = "";
            memory.Font = new Font(FontFamily.GenericMonospace, 10);

            ppu = new PictureBox();
            ppu.Height = 240;
            ppu.Width = 512;
            ppu.Top = 1000 - 240;

            ppuImage = new Bitmap(512, 240, PixelFormat.Format24bppRgb);

            Controls.Add(memory);
            Controls.Add(ppu);
        }

        public void UpdateMemory()
        {
            var sb = new StringBuilder();
            var vram = cpu.bus.ppu.vram;

            for (var i = 0x2000; i < 0x3000; ++i)
            {
                if (i % 32 == 0)
                    sb.AppendFormat("{0:X4} ", i);

                sb.AppendFormat("{0:X2} ", vram[i]);

                if (i % 32 == 31)
                    sb.AppendLine();
            }

            memory.Text = sb.ToString();
        }

        public void UpdateBackgroundBuffers()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            cpu.bus.ppu.GetBackgroundBuffers(ppuImage);
            ppu.Image = ppuImage;
            base.OnPaint(e);
        }
    }
}
