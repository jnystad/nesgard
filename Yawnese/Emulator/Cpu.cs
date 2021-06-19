using System;
using System.IO;
using System.Threading;

namespace Yawnese.Emulator
{
    public partial class Cpu
    {
        StreamWriter traceFile;

        public Bus bus;

        public Registers registers;

        public class Registers
        {
            public ushort pc;
            public byte sp;
            public byte acc;
            public byte x;
            public byte y;
            public CpuStatus status;

            public void Reset()
            {
                pc = 0;
                sp = 0xFD;
                acc = 0;
                x = 0;
                y = 0;
                status = CpuStatus.Break | CpuStatus.InterruptDisable;
            }
        }

        public ulong cycles;

        public bool testMode;


        public enum Mode
        {
            Accumulator = 0,
            Immediate = 1,
            Implied = 2,
            Relative = 3,
            Absolute = 4,
            AbsoluteX = 5,
            AbsoluteXAlwaysTick = 25,
            AbsoluteY = 6,
            AbsoluteYAlwaysTick = 26,
            ZeroPage = 7,
            ZeroPageX = 8,
            ZeroPageY = 9,
            Indirect = 10,
            IndirectX = 11,
            IndirectY = 12,
            IndirectYAlwaysTick = 32,
            NotAvailable = 99,
        }

        public Cpu(Cartridge rom)
        {
            bus = new Bus(rom);
            registers = new Registers();

            if (File.Exists("cpu_trace.log"))
                File.Delete("cpu_trace.log");
            traceFile = new StreamWriter(File.OpenWrite("cpu_trace.log"));
        }

        public void Reset(bool testMode)
        {
            registers.Reset();
            registers.pc = testMode ? (ushort)0xC000 : ReadWord(0xFFFC);

            this.testMode = testMode;

            cycles = 0;

            bus.Reset();

            Tick();
            Tick();
            Tick();
            Tick();
            Tick();
            Tick();
            Tick();
        }

