using System;

/*
 * Source: http://www.slack.net/~ant/nes-emu/apu_ref.txt
 */

namespace NESgard.Emulator
{
    public class Apu
    {
        Bus bus;


        Pulse pulse1;
        Pulse pulse2;
        Triangle triangle;
        Noise noise;
        DMC dmc;
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
            pulse2.isPulse2 = true;

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
            pulse1.lengthCounter = 0;
            pulse2.lengthCounter = 0;
            triangle.lengthCounter = 0;
            noise.lengthCounter = 0;
            noise.shiftRegister = 1;
            dmc.lengthCounter = 0;
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
                samples[(i >> 1) - 1] = (byte)((buffer[i - 2] + buffer[i - 1] + buffer[i] + buffer[i + 1]) * 25.0f);
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
            var p1 = pulse1.dac;
            var p2 = pulse2.dac;
            var t = triangle.dac;
            var n = noise.dac;
            var d = dmc.dac;

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

            triangle.ClockTimer();
            timerClockCounter = !timerClockCounter;
            if (timerClockCounter)
            {
                pulse1.ClockTimer();
                pulse2.ClockTimer();
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
            if (frameCounterStatus.HasFlag(ApuFrameCounter.Step))
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

                if (frameStep == 3 && !frameCounterStatus.HasFlag(ApuFrameCounter.IrqDisable))
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
            pulse1.ClockLengthSweep();
            pulse2.ClockLengthSweep();
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
                    pulse1.Write(addr & 0x3, data, status.HasFlag(ApuStatus.Pulse1));
                    break;

                case 0x4004:
                case 0x4005:
                case 0x4006:
                case 0x4007:
                    pulse2.Write(addr & 0x3, data, status.HasFlag(ApuStatus.Pulse2));
                    break;

                case 0x4008:
                case 0x4009:
                case 0x400A:
                case 0x400B:
                    triangle.Write(addr & 0x3, data, status.HasFlag(ApuStatus.Triangle));
                    break;

                case 0x400C:
                case 0x400D:
                case 0x400E:
                case 0x400F:
                    noise.Write(addr & 0x3, data, status.HasFlag(ApuStatus.Noise));
                    break;

                case 0x4010:
                case 0x4011:
                case 0x4012:
                case 0x4013:
                    dmc.Write(addr & 0x3, data, status.HasFlag(ApuStatus.Dmc));
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
            if (pulse1.lengthCounter > 0)
                data |= ApuStatus.Pulse1;
            if (pulse2.lengthCounter > 0)
                data |= ApuStatus.Pulse2;
            if (triangle.lengthCounter > 0)
                data |= ApuStatus.Triangle;
            if (noise.lengthCounter > 0)
                data |= ApuStatus.Noise;
            if (dmc.lengthCounter > 0)
                data |= ApuStatus.Dmc;
            status &= ~ApuStatus.FrameIrq;
            return (byte)data;
        }

        void WriteStatus(byte data)
        {
            status = (ApuStatus)(((byte)status & 0x40) | (data & 0x1F));
            pulse1.enabled = status.HasFlag(ApuStatus.Pulse1);
            if (!pulse1.enabled)
                pulse1.lengthCounter = 0;
            pulse2.enabled = status.HasFlag(ApuStatus.Pulse2);
            if (!pulse2.enabled)
                pulse2.lengthCounter = 0;
            triangle.enabled = status.HasFlag(ApuStatus.Triangle);
            if (!triangle.enabled)
                triangle.lengthCounter = 0;
            noise.enabled = status.HasFlag(ApuStatus.Noise);
            if (!noise.enabled)
                noise.lengthCounter = 0;
            dmc.enabled = status.HasFlag(ApuStatus.Dmc);
            if (!dmc.enabled)
                dmc.lengthCounter = 0;
            else
                dmc.Resume();
        }

        void WriteFrameCounter(byte data)
        {
            frameCounterStatus = (ApuFrameCounter)(data & 0xC0);
            frameClockCounter = 0;
        }

