using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Z80;

namespace H8DUtility.H89
{
    public class H89IO : IIO
    {
        private Form4 Emulator;

        public byte[] Port170Q = new byte[4];
        public byte[] Port174Q = new byte[4];
        public byte[] Port320Q = new byte[8];
        public byte[] Port330Q = new byte[8];
        public byte[] Port340Q = new byte[8];
        public byte[] Port350Q = new byte[8];
        public byte[] Port360Q = new byte[2];
        public byte[] Port362Q = new byte[6];
        public byte[] Port370Q = new byte[2];
        public byte[] Port372Q = new byte[6];

        //
        //  Port assignments:
        //
        //  170 (0x78) - 173 (0x7B) H37 floppy device
        //  174 (0x7C) - 177 (0x7F) H17 floppy device
        //  320 (0xD0) - 327 (0xD7) 8250 UART DCE device
        //  330 (0xD8) - 337 (0xDF) 8250 UART DTE device
        //  340 (0xE0) - 347 (0xE7) 8250 UART LP device
        //  350 (0xE8) - 357 (0xEF) 8250 UART console device
        //  360 (0xF0) - 361 (0xF1) H8 front panel
        //  362 (0xF2)              H89 control port
        //  370 (0xF8) - 371 (0xF9) TAPE control/data ports
        //  372 (0xFA) - 377 (0xFF) H8-5 Serial I/O (not used)
        //
        //  Video emulation is handled via a picturebox component using the actual
        //  H19 ROM font. All output to 350Q will draw an image of the font character
        //  to the picturebox.
        //
        //  All input from 350Q will read from the keyboard.
        //

        public H89IO(Form4 form)
        {
            Emulator = form;
        }

        public void ResetPorts()
        {
            int i;

            for (i = 0; i < Port170Q.Length; i++)
            {
                Port170Q[i] = 0;
            }
            for (i = 0; i < Port174Q.Length; i++)
            {
                Port174Q[i] = 0;
            }
            for (i = 0; i < Port320Q.Length; i++)
            {
                Port320Q[i] = 0;
            }
            for (i = 0; i < Port330Q.Length; i++)
            {
                Port330Q[i] = 0;
            }
            for (i = 0; i < Port340Q.Length; i++)
            {
                Port340Q[i] = 0;
            }
            for (i = 0; i < Port350Q.Length; i++)
            {
                Port350Q[i] = 0;
            }
            Port350Q[2] = 0x01;
            Port350Q[3] = 0x03;
            Port350Q[5] = 0x60;
            for (i = 0; i < Port360Q.Length; i++)
            {
                Port360Q[i] = 0;
            }
            for (i = 0; i < Port362Q.Length; i++)
            {
                Port362Q[i] = 0;
            }
            for (i = 0; i < Port370Q.Length; i++)
            {
                Port370Q[i] = 0;
            }
            for (i = 0; i < Port372Q.Length; i++)
            {
                Port372Q[i] = 0;
            }
        }