        public void Run()
        {
            while (true)
            {
                if (bus.PollNMI())
                {
                    PushWord(registers.pc);
                    Push((byte)registers.status);
                    registers.pc = ReadWord(0xFFFA);
                    registers.status |= CpuStatus.InterruptDisable;
                }

                if (cycles < 200000)
                    LogTrace();

                if (testMode && cycles > 26600)
                    throw new Exception("Test failed, too long");

                while (bus.stallCycles > 0)
                {
                    Tick();
                    bus.stallCycles--;
                }

                Tick();

                var opcode = Next();
                switch (opcode)
                {
                    case 0x00: brk(); break;

                    case 0x10: BranchFlag(CpuStatus.Negative, false); break;
                    case 0x30: BranchFlag(CpuStatus.Negative, true); break;
                    case 0x50: BranchFlag(CpuStatus.Overflow, false); break;
                    case 0x70: BranchFlag(CpuStatus.Overflow, true); break;
                    case 0x90: BranchFlag(CpuStatus.Carry, false); break;
                    case 0xB0: BranchFlag(CpuStatus.Carry, true); break;
                    case 0xD0: BranchFlag(CpuStatus.Zero, false); break;
                    case 0xF0: BranchFlag(CpuStatus.Zero, true); break;

                    case 0x18:
                        {
                            Tick();
                            SetFlag(CpuStatus.Carry, false); break;
                        }
                    case 0x38:
                        {
                            Tick();
                            SetFlag(CpuStatus.Carry, true); break;
                        }
                    case 0x58:
                        {
                            Tick();
                            SetFlag(CpuStatus.InterruptDisable, false); break;
                        }
                    case 0x78:
                        {
                            Tick();
                            SetFlag(CpuStatus.InterruptDisable, true); break;
                        }
                    case 0xB8:
                        {
                            Tick();
                            SetFlag(CpuStatus.Overflow, false); break;
                        }
                    case 0xD8:
                        {
                            Tick();
                            SetFlag(CpuStatus.DecimalMode, false); break;
                        }
                    case 0xF8:
                        {
                            Tick();
                            SetFlag(CpuStatus.DecimalMode, true); break;
                        }

                    case 0x20: jsr(); break;
                    case 0x4C: jmp(Mode.Absolute); break;
                    case 0x6C: jmp(Mode.Indirect); break;
                    case 0x40: rti(); break;
                    case 0x60:
                        {
                            rts();
                            if (registers.sp == 0xFF && testMode)
                            {
                                throw new Exception("Test complete");
                            }
                            break;
                        }

                    case 0x21: and(Mode.IndirectX); break;
                    case 0x25: and(Mode.ZeroPage); break;
                    case 0x29: and(Mode.Immediate); break;
                    case 0x2D: and(Mode.Absolute); break;
                    case 0x31: and(Mode.IndirectY); break;
                    case 0x35: and(Mode.ZeroPageX); break;
                    case 0x39: and(Mode.AbsoluteY); break;
                    case 0x3D: and(Mode.AbsoluteX); break;

                    case 0xC1: cmp(Mode.IndirectX); break;
                    case 0xC5: cmp(Mode.ZeroPage); break;
                    case 0xC9: cmp(Mode.Immediate); break;
                    case 0xCD: cmp(Mode.Absolute); break;
                    case 0xD1: cmp(Mode.IndirectY); break;
                    case 0xD5: cmp(Mode.ZeroPageX); break;
                    case 0xD9: cmp(Mode.AbsoluteY); break;
                    case 0xDD: cmp(Mode.AbsoluteX); break;

                    case 0xC0: cpy(Mode.Immediate); break;
                    case 0xC4: cpy(Mode.ZeroPage); break;
                    case 0xCC: cpy(Mode.Absolute); break;

                    case 0xE0: cpx(Mode.Immediate); break;
                    case 0xE4: cpx(Mode.ZeroPage); break;
                    case 0xEC: cpx(Mode.Absolute); break;

                    case 0x41: eor(Mode.IndirectX); break;
                    case 0x45: eor(Mode.ZeroPage); break;
                    case 0x49: eor(Mode.Immediate); break;
                    case 0x4D: eor(Mode.Absolute); break;
                    case 0x51: eor(Mode.IndirectY); break;
                    case 0x55: eor(Mode.ZeroPageX); break;
                    case 0x59: eor(Mode.AbsoluteY); break;
                    case 0x5D: eor(Mode.AbsoluteX); break;

                    case 0x61: adc(Mode.IndirectX); break;
                    case 0x65: adc(Mode.ZeroPage); break;
                    case 0x69: adc(Mode.Immediate); break;
                    case 0x6D: adc(Mode.Absolute); break;
                    case 0x71: adc(Mode.IndirectY); break;
                    case 0x75: adc(Mode.ZeroPageX); break;
                    case 0x79: adc(Mode.AbsoluteY); break;
                    case 0x7D: adc(Mode.AbsoluteX); break;

                    case 0xE1: sbc(Mode.IndirectX); break;
                    case 0xE5: sbc(Mode.ZeroPage); break;
                    case 0xE9: sbc(Mode.Immediate); break;
                    case 0xEB: sbc(Mode.Immediate); break;
                    case 0xED: sbc(Mode.Absolute); break;
                    case 0xF1: sbc(Mode.IndirectY); break;
                    case 0xF5: sbc(Mode.ZeroPageX); break;
                    case 0xF9: sbc(Mode.AbsoluteY); break;
                    case 0xFD: sbc(Mode.AbsoluteX); break;

                    case 0x24: bit(Mode.ZeroPage); break;
                    case 0x2C: bit(Mode.Absolute); break;

                    case 0x01: ora(Mode.IndirectX); break;
                    case 0x05: ora(Mode.ZeroPage); break;
                    case 0x09: ora(Mode.Immediate); break;
                    case 0x0D: ora(Mode.Absolute); break;
                    case 0x11: ora(Mode.IndirectY); break;
                    case 0x15: ora(Mode.ZeroPageX); break;
                    case 0x19: ora(Mode.AbsoluteY); break;
                    case 0x1D: ora(Mode.AbsoluteX); break;

                    case 0x0A: asl_a(); break;
                    case 0x06: asl(Mode.ZeroPage); break;
                    case 0x16: asl(Mode.ZeroPageX); break;
                    case 0x0E: asl(Mode.Absolute); break;
                    case 0x1E: asl(Mode.AbsoluteXAlwaysTick); break;

                    case 0x07: slo(Mode.ZeroPage); break;
                    case 0x17: slo(Mode.ZeroPageX); break;
                    case 0x0F: slo(Mode.Absolute); break;
                    case 0x1F: slo(Mode.AbsoluteX); break;
                    case 0x1B: slo(Mode.AbsoluteY); break;
                    case 0x03: slo(Mode.IndirectX); break;
                    case 0x13: slo(Mode.IndirectY); break;

                    case 0x4A: lsr_a(); break;
                    case 0x46: lsr(Mode.ZeroPage); break;
                    case 0x56: lsr(Mode.ZeroPageX); break;
                    case 0x4E: lsr(Mode.Absolute); break;
                    case 0x5E: lsr(Mode.AbsoluteXAlwaysTick); break;

                    case 0x47: sre(Mode.ZeroPage); break;
                    case 0x57: sre(Mode.ZeroPageX); break;
                    case 0x4F: sre(Mode.Absolute); break;
                    case 0x5F: sre(Mode.AbsoluteX); break;
                    case 0x5B: sre(Mode.AbsoluteX); break;
                    case 0x43: sre(Mode.IndirectX); break;
                    case 0x53: sre(Mode.IndirectY); break;

                    case 0x2A: rol_a(); break;
                    case 0x26: rol(Mode.ZeroPage); break;
                    case 0x36: rol(Mode.ZeroPageX); break;
                    case 0x2E: rol(Mode.Absolute); break;
                    case 0x3E: rol(Mode.AbsoluteXAlwaysTick); break;

                    case 0x27: rla(Mode.ZeroPage); break;
                    case 0x37: rla(Mode.ZeroPageX); break;
                    case 0x2F: rla(Mode.Absolute); break;
                    case 0x3F: rla(Mode.AbsoluteX); break;
                    case 0x3B: rla(Mode.AbsoluteY); break;
                    case 0x23: rla(Mode.IndirectX); break;
                    case 0x33: rla(Mode.IndirectY); break;

                    case 0x6A: ror_a(); break;
                    case 0x66: ror(Mode.ZeroPage); break;
                    case 0x76: ror(Mode.ZeroPageX); break;
                    case 0x6E: ror(Mode.Absolute); break;
                    case 0x7E: ror(Mode.AbsoluteXAlwaysTick); break;

                    case 0x67: rra(Mode.ZeroPage); break;
                    case 0x77: rra(Mode.ZeroPageX); break;
                    case 0x6F: rra(Mode.Absolute); break;
                    case 0x7F: rra(Mode.AbsoluteX); break;
                    case 0x7B: rra(Mode.AbsoluteY); break;
                    case 0x63: rra(Mode.IndirectX); break;
                    case 0x73: rra(Mode.IndirectY); break;

                    case 0x85: sta(Mode.ZeroPage); break;
                    case 0x95: sta(Mode.ZeroPageX); break;
                    case 0x8D: sta(Mode.Absolute); break;
                    case 0x9D: sta(Mode.AbsoluteXAlwaysTick); break;
                    case 0x99: sta(Mode.AbsoluteYAlwaysTick); break;
                    case 0x81: sta(Mode.IndirectX); break;
                    case 0x91: sta(Mode.IndirectYAlwaysTick); break;

                    case 0x86: stx(Mode.ZeroPage); break;
                    case 0x96: stx(Mode.ZeroPageY); break;
                    case 0x8E: stx(Mode.Absolute); break;

                    case 0x84: sty(Mode.ZeroPage); break;
                    case 0x94: sty(Mode.ZeroPageX); break;
                    case 0x8C: sty(Mode.Absolute); break;

                    case 0x87: sax(Mode.ZeroPage); break;
                    case 0x97: sax(Mode.ZeroPageY); break;
                    case 0x8F: sax(Mode.Absolute); break;
                    case 0x83: sax(Mode.IndirectX); break;

                    case 0xA9: lda(Mode.Immediate); break;
                    case 0xA5: lda(Mode.ZeroPage); break;
                    case 0xB5: lda(Mode.ZeroPageX); break;
                    case 0xAD: lda(Mode.Absolute); break;
                    case 0xBD: lda(Mode.AbsoluteX); break;
                    case 0xB9: lda(Mode.AbsoluteY); break;
                    case 0xA1: lda(Mode.IndirectX); break;
                    case 0xB1: lda(Mode.IndirectY); break;

                    case 0xA2: ldx(Mode.Immediate); break;
                    case 0xA6: ldx(Mode.ZeroPage); break;
                    case 0xB6: ldx(Mode.ZeroPageY); break;
                    case 0xAE: ldx(Mode.Absolute); break;
                    case 0xBE: ldx(Mode.AbsoluteY); break;

                    case 0xA0: ldy(Mode.Immediate); break;
                    case 0xA4: ldy(Mode.ZeroPage); break;
                    case 0xB4: ldy(Mode.ZeroPageX); break;
                    case 0xAC: ldy(Mode.Absolute); break;
                    case 0xBC: ldy(Mode.AbsoluteX); break;

                    case 0xAB: lax(Mode.Immediate); break;
                    case 0xA7: lax(Mode.ZeroPage); break;
                    case 0xB7: lax(Mode.ZeroPageY); break;
                    case 0xAF: lax(Mode.Absolute); break;
                    case 0xBF: lax(Mode.AbsoluteY); break;
                    case 0xA3: lax(Mode.IndirectX); break;
                    case 0xB3: lax(Mode.IndirectY); break;

                    case 0xE6: inc(Mode.ZeroPage); break;
                    case 0xF6: inc(Mode.ZeroPageX); break;
                    case 0xEE: inc(Mode.Absolute); break;
                    case 0xFE: inc(Mode.AbsoluteXAlwaysTick); break;

                    case 0xC6: dec(Mode.ZeroPage); break;
                    case 0xD6: dec(Mode.ZeroPageX); break;
                    case 0xCE: dec(Mode.Absolute); break;
                    case 0xDE: dec(Mode.AbsoluteXAlwaysTick); break;

                    case 0xE7: isb(Mode.ZeroPage); break;
                    case 0xF7: isb(Mode.ZeroPageX); break;
                    case 0xEF: isb(Mode.Absolute); break;
                    case 0xFF: isb(Mode.AbsoluteX); break;
                    case 0xFB: isb(Mode.AbsoluteY); break;
                    case 0xE3: isb(Mode.IndirectX); break;
                    case 0xF3: isb(Mode.IndirectY); break;

                    case 0xC7: dcp(Mode.ZeroPage); break;
                    case 0xD7: dcp(Mode.ZeroPageX); break;
                    case 0xCF: dcp(Mode.Absolute); break;
                    case 0xDF: dcp(Mode.AbsoluteX); break;
                    case 0xDB: dcp(Mode.AbsoluteY); break;
                    case 0xC3: dcp(Mode.IndirectX); break;
                    case 0xD3: dcp(Mode.IndirectY); break;

                    case 0x08: php(); break;
                    case 0x28: plp(); break;
                    case 0x48: pha(); break;
                    case 0x68: pla(); break;
                    case 0x9A: txs(); break;
                    case 0xBA: tsx(); break;
                    case 0x8A: txa(); break;
                    case 0xAA: tax(); break;
                    case 0xCA: dex(); break;
                    case 0xE8: inx(); break;
                    case 0x98: tya(); break;
                    case 0x88: dey(); break;
                    case 0xA8: tay(); break;
                    case 0xC8: iny(); break;

                    case 0xEA: nop(Mode.Implied); break;
                    case 0x1A: nop(Mode.Implied); break;
                    case 0x3A: nop(Mode.Implied); break;
                    case 0x5A: nop(Mode.Implied); break;
                    case 0x7A: nop(Mode.Implied); break;
                    case 0xDA: nop(Mode.Implied); break;
                    case 0xFA: nop(Mode.Implied); break;
                    case 0x80: nop(Mode.Immediate); break;
                    case 0x82: nop(Mode.Immediate); break;
                    case 0x89: nop(Mode.Immediate); break;
                    case 0xC2: nop(Mode.Immediate); break;
                    case 0xE2: nop(Mode.Immediate); break;
                    case 0x04: nop(Mode.ZeroPage); break;
                    case 0x44: nop(Mode.ZeroPage); break;
                    case 0x64: nop(Mode.ZeroPage); break;
                    case 0x14: nop(Mode.ZeroPageX); break;
                    case 0x34: nop(Mode.ZeroPageX); break;
                    case 0x54: nop(Mode.ZeroPageX); break;
                    case 0x74: nop(Mode.ZeroPageX); break;
                    case 0xD4: nop(Mode.ZeroPageX); break;
                    case 0xF4: nop(Mode.ZeroPageX); break;
                    case 0x0C: nop(Mode.Absolute); break;
                    case 0x1C: nop(Mode.AbsoluteX); break;
                    case 0x3C: nop(Mode.AbsoluteX); break;
                    case 0x5C: nop(Mode.AbsoluteX); break;
                    case 0x7C: nop(Mode.AbsoluteX); break;
                    case 0xDC: nop(Mode.AbsoluteX); break;
                    case 0xFC: nop(Mode.AbsoluteX); break;

                    default:
                        throw new NotImplementedException();
                }

                if (bus.endOfFrame)
                {
                    bus.endOfFrame = false;
                    break;
                }
            }
        }