        struct Pulse
        {
            public bool enabled;
            public bool isPulse2;

            public byte volume;
            public bool constantVolume;
            public bool envelopeLoop;
            public byte dutyMode;
            public byte sweepShift;
            public bool sweepNegate;
            public byte sweepPeriod;
            public bool sweepEnabled;
            public ushort timerPeriod;

            public byte lengthCounter;
            byte envelopeCounter;
            byte envelopeVolume;
            byte dutyCounter;
            bool wasUpdated;
            bool wasSweepUpdated;
            byte sweepCounter;
            ushort timerCounter;

            public byte dac;

            public void ClockLengthSweep()
            {
                if (lengthCounter > 0 && !envelopeLoop)
                    --lengthCounter;

                if (wasSweepUpdated)
                {
                    if (sweepEnabled && sweepCounter == 0)
                        Sweep();
                    sweepCounter = (byte)(sweepPeriod + 1);
                    wasSweepUpdated = false;
                }
                else if (sweepCounter > 0)
                {
                    --sweepCounter;
                }
                else
                {
                    if (sweepEnabled)
                        Sweep();
                    sweepCounter = (byte)(sweepPeriod + 1);
                }
            }

            void Sweep()
            {
                var delta = (ushort)(timerPeriod >> sweepShift);
                if (sweepNegate)
                {
                    timerPeriod -= delta;
                    if (isPulse2)
                        --timerPeriod;
                }
                else
                {
                    timerPeriod += delta;
                }
            }

            public void ClockEnvelope()
            {
                if (wasUpdated)
                {
                    envelopeVolume = 15;
                    envelopeCounter = volume;
                    wasUpdated = false;
                }
                else if (envelopeCounter > 0)
                    --envelopeCounter;
                else
                {
                    if (envelopeVolume > 0)
                        --envelopeVolume;
                    else if (envelopeLoop)
                        envelopeVolume = 15;
                    envelopeCounter = volume;
                }
            }

            public void ClockTimer()
            {
                if (timerCounter == 0)
                {
                    timerCounter = timerPeriod;
                    dutyCounter = (byte)((dutyCounter + 1) % 8);

                    var v = constantVolume ? volume : envelopeVolume;

                    if (!enabled)
                        v = 0;

                    if (lengthCounter == 0)
                        v = 0;

                    if (DUTY_SEQUENCES[dutyMode][dutyCounter] == 0)
                        v = 0;

                    if (timerPeriod < 8 || timerPeriod > 0x7FF)
                        v = 0;

                    dac = v;
                }
                else
                    --timerCounter;
            }

            public void Write(int offset, byte data, bool isEnabled)
            {
                switch (offset)
                {
                    case 0:
                        volume = (byte)(data & 0x0F);
                        constantVolume = (data & 0x10) != 0;
                        envelopeLoop = (data & 0x20) != 0;
                        dutyMode = (byte)((data & 0xC0) >> 6);
                        wasUpdated = true;
                        break;

                    case 1:
                        sweepShift = (byte)(data & 0x07);
                        sweepNegate = (data & 0x08) != 0;
                        sweepPeriod = (byte)((data & 0x70) >> 4);
                        sweepEnabled = (data & 0x80) != 0;
                        wasSweepUpdated = true;
                        break;

                    case 2:
                        timerPeriod = (ushort)((timerPeriod & 0x0700) | data);
                        break;

                    case 3:
                        timerPeriod = (ushort)((timerPeriod & 0x00FF) | ((data & 0x7) << 8));
                        lengthCounter = LENGTH_TABLE[data >> 3];
                        dutyCounter = 0;
                        wasUpdated = true;
                        break;
                }
            }
        }

        struct Triangle
        {
            public bool enabled;
            public byte linearCounterLoad;
            public bool linearCounterControl;
            public ushort timerPeriod;

            public bool halt;
            public byte lengthCounter;
            public byte linearCounter;
            ushort timerCounter;
            int sequenceCounter;

            public byte dac;

            public void ClockLength()
            {
                if (lengthCounter > 0 && !halt)
                    --lengthCounter;
            }

