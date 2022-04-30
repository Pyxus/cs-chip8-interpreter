using System.Reflection.Emit;
using System.Collections.Generic;
namespace Chip8
{
    public class Cpu
    {
        private Memory _ram; // Holds our memory represented by an array of 4096 bytes.
        private Memory _vram; // Holds our video memory which will be used to draw to the display
        private byte[] _vRegisters = new byte[16]; // General purpose register. Think of it as a variables that store data current being used by the CPU
        private short _iRegister; // Specifications says its used to 'store memory addresses' but im not sure what programs would use it for...
        private byte _timerRegister; // Basically just a variable. I don't know what programs would actually use it for...
                                     // But we don't need to know that in order to implement it. 
        private byte _delayRegister; // SImilar case to the timer
        private ushort _programCounter; // Tells where in memory the next instruction should be read
        private byte _stackPointer; // Stores where in the call stack the program is.
                                    // Basically if multiple functions are called this combined with the stack helps keep track of all their return points.
        private ushort[] _stack = new ushort[16]; // The stack stores the address the interpreter should return to when a subroutine is finished.
        private ushort _opcode; // Stores the current instruction
        private Dictionary<byte, Action> _opHandlers = new Dictionary<byte, Action>(); // Basically a list of OpHanlder functions that can be retreived using the opID

        public Cpu(Memory ram, Memory vram)
        {
            _ram = ram;
            _vram = vram;

            _opHandlers.Add(0x0, OP_00E);
            _opHandlers.Add(0x1, OP_1nnn);
            _opHandlers.Add(0x2, OP_2nnn);
            _opHandlers.Add(0x3, OP_3xkk);
            _opHandlers.Add(0x4, OP_4xkk);
            _opHandlers.Add(0x5, OP_5xy0);
            _opHandlers.Add(0x6, OP_6xkk);
            _opHandlers.Add(0x7, OP_7xkk);
            _opHandlers.Add(0x8, OP_8xy);
            _opHandlers.Add(0x9, OP_9xy0);
            _opHandlers.Add(0xA, OP_Annn);
            _opHandlers.Add(0xB, OP_Bnnn);
            _opHandlers.Add(0xC, OP_Cxkk);
            _opHandlers.Add(0xD, OP_Dxyn);
            _opHandlers.Add(0xE, OP_Ex);
            _opHandlers.Add(0xF, OP_Fx);
        }

        public void Reset()
        {
            _programCounter = 0x200; // 0x00 to 0x1FF are reserved so the program counter starts at 0x200
        }

        public void Cycle()
        {
            Fetch();
            Execute();
        }

        private void Fetch()
        {
            /*
                Each location in memory only stores a byte but as per the chip8 specifications all instructions
                are 2 bytes long stored adjacent in memory. So in order to fetch an opcode we read the address 
                at the program counter and the address next to that one from memory.]

                For convinience these 2 bytes are stored as 16-bit value which is done by by left bit shifting '<<'
                the first byte and binary oring '|' it with the second byte. If you think of the byte as an array of 8 bits (1s and 0s)
                then you can imagine shifting by 8 to the left as moving the elemets of the 'array' left by 8.
                [00001010] << 8 -> [00001010XXXXXXXX] note: The X's would actually be 0 but I did it this way to make the shift easier to see

                The binary or can thought of as a logical or applied to each bit. So 1 | 1 = 1, 1 | 0 = 1, 0 | 0 = 0.
                But since the values at the right end will all be 0 after the shift this basically results in the 2nd byte
                being added on to the end.
                
                so if byte 1 = [00001010] and byte 2 = [10010001] the the operation below does...
                [00001010] << 8 -> [0000101000000000]

                [0000101000000000]
                |       [10010001]
                --------------------
                [0000101010010001]
                End result you have a 16-bit (or 2 byte) value containing the opcode
            */
            _opcode = (ushort)((_ram[_programCounter] << 8) | _ram[_programCounter + 1]);
            _programCounter += 2; // Incremeted twice since we read 2 bytes at a time above
        }

        private void Execute()
        {
            /*
                For our decoding we use the first 4 bits (nibble) as an ID to call the appropriate procedure. 
                In order to get that ID we & the op code against the 0xF000 mask which well set
                all but the first nibble to 0. If you think about it 0 & anything will be 0 while F,
                which is 1111 in binary, & anything will always be the original value.

                [1101000101111010] -> Random number
               &[1111000000000000] -> 0xF000
               ----------------------
                [1101000000000000]

                However aftering &ing the value is still 16 bit and we only need the first 4 so we bit shift to the right by 12.
                [1101000000000000] >> 12 -> [1101]
            */
            var opID = (byte)((_opcode & 0xF000u) >> 12);
            var opHandler = OP_NULL;
            _opHandlers.TryGetValue(opID, out opHandler);
            opHandler?.Invoke(); // This is a c# thing, basically it'll call the function stored in opHanlder
        }