        void Tick()
        {
            cycles += 1;
            bus.Tick();
        }

        // Memory

        byte Read(ushort addr)
        {
            return bus.Read(addr);
        }

        ushort ReadWord(ushort addr)
        {
            var lsb = (ushort)Read(addr);
            var msb = (ushort)Read((ushort)(addr + 1));
            return (ushort)((msb << 8) | lsb);
        }

        ushort ReadWordZeroPage(byte addr)
        {
            var lsb = (ushort)Read(addr);
            var msb = (ushort)Read((byte)(addr + 1));
            return (ushort)((msb << 8) | lsb);
        }

        void Write(ushort addr, byte data)
        {
            Tick();
            bus.Write(addr, data);
        }

        byte Next()
        {
            var pc = registers.pc;
            registers.pc += 1;
            return Read(pc);
        }

        ushort NextWord()
        {
            var pc = registers.pc;
            registers.pc += 2;
            return ReadWord(pc);
        }

        ushort Address(Mode mode)
        {
            switch (mode)
            {
                case Mode.Absolute:
                    {
                        Tick();
                        Tick();
                        return NextWord();
                    }
                case Mode.AbsoluteX:
                case Mode.AbsoluteXAlwaysTick:
                    {
                        Tick();
                        Tick();
                        var offset = NextWord();
                        if (mode == Mode.AbsoluteXAlwaysTick || CrossBoundary(offset, registers.x))
                        {
                            Tick();
                        }
                        return (ushort)(((ushort)registers.x) + offset);
                    }
                case Mode.AbsoluteY:
                case Mode.AbsoluteYAlwaysTick:
                    {
                        Tick();
                        Tick();
                        var offset = NextWord();
                        if (mode == Mode.AbsoluteYAlwaysTick || CrossBoundary(offset, registers.y))
                        {
                            Tick();
                        }
                        return (ushort)(((ushort)registers.y) + offset);
                    }
                case Mode.Immediate:
                    {
                        var a = registers.pc;
                        registers.pc += 1;
                        return a;
                    }
                case Mode.Indirect:
                    {
                        var b1 = Next();
                        var b2 = Next();
                        Tick();
                        Tick();
                        Tick();
                        Tick();
                        var a1 = (ushort)((b2 << 8) | b1);
                        var a2 = (ushort)((b2 << 8) | (byte)(b1 + 1));
                        return (ushort)(Read(a2) << 8 | Read(a1));
                    }
                case Mode.IndirectX:
                    {
                        var b = Next();
                        Tick();
                        var r = (byte)(registers.x + b);
                        Tick();
                        Tick();
                        Tick();
                        return ReadWordZeroPage(r);
                    }
                case Mode.IndirectY:
                case Mode.IndirectYAlwaysTick:
                    {
                        var b = Next();
                        Tick();
                        var r = ReadWordZeroPage(b);
                        Tick();
                        Tick();
                        if (mode == Mode.IndirectYAlwaysTick || CrossBoundary(r, registers.y))
                        {
                            Tick();
                        }
                        return (ushort)(r + registers.y);
                    }
                case Mode.ZeroPage:
                    {
                        Tick();
                        return Next();
                    }
                case Mode.ZeroPageX:
                    {
                        Tick();
                        Tick();
                        return (byte)(Next() + registers.x);
                    }
                case Mode.ZeroPageY:
                    {
                        Tick();
                        Tick();
                        return (byte)(Next() + registers.y);
                    }
                default:
                    throw new NotImplementedException("Missing address mode");
            }
        }