        public byte ReadPort(ushort Port)
        {
            byte Value = 0xFF;
            byte port = (byte)(Port & 0xFF);

            //  floppy device (170Q - 174Q)
            if (port >= 0x78 && port <= 0x7B)
            {
                port += 4;
                //int idx = port - 0x78;
                //Value = Port170Q[idx];
            }
            //  floppy device (174Q - 177Q)
            if (port >= 0x7C && port <= 0x7F)
            {
                int idx = port - 0x7C;
                if (idx == 2)   //  sync character port
                {
                    Value = 0xFF;
                }
                else
                {
                    if (idx == 0)
                    {
                        //  reset the received data available when a byte is read from 174Q
                        Port174Q[1] &= 0xFE;
                    }
                    Value = Port174Q[idx];
                    if (idx == 1)
                    {
                        Value |= 0xC0;
                    }
                    else if (idx == 3)
                    {
                        if (Emulator.H17DiskWriteProtect())
                        {
                            Value |= 0x04;
                        }
                        else
                        {
                            Value &= 0x0B;
                        }
                    }
                }
            }
            //  DCE device (320Q - 327Q)
            else if (port >= 0xD0 && port <= 0xD7)
            {
                int idx = port - 0xD0;
                Value = Port320Q[idx];
            }
            //  DTE device (330Q - 337Q)
            else if (port >= 0xD8 && port <= 0xDF)
            {
                int idx = port - 0xD8;
                Value = Port330Q[idx];
            }
            //  LP device (340Q - 347Q)
            else if (port >= 0xE0 && port <= 0xE7)
            {
                int idx = port - 0xE0;
                Value = Port340Q[idx];
            }
            //  console device (350Q - 357Q)
            else if (port >= 0xE8 && port <= 0xEF)
            {
                int idx = port - 0xE8;
                switch (idx)
                {
                    case 0: //  data register
                        if ((Port350Q[3] & 0x80) == 0x80) //  divisor latch
                        {
                            Value = 0x0E;
                        }
                        else
                        {
                            if (Emulator.KeyboardBufferStatus())
                            {
                                Port350Q[2] = 0x01;
                                Port350Q[idx] = (byte)Emulator.ReadKeyboardBuffer();
                                Value = Port350Q[idx];
                            }
                            else
                            {
                                Value = 0;
                            }
                            Port350Q[5] &= 0xFE;
                        }
                        break;
                    case 1:
                        if ((Port350Q[3] & 0x80) == 0x80) //  divisor latch
                        {
                            Value = 0;
                        }
                        else
                        {
                            Value = Port350Q[idx];
                        }
                        break;
                    case 5: //  line status register
                        Port350Q[idx] |= 0x60;
                        if (Emulator.KeyboardBufferStatus())
                        {
                            Value = (byte)(Port350Q[idx] | 0x01);
                        }
                        else
                        {
                            Value = (byte)(Port350Q[idx] & 0xFE);
                        }
                        break;
                    default:
                        Value = Port350Q[idx];
                        break;
                }
            }
            //  H88 control output port (360Q - 361Q)
            else if (port >= 0xF0 && port <= 0xF1)
            {
                Value = 0xFF;
            }
            //  H88 control port (362Q)
            else if (port == 0xF2)
            {
                Value = 0x20;   //  switch settings: 0010.0000
            }
            //  TAPE control/data ports (370Q - 371Q)
            else if (port >= 0xF8 && port <= 0xF9)
            {
                int idx = port - 0xF8;
                Value = Port370Q[idx];
            }
            //  H8-5 Serial I/O (372Q - 377Q)
            else if (port >= 0xFA && port <= 0xFF)
            {
                int idx = port - 0xFA;
                Value = Port372Q[idx];
            }

            Emulator.OnPortRead(port, Value);

            return (Value);
        }

        public void WritePort(ushort Port, byte Value)
        {
            byte port = (byte)(Port & 0xFF);
            //  floppy device (170Q - 173Q)
            if (port >= 0x78 && port <= 0x7B)
            {
                port += 4;
                //int idx = port - 0x78;
                //Port170Q[idx] = Value;
            }
            //  floppy device (174Q - 177Q)
            if (port >= 0x7C && port <= 0x7F)
            {
                int idx = port - 0x7C;
                if (idx == 3)   //  port 177Q
                {
                    Emulator.H17Control(port, Value);
                }
                else
                {               //  ports 174Q, 175Q, 176Q
                    Emulator.H17Write(port, Value);
                }
            }
            //  DCE device (320Q - 327Q)
            else if (port >= 0xD0 && port <= 0xD7)
            {
                int idx = port - 0xD0;
                Port320Q[idx] = Value;
            }
            //  DTE device (330Q - 337Q)
            else if (port >= 0xD8 && port <= 0xDF)
            {
                int idx = port - 0xD8;
                Port330Q[idx] = Value;
            }
            //  LP device (340Q - 347Q)
            else if (port >= 0xE0 && port <= 0xE7)
            {
                int idx = port - 0xE0;
                Port340Q[idx] = Value;
            }
            //  console device (350Q - 357Q)
            else if (port >= 0xE8 && port <= 0xEF)
            {
                int idx = port - 0xE8;
                Port350Q[idx] = Value;
                if (port == 0xE8 && (Port350Q[3] & 0x80) == 0)
                {
                    Emulator.H19Update(Value);
                }
            }
            //  H8 front panel (360Q - 361Q)
            else if (port >= 0xF0 && port <= 0xF1)
            {
                int idx = port - 0xF0;
                Port360Q[idx] = Value;
            }
            //  H88 control port (362Q)
            else if (port == 0xF2)
            {
                Port362Q[0] = Value;
            }
            //  TAPE control/data ports (370Q - 371Q)
            else if (port >= 0xF8 && port <= 0xF9)
            {
                int idx = port - 0xF8;
                Port370Q[idx] = Value;
            }
            //  H8-5 Cassette I/O (372Q - 377Q)
            else if (port >= 0xFA && port <= 0xFF)
            {
                int idx = port - 0xFA;
                Port372Q[idx] = Value;
            }

            Emulator.OnPortWrite(port, Value);
        }
    }
}
