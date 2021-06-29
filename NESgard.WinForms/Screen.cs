using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XInput.Wrapper;
using NAudio.Wave;
using NESgard.Emulator;
using System.IO.Pipelines;

namespace NESgard.WinForms
{
    public partial class Screen : Form
    {
        Cpu cpu;

        Task cpuTask;

        bool exitCpu = false;

        PictureBoxInterpolation image = new PictureBoxInterpolation();

        Bitmap bitmap;

        PpuNameTableViewer ppuNameTableViewer;

        bool showPpuNameTableViewer;

        bool pause;

        bool isActive;

        X.Gamepad gamepad;

        Task gamepadTask;

        Pipe wavePipe;
        RawSourceWaveStream waveProvider;
        IWavePlayer waveOut;

        public Screen()
        {
            Text = "NESgard";
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(256 * 2, 240 * 2 + 24);
            BackColor = Color.Black;

            Activated += new EventHandler(HandleActivate);
            Deactivate += new EventHandler(HandleDeactivate);

            bitmap = new Bitmap(256, 240, PixelFormat.Format24bppRgb);

            image.InterpolationMode = InterpolationMode.NearestNeighbor;
            image.Dock = DockStyle.Fill;
            image.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(image);

            KeyDown += new KeyEventHandler(this.HandleKeyDown);
            KeyUp += new KeyEventHandler(this.HandleKeyUp);

            wavePipe = new Pipe();
            waveProvider = new RawSourceWaveStream(wavePipe.Reader.AsStream(), new WaveFormat(48000, 1));
            waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);

            if (X.Available)
            {
                gamepad = X.AvailableGamepads.FirstOrDefault();

                if (gamepad != null)
                {
                    X.Gamepad.Enable = true;

                    gamepadTask = new Task(() =>
                    {
                        while (true)
                        {
                            if (gamepad.Update())
                            {
                                cpu.bus.controller1.Update(ControllerButton.BUTTON_A, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.A));
                                cpu.bus.controller1.Update(ControllerButton.BUTTON_B, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.X));
                                cpu.bus.controller1.Update(ControllerButton.START, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Start));
                                cpu.bus.controller1.Update(ControllerButton.SELECT, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Back));
                                cpu.bus.controller1.Update(ControllerButton.UP, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Up));
                                cpu.bus.controller1.Update(ControllerButton.DOWN, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Down));
                                cpu.bus.controller1.Update(ControllerButton.LEFT, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Left));
                                cpu.bus.controller1.Update(ControllerButton.RIGHT, gamepad.ButtonsState.HasFlag(X.Gamepad.ButtonFlags.Right));
                            }

                            Thread.Sleep(50);
                        }
                    });

                    gamepadTask.Start();
                }
            }

            Console.WriteLine("Using gamepad {0}", gamepad != null);

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open", null, new EventHandler(LoadRom)));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Exit", null, new EventHandler(Exit)));

            var debugMenu = new ToolStripMenuItem("Debug");
            debugMenu.DropDownItems.Add(new ToolStripMenuItem("PPU Name Table Viewer", null, new EventHandler(TogglePpuNameTableViewer)));

            menu.Items.Add(fileMenu);
            menu.Items.Add(debugMenu);
            MainMenuStrip = menu;

            Controls.Add(menu);

            Focus();
            BringToFront();
        }

        protected override void Dispose(bool disposing)
        {
            exitCpu = true;

            waveOut.Dispose();
            waveProvider.Dispose();
            image.Dispose();
            bitmap.Dispose();

            base.Dispose(disposing);
        }

        protected void LoadRom(object sender, EventArgs e)
        {
            if (ppuNameTableViewer != null)
            {
                ppuNameTableViewer.Dispose();
                ppuNameTableViewer = null;
            }

            if (cpuTask != null)
            {
                pause = false;
                exitCpu = true;
                cpuTask.Wait();
                cpuTask = null;
                exitCpu = false;
            }

            if (cpu != null)
            {
                waveOut.Stop();
                cpu.Dispose();
                cpu = null;
            }

            string file;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "NES roms (*.nes)|*.nes|All files|*.*";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                file = dialog.FileName;
            }

            try
            {
                var rom = new Cartridge(file);
                cpu = new Cpu(rom);
                cpu.Reset(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message, "Could not load NES rom", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (showPpuNameTableViewer)
            {
                ppuNameTableViewer = new PpuNameTableViewer(cpu);
                ppuNameTableViewer.Show();
            }

            cpuTask = new Task(() =>
            {
                try
                {
                    var frameCount = 0;
                    var fpsWatch = Stopwatch.StartNew();
                    while (true)
                    {
                        var wasPlaying = waveOut.PlaybackState == PlaybackState.Playing;
                        while (pause || !isActive)
                        {
                            waveOut.Pause();
                            fpsWatch.Stop();
                            Thread.Sleep(100);
                        }

                        if (exitCpu)
                            break;

                        if (wasPlaying)
                            waveOut.Play();

                        fpsWatch.Start();
                        var watch = Stopwatch.StartNew();

                        cpu.Run();
                        frameCount++;

                        if (exitCpu)
                            break;

                        var samples = cpu.bus.apu.GetSamples();
                        try
                        {
                            wavePipe.Writer.WriteAsync(samples);
                            if (waveOut.PlaybackState == PlaybackState.Stopped)
                            {
                                waveOut.Play();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }

                        Invalidate();

                        if (frameCount % 5 == 0)
                            ppuNameTableViewer?.Rerender();

                        if (frameCount % 60 == 0)
                        {
                            var fps = 60 / fpsWatch.Elapsed.TotalSeconds;
                            fpsWatch.Restart();
                            Console.WriteLine("FPS: {0}", fps);
                        }

                        while (watch.Elapsed.TotalMilliseconds < 16.5)
                            Thread.Sleep(0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });

            cpuTask.Start();

            Focus();
        }

        protected void Exit(object sender, EventArgs e)
        {
            Close();
        }

        protected void TogglePpuNameTableViewer(object sender, EventArgs e)
        {
            showPpuNameTableViewer = !showPpuNameTableViewer;
            if (showPpuNameTableViewer)
            {
                if (cpu != null)
                {
                    ppuNameTableViewer = new PpuNameTableViewer(cpu);
                    ppuNameTableViewer.Show();
                }
                ((ToolStripMenuItem)sender).Checked = true;
            }
            else
            {
                if (ppuNameTableViewer != null)
                {
                    ppuNameTableViewer.Close();
                    ppuNameTableViewer = null;
                }
                ((ToolStripMenuItem)sender).Checked = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (cpu != null)
            {
                cpu.bus.ppu.GetImage(bitmap);
                image.Image = bitmap;
            }
            base.OnPaint(e);
        }

        protected void HandleActivate(object sender, EventArgs e)
        {
            isActive = true;
        }

        protected void HandleDeactivate(object sender, EventArgs e)
        {
            isActive = false;
        }

        protected void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (cpu == null)
                return;

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
            if (cpu == null)
                return;

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