        // Stack

        void Push(byte value)
        {
            var addr = (ushort)(0x100 + registers.sp);
            Write(addr, value);
            //trace!("Push {0:X2}}", value);
            if (registers.sp == 0)
            {
                registers.sp = 0xFF;
            }
            else
            {
                registers.sp -= 1;
            }
        }

        void PushWord(ushort value)
        {
            Push((byte)(value >> 8));
            Push((byte)value);
        }

        byte Pop()
        {
            if (registers.sp == 0xFF)
            {
                registers.sp = 0;
            }
            else
            {
                registers.sp += 1;
            }
            Tick();
            var addr = (ushort)(0x100 + registers.sp);
            var result = Read(addr);
            //trace!("pop  {0:X2}}", result);
            return result;
        }

        ushort PopWord()
        {
            return (ushort)(Pop() | (Pop() << 8));
        }

        // Utils

        bool GetFlag(CpuStatus flag)
        {
            return registers.status.HasFlag(flag);
        }

        void SetFlag(CpuStatus flag, bool value)
        {
            if (value)
                registers.status |= flag;
            else
                registers.status &= ~flag;
        }

        void SetZeroNegativeFlags(byte value)
        {
            SetFlag(CpuStatus.Zero, value == 0);
            SetFlag(CpuStatus.Negative, (value & 0b10000000) != 0);
        }

