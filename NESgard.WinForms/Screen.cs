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
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace NESgard.WinForms
{
    public partial class Screen : Form
    {
        Cpu cpu;

        Task cpuTask;

        bool exitCpu = false;

        PictureBoxInterpolation image = new PictureBoxInterpolation();

        Bitmap bitmap;
        Bitmap pauseBitmap;

        PpuNameTableViewer ppuNameTableViewer;

        bool showPpuNameTableViewer;

        bool pause;

        bool isActive;

        X.Gamepad gamepad;

        Task gamepadTask;

        Pipe wavePipe;
        RawSourceWaveStream waveProvider;
        IWavePlayer waveOut;

        public class Settings
        {
            public List<string> recentFiles { get; set; } = new List<string>();
        }

        Settings settings;

        ToolStripMenuItem recentFilesMenu;

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

            Console.WriteLine("Using gamepad: {0}", gamepad != null);

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open", null, new EventHandler(OpenRom), Keys.Control | Keys.O));
            recentFilesMenu = new ToolStripMenuItem("Recent Files");
            fileMenu.DropDownItems.Add(recentFilesMenu);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Reset", null, new EventHandler(Reset)));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Exit", null, new EventHandler(Exit), Keys.Control | Keys.W));

            var debugMenu = new ToolStripMenuItem("Debug");
            debugMenu.DropDownItems.Add(new ToolStripMenuItem("PPU Name Table Viewer", null, new EventHandler(TogglePpuNameTableViewer)));

            menu.Items.Add(fileMenu);
            menu.Items.Add(debugMenu);
            MainMenuStrip = menu;

            LoadSettings();

            Controls.Add(menu);

            Focus();
            BringToFront();
        }

        protected void LoadRecentFiles()
        {
            recentFilesMenu.DropDownItems.Clear();
            recentFilesMenu.Enabled = false;

            if (settings.recentFiles.Count > 0)
            {
                recentFilesMenu.Enabled = true;
                foreach (var file in settings.recentFiles)
                {
                    var item = new ToolStripMenuItem(Path.GetFileName(file), null, new EventHandler(OpenRecent), file);
                    recentFilesMenu.DropDownItems.Add(item);
                }
            }
        }

        protected void LoadSettings()
        {
            var settingsFile = Path.Combine(Application.UserAppDataPath, "settings.json");
            try
            {
                Console.WriteLine("Loading settings from: {0}", settingsFile);
                using (var stream = File.OpenText(settingsFile))
                {
                    var json = stream.ReadToEnd();
                    settings = JsonSerializer.Deserialize<Settings>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not load settings. Exception: {0}", ex);
                settings = new Settings();
            }

            LoadRecentFiles();
        }

        protected void SaveSettings()
        {
            try
            {
                var settingsFile = Path.Combine(Application.UserAppDataPath, "settings.json");
                Console.WriteLine("Writing settings to: {0}", settingsFile);

                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(settingsFile, json);
                Console.WriteLine(json);

                LoadRecentFiles();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not write settings. Exception: {0}", ex);
            }
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

        protected void OpenRom(object sender, EventArgs e)
        {
            pause = true;
            Invalidate();

            string file;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "NES roms (*.nes)|*.nes|All files|*.*";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                file = dialog.FileName;
            }

            LoadRom(file);
        }

        protected void OpenRecent(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            var file = item.Name;

            LoadRom(file);
        }

        protected void LoadRom(string file)
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
            }
            pause = true;
            exitCpu = false;

            if (cpu != null)
            {
                waveOut.Stop();
                cpu.Dispose();
                cpu = null;
            }

            try
            {
                var rom = new Cartridge(file);
                cpu = new Cpu(rom);
                cpu.Reset(false);

                while (settings.recentFiles.Remove(file)) { }
                settings.recentFiles.Insert(0, file);
                if (settings.recentFiles.Count > 10)
                    settings.recentFiles = settings.recentFiles.Take(10).ToList();
                SaveSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message, "Could not load NES rom", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            pause = false;

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

                        while (watch.Elapsed.TotalMilliseconds < 16.6)
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

        protected void Reset(object sender, EventArgs e)
        {
            if (cpu != null)
                cpu.Reset(false);
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
            if (pause)
            {
                if (pauseBitmap == null)
                {
                    pauseBitmap = new Bitmap(image.Width, image.Height);
                    using (var g = Graphics.FromImage(pauseBitmap))
                    {
                        var rect = new Rectangle(0, 0, pauseBitmap.Width, pauseBitmap.Height);
                        g.DrawImage(bitmap, rect);
                        using (var bgBrush = new SolidBrush(Color.FromArgb(128, Color.DarkSlateGray)))
                            g.FillRectangle(bgBrush, rect);

                        using (var font = new Font(DefaultFont.FontFamily, 16.0f))
                        using (var whiteBrush = new SolidBrush(Color.White))
                        {
                            var paused = "PAUSED";
                            var ms = g.MeasureString(paused, font);
                            var x = rect.Width / 2 - ms.Width / 2;
                            var y = rect.Height / 2 - ms.Height / 2;

                            using (var bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
                                g.FillRectangle(bgBrush, new RectangleF(0, y - 3, rect.Width, ms.Height + 6));

                            g.DrawString(paused, font, whiteBrush, x, y);
                        }
                    }
                    image.Image = pauseBitmap;
                }
            }
            else if (cpu != null)
            {
                if (pauseBitmap != null)
                {
                    pauseBitmap.Dispose();
                    pauseBitmap = null;
                }

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
            {
                pause = !pause;
                Invalidate();
            }
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
