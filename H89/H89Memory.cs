using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Z80;

namespace H8DUtility.H89
{
    
    public class H89Memory : IMemory
    {
        private Form4 Emulator;

        private byte[] Memory = new byte[0x10000];

        public event OnReadHandler OnRead;
        public event OnWriteHandler OnWrite;

        //
        //
        //

        public H89Memory(Form4 form)
        {
            Emulator = form;
        }

        public byte ReadByte(ushort Address)
        {
            if (OnRead != null)
            {
                OnRead(Address);
            }
            return Memory[Address];
        }

        public void WriteByte(ushort Address, byte Value)
        {
            if (OnWrite != null)
            {
                OnWrite(Address, Value);
            }
            else
            {
                Memory[Address] = Value;
            }
        }

        public ushort ReadWord(ushort Address)
        {
            return (ushort)(ReadByte((ushort)(Address + 1)) << 8 | ReadByte(Address));
        }

        public void WriteWord(ushort Address, ushort Value)
        {
            WriteByte(Address, (byte)(Value & 0x00FF));
            WriteByte((ushort)(Address + 1), (byte)((Value & 0xFF00) >> 8));
        }

        public int Size
        {
            get
            {
                return 0x10000;
            }
        }

        public byte this[int Address]
        {
            get
            {
                return Memory[Address];
            }
            set
            {
                Memory[Address] = value;
            }
        }

        public byte[] Raw
        {
            get
            {
                return Memory;
            }
        }
    }
}