        byte Carry()
        {
            return registers.status.HasFlag(CpuStatus.Carry) ? (byte)CpuStatus.Carry : (byte)0;
        }

        byte ReadOperand(Mode mode)
        {
            var addr = Address(mode);
            return Read(addr);
        }

        void AddSubCarry(ushort a, ushort b)
        {
            var c = (ushort)Carry();
            //info!("AddSubCarry {} {} {}", b, a, c);
            var r = a + b + c;
            SetZeroNegativeFlags((byte)r);
            SetFlag(CpuStatus.Carry, r > 0xFF);
            SetFlag(CpuStatus.Overflow, ((~(a ^ b)) & (b ^ r) & 0x80) != 0);
            registers.acc = (byte)r;
            Tick();
        }

        // Branching

        void BranchFlag(CpuStatus flag, bool value)
        {
            var rel = Next();
            Tick();
            if (GetFlag(flag) == value)
            {
                Tick();
                var pc = registers.pc;
                if ((rel & 0x80) == 0)
                {
                    registers.pc += rel;
                }
                else
                {
                    registers.pc = (ushort)(pc + (rel & 0x7F) - 128);
                }
                if ((registers.pc & 0xFF00) != (pc & 0xFF00))
                {
                    Tick();
                }
            }
        }

        // Operations

        void adc(Mode mode)
        {
            var a = registers.acc;
            var b = ReadOperand(mode);
            AddSubCarry(a, b);
        }

        void asl(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand << 1);
            Tick();
            SetFlag(CpuStatus.Carry, (operand & 0x80) != 0);
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void asl_a()
        {
            var result = (byte)(registers.acc << 1);
            SetFlag(CpuStatus.Carry, (registers.acc & 0x80) != 0);
            SetZeroNegativeFlags(result);
            registers.acc = result;
            Tick();
        }

