using System;

/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

namespace NESgard.Emulator
{
    public class Apu
    {
        public Bus bus;


        Pulse pulse1 = new Pulse(false);
        Pulse pulse2 = new Pulse(true);
        Triangle triangle = new Triangle();
        Noise noise = new Noise();
        DMC dmc = new DMC();
        ApuStatus status;
        ApuFrameCounter frameCounterStatus;
        int frameClockCounter;
        bool timerClockCounter;
        byte frameStep;


        float[] pulseValues;
        float[] tndValues;

        public Apu(Bus bus)
        {
            this.bus = bus;

            pulseValues = new float[31];
            for (var i = 0; i < 31; ++i)
            {
                pulseValues[i] = 95.52f / (8128.0f / i + 100);
            }
            tndValues = new float[203];
            for (var i = 0; i < 203; ++i)
            {
                tndValues[i] = 163.67f / (24329.0f / i + 100);
            }
        }

        public void Reset()
        {
            buffer = new float[10000];
            buffer[0] = 0;
            buffer[1] = 1;
            bufferIdx = 2;
            bufferCounter = 0;

            status = 0;
            pulse1.Reset();
            pulse2.Reset();
            triangle.Reset();
            noise.Reset();
            dmc.Reset();
            frameClockCounter = 0;
            timerClockCounter = false;
            frameCounterStatus = ApuFrameCounter.IrqDisable;
        }

        float[] buffer;
        int bufferIdx = 0;
        int bufferCounter = 0;

        public byte[] GetSamples()
        {
            var samples = new byte[(bufferIdx >> 1) - 1];
            var i = 2;
            for (; i < bufferIdx - 1; i += 2)
            {
                samples[(i >> 1) - 1] = (byte)((buffer[i] + buffer[i + 1]) * 50.0f);
            }
            buffer[0] = buffer[i - 2];
            buffer[1] = buffer[i - 1];
            if ((bufferIdx & 1) == 1)
            {
                buffer[2] = buffer[bufferIdx - 1];
                bufferIdx = 3;
            }
            else
            {
                bufferIdx = 2;
            }
            return samples;
        }

        public float Output()
        {
            var p1 = pulse1.Output;
            var p2 = pulse2.Output;
            var t = triangle.Output;
            var n = noise.Output;
            var d = dmc.Output;

            var p = pulseValues[p1 + p2];
            var tnd = tndValues[3 * t + 2 * n + d];
            return p + tnd;
        }

        public void Tick()
        {
            ++frameClockCounter;
            if (frameClockCounter == 7457)
            {
                frameClockCounter = 0;
                ClockFrame();
            }

            pulse1.ClockTimer();
            pulse2.ClockTimer();
            triangle.ClockTimer();
            timerClockCounter = !timerClockCounter;
            if (timerClockCounter)
            {
                noise.ClockTimer();
                dmc.ClockTimer(this);
            }

            ++bufferCounter;
            switch (bufferCounter)
            {
                case 9:
                case 18:
                case 27:
                    buffer[bufferIdx++] = Output();
                    break;
                case 28:
                    bufferCounter = 0;
                    break;
            }
        }

        public void ClockFrame()
        {
            ++frameStep;
            if ((frameCounterStatus & ApuFrameCounter.Step) != 0)
            {
                if (frameStep == 5)
                    frameStep = 0;

                if (frameStep == 0 || frameStep == 2)
                    ClockLengthSweep();

                if (frameStep != 4)
                    ClockEnvelope();
            }
            else
            {
                if (frameStep == 4)
                    frameStep = 0;

                if (frameStep == 3 && (frameCounterStatus & ApuFrameCounter.IrqDisable) == 0)
                {
                    status |= ApuStatus.FrameIrq;
                }

                if ((frameStep & 1) == 1)
                {
                    ClockLengthSweep();
                }
                ClockEnvelope();
            }
        }