            public void ClockLinear()
            {
                if (halt)
                {
                    linearCounter = linearCounterLoad;
                }
                else if (linearCounter > 0)
                {
                    --linearCounter;
                }
                if (!linearCounterControl)
                    halt = false;
            }

            public void ClockTimer()
            {
                if (timerCounter == 0)
                {
                    timerCounter = (ushort)(timerPeriod + 1);

                    dac = 0;

                    if (linearCounter > 0 && lengthCounter > 0)
                    {
                        dac = TRIANGLE_SEQUENCE[sequenceCounter];
                        sequenceCounter = (sequenceCounter + 1) % 32;
                    }
                }
                --timerCounter;
            }

            public void Write(int offset, byte data, bool isEnabled)
            {
                switch (offset)
                {
                    case 0:
                        linearCounterLoad = (byte)(data & 0x7F);
                        linearCounterControl = (data & 0x80) != 0;
                        break;

                    case 1:
                        break;

                    case 2:
                        timerPeriod = (ushort)((timerPeriod & 0x0700) | data);
                        break;

                    case 3:
                        timerPeriod = (ushort)((timerPeriod & 0x00FF) | ((data & 0x7) << 8));
                        lengthCounter = LENGTH_TABLE[data >> 3];
                        halt = true;
                        break;
                }
            }
        }

        struct Noise
        {
            public bool enabled;
            public byte volume;
            public bool constantVolume;
            public bool envelopeLoop;
            public byte period;
            public bool loopNoise;
            public byte lengthCounterLoad;

            public byte lengthCounter;
            ushort timerPeriod;
            ushort timerCounter;
            public int shiftRegister;
            bool wasUpdated;
            byte envelopeVolume;
            byte envelopeCounter;

            public byte dac;

            public void ClockLength()
            {
                if (lengthCounter > 0 && !envelopeLoop)
                    --lengthCounter;
            }

            public void ClockEnvelope()
            {
                if (wasUpdated)
                {
                    envelopeVolume = 15;
                    envelopeCounter = volume;
                    wasUpdated = false;
                }
                else if (envelopeCounter > 0)
                    --envelopeCounter;
                else
                {
                    if (envelopeVolume > 0)
                        --envelopeVolume;
                    else if (envelopeLoop)
                        envelopeVolume = 15;
                    envelopeCounter = volume;
                }
            }

            public void ClockTimer()
            {
                if (timerCounter == 0)
                {
                    timerCounter = timerPeriod;

                    var shift = loopNoise ? 6 : 1;
                    var b1 = shiftRegister & 1;
                    var b2 = (shiftRegister >> shift) & 1;
                    shiftRegister >>= 1;
                    shiftRegister |= (b1 ^ b2) << 14;

                    if (!enabled || lengthCounter == 0 || (shiftRegister & 1) == 1)
                        dac = 0;
                    else
                        dac = constantVolume ? volume : envelopeVolume;
                }
                else
                    --timerCounter;
            }

            public void Write(int offset, byte data, bool isEnabled)
            {
                switch (offset)
                {
                    case 0:
                        volume = (byte)(data & 0x0F);
                        constantVolume = (data & 0x10) != 0;
                        envelopeLoop = (data & 0x20) != 0;
                        break;

                    case 1:
                        break;

                    case 2:
                        period = (byte)(data & 0x0F);
                        timerPeriod = TIMER_PERIODS[period];
                        loopNoise = (data & 0x80) != 0;
                        break;

                    case 3:
                        wasUpdated = true;
                        lengthCounterLoad = (byte)((data & 0xF8) >> 3);
                        if (isEnabled)
                            lengthCounter = LENGTH_TABLE[lengthCounterLoad];
                        break;
                }
            }
        }

        struct DMC
        {
            public bool enabled;
            public byte frequency;
            public bool loop;
            public bool irqEnable;
            public ushort sampleAddress;
            public ushort sampleLength;

            byte shiftRegister;
            byte bitCount;
            public ushort addressCounter;
            public ushort lengthCounter;
            byte counter;
            byte timerCounter;