        void and(Mode mode)
        {
            var operand = ReadOperand(mode);
            registers.acc &= operand;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void bit(Mode mode)
        {
            var operand = ReadOperand(mode);
            var result = operand & registers.acc;
            SetFlag(CpuStatus.Zero, result == 0);
            SetFlag(CpuStatus.Negative, (operand & 0b10000000) != 0);
            SetFlag(CpuStatus.Overflow, (operand & 0b01000000) != 0);
            Tick();
        }

        void brk()
        {
            registers.pc += 1;
            // interrupt();
            SetFlag(CpuStatus.InterruptDisable, true);

            var status = (byte)registers.status | (byte)CpuStatus.Break;
            PushWord(registers.pc);
            Push((byte)status);

            SetFlag(CpuStatus.InterruptDisable, true);
            registers.pc = ReadWord(0xFFFE);
        }

        void cmp(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetFlag(CpuStatus.Carry, registers.acc >= operand);
            SetZeroNegativeFlags((byte)(registers.acc - operand));
            Tick();
        }

        void cpx(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetFlag(CpuStatus.Carry, registers.x >= operand);
            SetZeroNegativeFlags((byte)(registers.x - operand));
            Tick();
        }

        void cpy(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetFlag(CpuStatus.Carry, registers.y >= operand);
            SetZeroNegativeFlags((byte)(registers.y - operand));
            Tick();
        }

        void dcp(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand - 1);
            Tick();
            SetFlag(CpuStatus.Carry, registers.acc >= result);
            SetZeroNegativeFlags((byte)(registers.acc - result));
            Write(addr, result);
            Tick();
        }

        void dec(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand - 1);
            Tick();
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void dex()
        {
            registers.x = (byte)(registers.x - 1);
            SetZeroNegativeFlags(registers.x);
            Tick();
        }

        void dey()
        {
            registers.y = (byte)(registers.y - 1);
            SetZeroNegativeFlags(registers.y);
            Tick();
        }

        void eor(Mode mode)
        {
            var operand = ReadOperand(mode);
            registers.acc ^= operand;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void inc(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand + 1);
            Tick();
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void inx()
        {
            registers.x = (byte)(registers.x + 1);
            SetZeroNegativeFlags(registers.x);
            Tick();
        }

        void iny()
        {
            registers.y = (byte)(registers.y + 1);
            SetZeroNegativeFlags(registers.y);
            Tick();
        }

        void isb(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand + 1);
            AddSubCarry(registers.acc, (byte)(~result));
            Write(addr, result);
            Tick();
        }

        void jmp(Mode mode)
        {
            var addr = Address(mode);
            registers.pc = addr;
        }

        void jsr()
        {
            Tick();
            var addr = Address(Mode.Absolute);
            PushWord((ushort)(registers.pc - 1));
            registers.pc = addr;
        }

        void lax(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetZeroNegativeFlags(operand);
            registers.acc = operand;
            registers.x = operand;
            Tick();
        }

        void lda(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetZeroNegativeFlags(operand);
            registers.acc = operand;
            Tick();
        }

        void ldx(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetZeroNegativeFlags(operand);
            registers.x = operand;
            Tick();
        }

        void ldy(Mode mode)
        {
            var operand = ReadOperand(mode);
            SetZeroNegativeFlags(operand);
            registers.y = operand;
            Tick();
        }

        void lsr(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand >> 1);
            Tick();
            SetFlag(CpuStatus.Carry, (operand & 1) != 0);
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void lsr_a()
        {
            var result = (byte)(registers.acc >> 1);
            SetFlag(CpuStatus.Carry, (registers.acc & 1) != 0);
            SetZeroNegativeFlags(result);
            registers.acc = result;
            Tick();
        }

        void nop(Mode mode)
        {
            if (mode != Mode.Implied)
            {
                ReadOperand(mode);
            }
            Tick();
        }

        void ora(Mode mode)
        {
            var operand = ReadOperand(mode);
            var result = (byte)(operand | registers.acc);
            SetZeroNegativeFlags(result);
            registers.acc = result;
            Tick();
        }

        void pha()
        {
            Push(registers.acc);
            Tick();
        }

        void php()
        {
            Push((byte)(registers.status | CpuStatus.Push | CpuStatus.Break));
            Tick();
        }

        void pla()
        {
            Tick();
            registers.acc = Pop();
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void plp()
        {
            Tick();
            registers.status = (CpuStatus)(Pop() & ~((byte)CpuStatus.Push) | (byte)CpuStatus.Break);
            Tick();
        }

        void rla(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand << 1 | Carry());
            SetFlag(CpuStatus.Carry, (operand & 0x80) != 0);
            Write(addr, result);
            Tick();
            registers.acc &= result;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void rol(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand << 1 | Carry());
            Tick();
            SetFlag(CpuStatus.Carry, (operand & 0x80) != 0);
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void rol_a()
        {
            var result = (byte)(registers.acc << 1 | Carry());
            SetFlag(CpuStatus.Carry, (registers.acc & 0x80) != 0);
            SetZeroNegativeFlags(result);
            registers.acc = result;
            Tick();
        }

        void ror(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand >> 1 | (Carry() << 7));
            Tick();
            SetFlag(CpuStatus.Carry, (operand & 0x01) != 0);
            SetZeroNegativeFlags(result);
            Write(addr, result);
            Tick();
        }