        void ClockLengthSweep()
        {
            pulse1.ClockLength();
            pulse1.ClockSweep();
            pulse2.ClockLength();
            pulse2.ClockSweep();
            triangle.ClockLength();
            noise.ClockLength();
        }

        void ClockEnvelope()
        {
            pulse1.ClockEnvelope();
            pulse2.ClockEnvelope();
            triangle.ClockLinear();
            noise.ClockEnvelope();
        }

        public void Interrupt()
        {
            status |= ApuStatus.DmcIrq;
        }

        public bool HasInterrupt()
        {
            return status.HasFlag(ApuStatus.FrameIrq) || status.HasFlag(ApuStatus.DmcIrq);
        }

        public byte Read(ushort addr)
        {
            switch (addr)
            {
                case 0x4015: return ReadStatus();
            }
            return 0;
        }

        public void Write(ushort addr, byte data)
        {
            switch (addr)
            {
                case 0x4000:
                case 0x4001:
                case 0x4002:
                case 0x4003:
                    pulse1.Write(addr & 0x3, data);
                    break;

                case 0x4004:
                case 0x4005:
                case 0x4006:
                case 0x4007:
                    pulse2.Write(addr & 0x3, data);
                    break;

                case 0x4008:
                case 0x4009:
                case 0x400A:
                case 0x400B:
                    triangle.Write(addr & 0x3, data);
                    break;

                case 0x400C:
                case 0x400D:
                case 0x400E:
                case 0x400F:
                    noise.Write(addr & 0x3, data);
                    break;

                case 0x4010:
                case 0x4011:
                case 0x4012:
                case 0x4013:
                    if (dmc.Write(addr & 0x3, data) == DMC.DmcWriteResult.ClearIrq)
                        status &= ~ApuStatus.DmcIrq;
                    break;

                case 0x4015:
                    WriteStatus(data);
                    break;

                case 0x4017:
                    WriteFrameCounter(data);
                    break;
            }
        }

        byte ReadStatus()
        {
            var data = status & (ApuStatus.DmcIrq | ApuStatus.FrameIrq);
            if (pulse1.LengthCounter > 0)
                data |= ApuStatus.Pulse1;
            if (pulse2.LengthCounter > 0)
                data |= ApuStatus.Pulse2;
            if (triangle.LengthCounter > 0)
                data |= ApuStatus.Triangle;
            if (noise.LengthCounter > 0)
                data |= ApuStatus.Noise;
            if (dmc.lengthCounter > 0)
                data |= ApuStatus.Dmc;
            status &= ~ApuStatus.FrameIrq;
            return (byte)data;
        }

        void WriteStatus(byte data)
        {
            status = (ApuStatus)(((byte)status & 0x40) | (data & 0x1F));
            pulse1.Enabled = status.HasFlag(ApuStatus.Pulse1);
            if (!pulse1.Enabled)
                pulse1.LengthCounter = 0;
            pulse2.Enabled = status.HasFlag(ApuStatus.Pulse2);
            if (!pulse2.Enabled)
                pulse2.LengthCounter = 0;
            triangle.Enabled = status.HasFlag(ApuStatus.Triangle);
            if (!triangle.Enabled)
                triangle.LengthCounter = 0;
            noise.Enabled = status.HasFlag(ApuStatus.Noise);
            if (!noise.Enabled)
                noise.LengthCounter = 0;
            dmc.Enabled = status.HasFlag(ApuStatus.Dmc);
            if (!dmc.Enabled)
                dmc.lengthCounter = 0;
            else
                dmc.Resume();
        }

        void WriteFrameCounter(byte data)
        {
            frameCounterStatus = (ApuFrameCounter)(data & 0xC0);
            if ((frameCounterStatus & ApuFrameCounter.IrqDisable) != 0)
                status &= ~ApuStatus.FrameIrq;
            frameClockCounter = 0;
            if ((frameCounterStatus & ApuFrameCounter.Step) != 0)
            {
                frameClockCounter = 4;
                ClockFrame();
            }
        }
    }
}