            public byte dac;

            public void Resume()
            {
                if (lengthCounter == 0)
                {
                    addressCounter = sampleAddress;
                    lengthCounter = sampleLength;
                }
            }

            public void ClockTimer(Apu apu)
            {
                if (enabled)
                    ReadByte(apu);

                if (timerCounter == 0)
                {
                    timerCounter = DMC_FREQUENCY_PERIODS[frequency];

                    dac = 0;
                    if (bitCount > 0)
                    {
                        if ((shiftRegister & 1) == 1)
                        {
                            if (counter <= 125)
                                counter += 2;
                        }
                        else
                        {
                            if (counter > 2)
                            {
                                counter -= 2;
                            }
                        }
                        shiftRegister >>= 1;
                        --bitCount;

                        dac = counter;
                    }
                }
                --timerCounter;
            }

            void ReadByte(Apu apu)
            {
                shiftRegister = apu.bus.Read(addressCounter);
                bitCount = 8;
                apu.bus.stallCycles += 4;
                ++addressCounter;
                if (addressCounter == 0)
                    addressCounter = 0x8000;
                --lengthCounter;

                if (lengthCounter == 0 && irqEnable)
                    apu.status |= ApuStatus.DmcIrq;

                if (lengthCounter == 0 && loop)
                {
                    addressCounter = sampleAddress;
                    lengthCounter = sampleLength;
                }
            }

            public DmcWriteResult Write(int offset, byte data, bool isEnabled)
            {
                switch (offset)
                {
                    case 0:
                        frequency = (byte)(data & 0x0F);
                        loop = (data & 0x40) != 0;
                        irqEnable = (data & 0x80) != 0;
                        if (!irqEnable)
                        {
                            return DmcWriteResult.ClearIrq;
                        }
                        break;

                    case 1:
                        counter = (byte)(data & 0x7F);
                        break;

                    case 2:
                        sampleAddress = (ushort)(0xC000 | (data << 6));
                        break;

                    case 3:
                        sampleLength = (ushort)((data << 4) | 1);
                        break;
                }
                return DmcWriteResult.None;
            }

            public enum DmcWriteResult
            {
                None,
                ClearIrq
            }
        }

        static readonly byte[] LENGTH_TABLE = new byte[]
        {
            0x0A, 0xFE, 0x14, 0x02, 0x28, 0x04, 0x50, 0x06,
            0xA0, 0x08, 0x3C, 0x0A, 0x0E, 0x0C, 0x1A, 0x0E,
            0x0C, 0x10, 0x18, 0x12, 0x30, 0x14, 0x60, 0x16,
            0xC0, 0x18, 0x48, 0x1A, 0x10, 0x1C, 0x20, 0x1E
        };

        static readonly ushort[] TIMER_PERIODS = new ushort[]
        {
            0x004, 0x008, 0x010, 0x020, 0x040, 0x060, 0x080, 0x0A0,
            0x0CA, 0x0FE, 0x17C, 0x1FC, 0x2FA, 0x3F8, 0x7F2, 0xFE4
        };

        static readonly byte[][] DUTY_SEQUENCES = new byte[][]
        {
            new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 },
            new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 },
        };

        static readonly byte[] TRIANGLE_SEQUENCE = new byte[]
        {
            0xF, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0x7, 0x6, 0x5, 0x4, 0x3, 0x2, 0x1, 0x0,
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF
        };

        static readonly byte[] DMC_FREQUENCY_PERIODS = new byte[]
        {
            214, 190, 170, 160, 143, 127, 113, 107,
            95, 80, 71, 64, 53, 42, 36, 27
        };

        static readonly ushort[] DMC_FREQUENCY = new ushort[]
        {
            0x1AC, 0x17C, 0x154, 0x140, 0x11E, 0x0FE, 0x0E2, 0x0D6,
            0x0BE, 0x0A0, 0x08E, 0x080, 0x06A, 0x054, 0x048, 0x036
        };
    }
}
