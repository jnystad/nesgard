using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yawnese.Emulator;

namespace Yawnese
{
    public partial class Screen : Form
    {
        Cpu cpu;

        Task cpuTask;

        PictureBox image = new PictureBox();

        Bitmap bitmap;

        Debugger debugger;

        bool pause;

        public Screen()
        {
            InitializeComponent();

            bitmap = new Bitmap(256, 240, PixelFormat.Format24bppRgb);
            image.Dock = DockStyle.Fill;
            image.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(image);

            var rom = new Cartridge("roms/super_mario_bros.nes");
            cpu = new Cpu(rom);
            cpu.Reset(false);

            debugger = new Debugger(cpu);
            debugger.Show();

            cpuTask = new Task(() =>
            {
                try
                {
                    var frameCount = 0;
                    var fpsWatch = Stopwatch.StartNew();
                    while (true)
                    {
                        while (pause)
                            Thread.Sleep(100);
                        var watch = Stopwatch.StartNew();
                        cpu.Run();
                        frameCount++;

                        Invalidate();

                        debugger.UpdateBackgroundBuffers();

                        if (frameCount % 60 == 0)
                        {
                            var fps = 60 / fpsWatch.Elapsed.TotalSeconds;
                            fpsWatch.Restart();
                            Console.WriteLine("FPS: {0}", fps);
                        }

                        debugger.UpdateMemory();

                        while (watch.ElapsedMilliseconds < 16)
                            Thread.Sleep(0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });

            cpuTask.Start();

            KeyDown += new KeyEventHandler(this.HandleKeyDown);
            KeyUp += new KeyEventHandler(this.HandleKeyUp);

            Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            cpu.bus.ppu.GetImage(bitmap);
            image.Image = bitmap;
            base.OnPaint(e);
        }

        protected void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
                cpu.bus.controller1.Update(ControllerButton.SELECT, true);
            if (e.KeyCode == Keys.Enter)
                cpu.bus.controller1.Update(ControllerButton.START, true);
            if (e.KeyCode == Keys.A)
                cpu.bus.controller1.Update(ControllerButton.BUTTON_A, true);
            if (e.KeyCode == Keys.S)
                cpu.bus.controller1.Update(ControllerButton.BUTTON_B, true);
            if (e.KeyCode == Keys.Up)
                cpu.bus.controller1.Update(ControllerButton.UP, true);
            if (e.KeyCode == Keys.Down)
                cpu.bus.controller1.Update(ControllerButton.DOWN, true);
            if (e.KeyCode == Keys.Left)
                cpu.bus.controller1.Update(ControllerButton.LEFT, true);
            if (e.KeyCode == Keys.Right)
                cpu.bus.controller1.Update(ControllerButton.RIGHT, true);

            if (e.KeyCode == Keys.Escape)
                pause = !pause;
        }

        protected void HandleKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
                cpu.bus.controller1.Update(ControllerButton.SELECT, false);
            if (e.KeyCode == Keys.Enter)
                cpu.bus.controller1.Update(ControllerButton.START, false);
            if (e.KeyCode == Keys.A)
                cpu.bus.controller1.Update(ControllerButton.BUTTON_A, false);
            if (e.KeyCode == Keys.S)
                cpu.bus.controller1.Update(ControllerButton.BUTTON_B, false);
            if (e.KeyCode == Keys.Up)
                cpu.bus.controller1.Update(ControllerButton.UP, false);
            if (e.KeyCode == Keys.Down)
                cpu.bus.controller1.Update(ControllerButton.DOWN, false);
            if (e.KeyCode == Keys.Left)
                cpu.bus.controller1.Update(ControllerButton.LEFT, false);
            if (e.KeyCode == Keys.Right)
                cpu.bus.controller1.Update(ControllerButton.RIGHT, false);
        }
    }
}