        private void OP_NULL()
        {
            Console.WriteLine("Invalid Instruction read");
        }

        private void OP_00E()
        {
            /*
                Where using the & similiar to how we used it in the execute method.
                However we don't need to bit shift since the left most 0 bites are
                ignored and of course since all but the last nibble is being set to 0
                we end up with a value that's just that nibble.
            */
            if ((_opcode & 0x000Fu) == 0x0u)
            {
                OP_OOEO();
            }
            else if ((_opcode & 0x000Fu) == 0xEu)
            {
                OP_OOEE();
            }
            else
            {
                OP_NULL();
            }
        }

        // UNTESTED
        private void OP_OOEO()
        {
            _vram.Clear();
        }

        // UNTESTED
        private void OP_OOEE()
        {
            _programCounter = _stack[--_stackPointer];
        }

        // UNTESTED
        private void OP_1nnn()
        {
            var address = (ushort) (_opcode & 0x0FFFu);
            _programCounter = address;
        }


        private void OP_2nnn()
        {
            var address = (ushort) (_opcode & 0x0FFFu);
            _stack[++_stackPointer] = _programCounter;
            _programCounter = address;
        }

        private void OP_3xkk()
        {
            var Vx = (byte)((_opcode & 0x0F00) >> 8);
            var kk = (byte)(_opcode & 0x00FF);

            if (_vRegisters[Vx] == kk)
            {
                _programCounter += 2;
            }
        }

        private void OP_4xkk()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte kk = (byte)(_opcode & 0x00FF);
            if (_vRegisters[Vx] != kk)
            {
                _programCounter += 2;
            }
        }

        private void OP_5xy0()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x0F00) << 8);
            if (_vRegisters[Vx] == _vRegisters[Vy])
            {
                _programCounter += 2;
            }
        }

        private void OP_6xkk()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte kk = (byte)(_opcode & 0x00FF);
            _vRegisters[Vx] = kk;
        }

        private void OP_7xkk()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte kk = (byte)(_opcode & 0x00FF);
            _vRegisters[Vx] += kk;

        }
        private void OP_8xy()
        {
            switch (_opcode & 0x000f)
            {
                case 0x0:
                    OP_8xy0();
                    break;
                case 0x1:
                    OP_8xy1();
                    break;
                case 0x2:
                    OP_8xy2();
                    break;
                case 0x3:
                    OP_8xy1();
                    break;
                case 0x4:
                    OP_8xy1();
                    break;
                case 0x5:
                    OP_8xy1();
                    break;
                case 0x6:
                    OP_8xy1();
                    break;
                case 0x7:
                    OP_8xy1();
                    break;
                case 0xE:
                    OP_8xyE();
                    break;
                default:
                    OP_NULL();
                    break;
            }


        }
        private void OP_8xy0()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x00F0) >> 4);
            _vRegisters[Vx] = _vRegisters[Vy];


        }
        private void OP_8xy1()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x00F0) >> 4);
            _vRegisters[Vx] |= _vRegisters[Vy];

        }
        private void OP_8xy2()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x00F0) >> 8);
            _vRegisters[Vx] &= _vRegisters[Vy];

        }
        private void OP_8xy3()
        {
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x00F0) >> 4);
            _vRegisters[Vx] ^= _vRegisters[Vy];

        }
        private void OP_8xy4()
        {
            /*
            byte Vx = (byte)((_opcode & 0x0F00) >> 8);
            byte Vy = (byte)((_opcode & 0x00F0) >> 4);
            _vRegisters[Vx] += _vRegisters[Vy];
            if (_vRegisters[Vx] > 255){
               byte Vf = 1;
            }else
                byte Vf = 0;
            */
        }
        private void OP_8xy5()
        {
      

        }
        private void OP_8xy6()
        {
           

        }
        private void OP_8xy7()
        {
          

        }
        private void OP_8xyE()
        {
        

        }

        private void OP_9xy0()
        {

        }

        private void OP_Annn()
        {

        }

        private void OP_Bnnn()
        {

        }

        private void OP_Cxkk()
        {

        }

        private void OP_Dxyn()
        {

        }

        private void OP_Ex()
        {

        }

        private void OP_Fx()
        {

        }

    }
}