        void ror_a()
        {
            var result = (byte)(registers.acc >> 1 | (Carry() << 7));
            SetFlag(CpuStatus.Carry, (registers.acc & 0x01) != 0);
            SetZeroNegativeFlags(result);
            registers.acc = result;
            Tick();
        }

        void rra(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand >> 1 | (Carry() << 7));
            SetFlag(CpuStatus.Carry, (operand & 0x01) != 0);
            Write(addr, result);
            Tick();
            var a = registers.acc;
            AddSubCarry(a, result);
        }

        void rti()
        {
            registers.status = (CpuStatus)Pop() | CpuStatus.Break;
            Tick();
            registers.pc = PopWord();
            Tick();
        }

        void rts()
        {
            Tick();
            Tick();
            registers.pc = (ushort)(PopWord() + 1);
            Tick();
        }

        void sax(Mode mode)
        {
            var addr = Address(mode);
            Write(addr, (byte)(registers.x & registers.acc));
        }

        void sbc(Mode mode)
        {
            var b = (byte)(~ReadOperand(mode));
            var a = registers.acc;
            AddSubCarry(a, b);
        }

        void slo(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand << 1);
            SetFlag(CpuStatus.Carry, (operand & 0x80) != 0);
            Write(addr, result);
            Tick();
            var result2 = (byte)(result | registers.acc);
            SetZeroNegativeFlags(result2);
            registers.acc = result2;
            Tick();
        }

        void sre(Mode mode)
        {
            var addr = Address(mode);
            var operand = Read(addr);
            var result = (byte)(operand >> 1);
            SetFlag(CpuStatus.Carry, (operand & 1) != 0);
            Write(addr, result);
            Tick();
            registers.acc ^= result;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void sta(Mode mode)
        {
            var addr = Address(mode);
            Write(addr, registers.acc);
        }

        void stx(Mode mode)
        {
            var addr = Address(mode);
            Write(addr, registers.x);
        }

        void sty(Mode mode)
        {
            var addr = Address(mode);
            Write(addr, registers.y);
        }

        void tax()
        {
            registers.x = registers.acc;
            SetZeroNegativeFlags(registers.x);
            Tick();
        }

        void tay()
        {
            registers.y = registers.acc;
            SetZeroNegativeFlags(registers.y);
            Tick();
        }

        void tsx()
        {
            registers.x = registers.sp;
            SetZeroNegativeFlags(registers.x);
            Tick();
        }

        void txa()
        {
            registers.acc = registers.x;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }

        void txs()
        {
            registers.sp = registers.x;
            Tick();
        }

        void tya()
        {
            registers.acc = registers.y;
            SetZeroNegativeFlags(registers.acc);
            Tick();
        }
        bool CrossBoundary(ushort addr, byte offset)
        {
            return (ushort)(addr & 0xFF00) != (ushort)((addr + offset) & 0xFF00);
        }

        // Debug

        byte DebugReadMem(ushort addr)
        {
            switch (addr)
            {
                case ushort n when (n <= 0x1FFF):
                    return bus.ram[addr % 0x0800];

                case 0x2000:
                case 0x2001:
                case 0x2003:
                case 0x2005:
                case 0x2006: return 0;
                case 0x4014: return 0;

                case ushort n when (n >= 0x8000):
                    return bus.mapper.PrgRead(addr);

                default: return 0;
            }
        }

        bool IsIllegalInstruction(byte opcode)
        {
            if ((opcode & 0x3) == 0x3) return true;
            switch (opcode & 0xF)
            {
                case 0x0:
                    if (opcode == 0x80) return true;
                    return false;
                case 0x2:
                    if (opcode == 0xA2) return false;
                    return true;
                case 0x4:
                    switch (opcode)
                    {
                        case 0x24:
                        case 0x84:
                        case 0x94:
                        case 0xA4:
                        case 0xB4:
                        case 0xC4:
                        case 0xE4:
                            return false;
                    }
                    return true;
                case 0x9:
                    if (opcode == 0x89) return true;
                    return false;
                case 0xA:
                    switch (opcode)
                    {
                        case 0x1A:
                        case 0x3A:
                        case 0x5A:
                        case 0x7A:
                        case 0xDA:
                        case 0xFA:
                            return true;
                    }
                    return false;
                case 0xC:
                    switch (opcode)
                    {
                        case 0x0C:
                        case 0x1C:
                        case 0x3C:
                        case 0x5C:
                        case 0x7C:
                        case 0x9C:
                        case 0xDC:
                        case 0xFC:
                            return true;
                    }
                    return false;
                case 0xE:
                    if (opcode == 0x9E) return true;
                    return false;
            }
            return false;
        }


        void LogTrace()
        {
            var opcode = DebugReadMem(registers.pc);
            var a = DebugReadMem((ushort)(registers.pc + 1));
            var b = DebugReadMem((ushort)(registers.pc + 2));
            var instr_size = INSTRUCTION_SIZES[opcode];
            var instr_name = INSTRUCTION_NAMES[opcode];
            var instr_ilgl = IsIllegalInstruction(opcode) ? "*" : " ";
            var instr = "";
            switch (instr_size)
            {
                case 1:
                    instr = string.Format("{0:X2}      ", opcode);
                    break;
                case 2:
                    instr = string.Format("{0:X2} {1:X2}   ", opcode, a);
                    break;
                case 3:
                    instr = string.Format("{0:X2} {1:X2} {2:X2}", opcode, a, b);
                    break;
            }
            var mode = INSTRUCTION_MODES[opcode];
            var mem = "";
            switch (mode)
            {
                case Mode.Accumulator:
                    mem = "A";
                    break;
                case Mode.Immediate:
                    mem = string.Format("#${0:X2}", a);
                    break;
                case Mode.Implied:
                    mem = string.Format("$({0:X2}{1:X2})", b, a);
                    break;
                case Mode.Relative:
                    mem = string.Format(
                        "${0:X4}",
                        (a & 0x80) == 0 ? instr_size + registers.pc + a : instr_size + registers.pc - 128 + (a & 0x7F)
                    );
                    break;
                case Mode.Absolute:
                    if (instr_name == "JMP" || instr_name == "JSR")
                    {
                        mem = string.Format("${0:X2}{1:X2}", b, a);
                    }
                    else
                    {
                        mem = string.Format(
                            "${0:X2}{1:X2} = {2:X2}",
                            b,
                            a,
                            DebugReadMem((ushort)((b << 8) | a))
                        );
                    }
                    break;
                case Mode.AbsoluteX:
                    {
                        var addr = (ushort)(registers.x + ((b << 8) | a));
                        var v = DebugReadMem(addr);
                        mem = string.Format("${0:X2}{1:X2},X @ {2:X4} = {3:X2}", b, a, addr, v);
                        break;
                    }
                case Mode.AbsoluteY:
                    {
                        var addr = (ushort)(registers.y + ((b << 8) | a));
                        var v = DebugReadMem(addr);
                        mem = string.Format("${0:X2}{1:X2},Y @ {2:X4} = {3:X2}", b, a, addr, v);
                        break;
                    }
                case Mode.ZeroPage:
                    mem = string.Format("${0:X2} = {1:X2}", a, DebugReadMem(a));
                    break;
                case Mode.ZeroPageX:
                    mem = string.Format(
                        "${0:X2},X @ {1:X2} = {2:X2}",
                        a,
                        (byte)(a + registers.x),
                        DebugReadMem((byte)(a + registers.x))
                    );
                    break;
                case Mode.ZeroPageY:
                    mem = string.Format(
                        "${0:X2},Y @ {1:X2} = {2:X2}",
                        a,
                        (byte)(a + registers.y),
                        DebugReadMem((byte)(a + registers.y))
                    );
                    break;
                case Mode.Indirect:
                    mem = string.Format(
                        "(${0:X2}{1:X2}) = {2:X2}{3:X2}",
                        b,
                        a,
                        DebugReadMem((ushort)((b << 8) | (byte)(a + 1))),
                        DebugReadMem((ushort)((b << 8) | a))
                    );
                    break;
                case Mode.IndirectX:
                    {
                        var ax = (byte)(a + registers.x);
                        var addr = ReadWordZeroPage(ax);
                        var v = DebugReadMem(addr);
                        mem = string.Format("(${0:X2},X) @ {1:X2} = {2:X4} = {3:X2}", a, ax, addr, v);
                        break;
                    }
                case Mode.IndirectY:
                    {
                        var av = ReadWordZeroPage(a);
                        var addr = (ushort)(av + registers.y);
                        var v = DebugReadMem(addr);
                        mem = string.Format("(${0:X2}),Y = {1:X4} @ {2:X4} = {3:X2}", a, av, addr, v);
                        break;
                    }
            }

            var output = string.Format(
                "{0:X4}  {1} {2}{3} {4,-28}A:{5:X2} X:{6:X2} Y:{7:X2} P:{8:X2} SP:{9:X2} PPU:{10,3},{11,3} CYC:{12}",
                registers.pc,
                instr,
                instr_ilgl,
                instr_name,
                mem,
                registers.acc,
                registers.x,
                registers.y,
                (byte)registers.status,
                registers.sp,
                bus.ppu.scanline,
                bus.ppu.cycles,
                cycles
            );

            traceFile.WriteLine(output);
            traceFile.Flush();
        }
    }
}