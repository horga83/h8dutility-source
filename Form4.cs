using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using MarkGwilliam.com.Framework.Convert;
using Z80;

namespace H8DUtility
{
    public partial class Form4 : Form
    {
        public class H19Panel : Form
        {
            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }
        }

        private const int MaxTracks = 40;
        private const int MaxSides = 1;
        //  maximum image size of a hard sector, double sided, 80 track drive
        //  double sided images will be stored as odd (256 bytes), even (256 bytes), etc.
        //  disk size is stored in the disk label, byte 16 (volume flags) for HDOS
        //  value 0 = 40 track, 1 side
        //  value 1 = 40 track, 2 sides
        //  value 2 = 80 track, 1 side
        //  value 3 = 80 track, 2 sides
        private const int MaxImageSize = ((MaxTracks * MaxSides) * 2560);

        public H89.H89Memory H89MEM;
        public H89.H89IO H89IO;
        public uP H89Z80;
        public Z80Disassembler H89Z80Disasm;

        private bool bKeyboardInterrupt;
        private char[] KeyboardBuffer = new char[64];
        private int KeyboardBufferBeg;
        private int KeyboardBufferEnd;

        private bool bBreak;
        private bool bEsc;
        private bool bLoadROM;
        private bool bReset;
        private bool bRunning;

        private ushort BreakPointAddress;
        private ushort DisassembleAddress;
        private ushort DisassembleAddressNext;

        private bool bH17Hole;
        private bool bH17MotorOn;
        private bool bH17RamEnable;
        private bool bH17Write;
        private bool bH17Read;
        private bool bH17WriteGate;
        private int H17Direction;
        private int H17DriveSelect;
        private int H17Drive;
        private int H17Index;
        private int H17Sector;
        private int H17Side;
        private int H17Track;
        private int H17Revs;
        private int H17WriteSector;
        private int H17WriteState;

        //  time the sector hole remains active
        private const int H17HoleTickMax = 3000;
        private int H17HoleTick;

        //  time between sectors
        private const int H17SectorTickMax = 19800;
        private int H17SectorTick;

        private bool[] bH17DoorOpen = new bool[3];
        private bool[] bH17WriteProtect = new bool[3];
        private int[] H17DiskVolume = new int[3];

        private int GlobalTick;
        private int UpdateTick;
        private int LastTStates;

        private string[] FullFileName = new string[3];
        private string[] SafeFileName = new string[3];

        private int DiskWriteIndex;
        private int DiskReadIndex;
        private int DiskReadCheckSum;

        private byte[] LastPortInp = new byte[256];
        private byte[] LastPortOut = new byte[256];

        private struct ImageBuffer
        {
            public byte[] DiskImageBuffer;
            public int Tracks;
            public int Sides;
        };

        private bool[] bDiskImageDirty = new bool[3];
        private ImageBuffer[] DiskImage = new ImageBuffer[3];

        private System.Drawing.Color H19ScreenColor = System.Drawing.Color.Lime;
        private byte[,] H19Cell = new byte[80, 25];
        private PointF H19Scale = new PointF(1.4f, 2);
        private bool bH19BlockCursor;
        private bool bH19CursorAddressing;
        private bool bH19CursorOn = true;
        private bool bH19CursorVis;
        private bool bH19GraphicsMode;
        private bool bH19Init;
        private bool bH19KeypadShift;
        private bool bH19Line25;
        private bool bH19ReverseVideo;
        private bool bH19ResetMode;
        private bool bH19SetMode;
        private int H19CursorTick;
        private const int H19FontHeight = 16;
        private const int H19FontHeightStep = H19FontHeight / 16;
        private const int H19FontWidth = 8;
        private int H19ScreenCellWidth = 8;
        private int H19ScreenCellHeight = 10 * H19FontHeightStep;
        private int H19State;
        private Point H19ScreenPos;
        private Point H19SavedPos;
        private SpriteBatch H19FontSprite;
        private Texture2D[] H19Font = new Texture2D[256];
        private GraphicsDevice H19RenderDevice;
        private PresentationParameters H19RenderParams;
        private H19Panel H19RenderSurface;

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long count);
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long freq);
        private double Frequency;

        private UnicodeEncoding Encoding = new UnicodeEncoding();

        //
        //
        //

        public Form4()
        {
            InitializeComponent();
        }
        // dcp ROM Search function
        private void Form4_BootROM()
        {
            string[] boot_list = Directory.GetFiles(string.Format(Application.StartupPath + "\\ROMS"),"b_*");
            if (boot_list.Length == 0)
            {
                comboBox1.Items.Add("No Boot ROM File Found");

            }
            else
            {
                foreach (string files in boot_list)
                {
                    string file_name;
                    file_name = files.Substring(files.LastIndexOf("\\") + 1).ToUpper();
                    comboBox1.Items.Add(file_name);

                }
            }

           // Stream stream = new FileStream(string.Format("{0}\\ROMS\\2716_444-62_MTR89.BIN", Application.StartupPath), FileMode.Open);
        }
        private void Form4_Load(object sender, EventArgs e)
        {
            Form4_BootROM();
            /* dcp
             * comboBox1.Items.Add("444-62 (MTR-89)");
             
            comboBox1.Items.Add("444-142 (MTR-90A)");
            comboBox1.Items.Add("2732_mttest2");
            */
            comboBox2.Items.Add("H-9");
            comboBox2.Items.Add("H-19");
            comboBox2.SelectedIndex = 1;
            comboBox2.Enabled = false;

            comboBox3.Items.Add("1S/40T");
            comboBox3.Items.Add("2S/40T");
            comboBox3.Items.Add("1S/80T");
            comboBox3.Items.Add("2S/80T");
            comboBox3.SelectedIndex = 0;

            comboBox4.Items.Add("1S/40T");
            comboBox4.Items.Add("2S/40T");
            comboBox4.Items.Add("1S/80T");
            comboBox4.Items.Add("2S/80T");
            comboBox4.SelectedIndex = 0;

            comboBox5.Items.Add("1S/40T");
            comboBox5.Items.Add("2S/40T");
            comboBox5.Items.Add("1S/80T");
            comboBox5.Items.Add("2S/80T");
            comboBox5.SelectedIndex = 0;

            colorDialog1.Color = H19ScreenColor;
            button8.BackColor = H19ScreenColor;

            H19Init();

            listBox1.DoubleClick += new EventHandler(listBox1_DoubleClick);

            textBox1.Text = "000.000";
            textBox1.KeyPress += new KeyPressEventHandler(textBox1_KeyPress);
            textBox2.Text = "000.000";
            textBox2.KeyPress += new KeyPressEventHandler(textBox2_KeyPress);

            H89MEM = new H8DUtility.H89.H89Memory(this);
            H89IO = new H8DUtility.H89.H89IO(this);
            H89Z80 = new uP(H89MEM, H89IO);
            H89Z80.OnFetch += new uP.OnFetchHandler(H89Z80_OnFetch);
            H89MEM.OnRead += new OnReadHandler(H89Memory_OnRead);
            H89MEM.OnWrite += new OnWriteHandler(H89Memory_OnWrite);
            H89Z80Disasm = new Z80Disassembler(H89MEM);

            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 1;

            this.FormClosing += new FormClosingEventHandler(Form4_FormClosing);

            long freq;
            if (QueryPerformanceFrequency(out freq))
            {
                Frequency = (double)freq;
            }
            else
            {
                checkBox4.Enabled = false;
            }
        }

        void Form4_FormClosing(object sender, FormClosingEventArgs e)
        {
            H19RenderSurface.Close();
        }

        public void Unload()
        {
        }

        public void H89Memory_OnRead(ushort address)
        {
            if (checkBox2.Checked)
            {
                if (H89Z80.Status.PC != address)
                {
                    byte v = H89MEM.Raw[address];
                    SendToListBox(string.Format("< M{0},{1}", ToOctalAdr(address), ToOctal(v)));
                }
            }
        }

        public void H89Memory_OnWrite(ushort address, byte value)
        {
            if (address < 0x2000)   //  lower 8K
            {
                //  if H17 RAM is disabled or ROM disable then just return
                if ((bH17RamEnable == false) && (H89IO.Port362Q[0] & 0x20) == 0)
                {
                    return;
                }
            }
            if (checkBox2.Checked)
            {
                if (H89MEM.Raw[address] != value)
                {
                    SendToListBox(string.Format("> M{0},{1}", ToOctalAdr(address), ToOctal(value)));
                }
            }
            H89MEM.Raw[address] = value;
        }

        public void Delay(double secs)
        {
            long start_time;
            QueryPerformanceCounter(out start_time);
            long end_time = start_time;
            double delay=0;
            do
            {
                QueryPerformanceCounter(out end_time);
                delay = (double)(end_time - start_time) / Frequency;
            } while (delay < secs);
        }

        //  calls this function before executing each instruction
        void H89Z80_OnFetch()
        {
            int elapsed_states = H89Z80.tstates - LastTStates;

            if (checkBox4.Checked)
            {
                Delay(0.000005);
            }
            if (bRunning)
            {
                if (BreakPointAddress != 0)
                {
                    if (H89Z80.Status.PC == BreakPointAddress)
                    {
                        BreakPoint();
                    }
                }
                if (bH17Read == false && bH17WriteGate == false)
                {
                    H17Service();
                }
                if (H89Z80.tstates >= 2000)
                {
                    int tstates = H89Z80.tstates;
                    if ((H89IO.Port362Q[0] & 0x02) == 0x02)
                    {
                        H89Z80.Status.I = 1;
                        H89Z80.Interrupt();
                    }
                    H89Z80.tstates = tstates - 2000;
                }
                if (bKeyboardInterrupt)
                {
                    if (KeyboardBufferStatus())
                    {
                        if (H89Z80.Status.IFF1)
                        {
                            H89Z80.Status.I = 3;
                            H89Z80.Interrupt();
                        }
                    }
                    else
                    {
                        bKeyboardInterrupt = false;
                    }
                }
                H17HoleTick += elapsed_states;
                if (H17HoleTick > H17HoleTickMax)
                {
                    H17HoleTick = H17HoleTickMax;
                }
                H17SectorTick += elapsed_states;
                if (H17SectorTick > H17SectorTickMax)
                {
                    H17SectorTick = H17SectorTickMax;
                }
            }
            else
            {
                H89Z80.event_next_event = H89Z80.tstates;
            }
            LastTStates = H89Z80.tstates;
            GlobalTick = Environment.TickCount & Int32.MaxValue;
            if (GlobalTick - UpdateTick >= 16)
            {
                if (bH19CursorOn)
                {
                    if (H19CursorTick >= 250)
                    {
                        if (bH19CursorVis)
                        {
                            bH19CursorVis = false;
                        }
                        else
                        {
                            bH19CursorVis = true;
                        }
                        H19CursorTick = 0;
                        H19Paint();
                    }
                }

                UpdateStats();
                Application.DoEvents();
                UpdateTick = GlobalTick;
                H19CursorTick += 16;
            }
        }


        void panel1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            char c = (char)(e.KeyValue & 0x7F);
            if (e.KeyCode == Keys.ControlKey)
            {
                return;
            }
            if (e.KeyCode == Keys.ShiftKey)
            {
                return;
            }
            if (e.KeyCode == Keys.Alt)
            {
                return;
            }
            if (e.Modifiers == Keys.Control)
            {
                c &= (char)0x1F;
            }
            else if (e.Modifiers == Keys.Shift)
            {
                switch (e.KeyCode)
                {
                    case Keys.D0:
                        c = ')';
                        break;
                    case Keys.D1:
                        c = '!';
                        break;
                    case Keys.D2:
                        c = '@';
                        break;
                    case Keys.D3:
                        c = '#';
                        break;
                    case Keys.D4:
                        c = '$';
                        break;
                    case Keys.D5:
                        c = '%';
                        break;
                    case Keys.D6:
                        c = '^';
                        break;
                    case Keys.D7:
                        c = '&';
                        break;
                    case Keys.D8:
                        c = '*';
                        break;
                    case Keys.D9:
                        c = '(';
                        break;
                    case Keys.OemBackslash:
                        c = '|';
                        break;
                    case Keys.OemCloseBrackets:
                        c = '}';
                        break;
                    case Keys.Oemcomma:
                        c = '<';
                        break;
                    case Keys.OemMinus:
                        c = '_';
                        break;
                    case Keys.OemOpenBrackets:
                        c = '{';
                        break;
                    case Keys.OemPeriod:
                        c = '>';
                        break;
                    case Keys.OemPipe:
                        c = '|';
                        break;
                    case Keys.Oemplus:
                        c = '+';
                        break;
                    case Keys.OemQuestion:
                        c = '?';
                        break;
                    case Keys.OemQuotes:
                        c = '"';
                        break;
                    case Keys.OemSemicolon:
                        c = ':';
                        break;
                    case Keys.Oemtilde:
                        c = '~';
                        break;
                }
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.OemBackslash:
                        c = (char)0x5C;
                        break;
                    case Keys.OemCloseBrackets:
                        c = ']';
                        break;
                    case Keys.Oemcomma:
                        c = ',';
                        break;
                    case Keys.OemMinus:
                        c = '-';
                        break;
                    case Keys.OemOpenBrackets:
                        c = '[';
                        break;
                    case Keys.OemPeriod:
                        c = '.';
                        break;
                    case Keys.OemPipe:
                        c = (char)0x5C;
                        break;
                    case Keys.Oemplus:
                        c = '=';
                        break;
                    case Keys.OemQuestion:
                        c = '/';
                        break;
                    case Keys.OemQuotes:
                        c = (char)0x27;
                        break;
                    case Keys.OemSemicolon:
                        c = ';';
                        break;
                    case Keys.Oemtilde:
                        c = '`';
                        break;
                    case Keys.NumPad0:
                        c = '0';
                        break;
                    case Keys.NumPad1:
                    case Keys.End:
                        c = '1';
                        break;
                    case Keys.NumPad2:
                    case Keys.Down:
                        c = '2';
                        break;
                    case Keys.NumPad3:
                    case Keys.PageDown:
                        c = '3';
                        break;
                    case Keys.NumPad4:
                    case Keys.Left:
                        c = '4';
                        break;
                    case Keys.NumPad5:
                        c = '5';
                        break;
                    case Keys.NumPad6:
                    case Keys.Right:
                        c = '6';
                        break;
                    case Keys.NumPad7:
                    case Keys.Home:
                        c = '7';
                        break;
                    case Keys.NumPad8:
                    case Keys.Up:
                        c = '8';
                        break;
                    case Keys.NumPad9:
                    case Keys.PageUp:
                        c = '9';
                        break;
                    case Keys.Insert:
                        c = '0';
                        break;
                    case Keys.Delete:
                        c = '.';
                        break;
                    case Keys.F1:
                        KeyboardCharInsert((char)0x1B);
                        c = 'S';
                        break;
                    case Keys.F2:
                        KeyboardCharInsert((char)0x1B);
                        c = 'T';
                        break;
                    case Keys.F3:
                        KeyboardCharInsert((char)0x1B);
                        c = 'U';
                        break;
                    case Keys.F4:
                        KeyboardCharInsert((char)0x1B);
                        c = 'V';
                        break;
                    case Keys.F5:
                        KeyboardCharInsert((char)0x1B);
                        c = 'W';
                        break;
                    case Keys.F7:   //  blue
                        KeyboardCharInsert((char)0x1B);
                        c = 'P';
                        break;
                    case Keys.F8:   //  red
                        KeyboardCharInsert((char)0x1B);
                        c = 'Q';
                        break;
                    case Keys.F9:   //  gray
                        KeyboardCharInsert((char)0x1B);
                        c = 'R';
                        break;
                    default:
                        if (e.KeyValue >= 'A' && e.KeyValue <= 'Z')
                        {
                            if (Control.IsKeyLocked(Keys.CapsLock) == false)
                            {
                                c |= (char)0x20;
                            }
                        }
                        break;
                }
            }
            KeyboardCharInsert(c);
            if ((H89IO.Port350Q[1] & 0x01) == 0x01)
            {
                H89IO.Port350Q[5] |= 0x01;
                H89IO.Port350Q[2] = 0x04;
                bKeyboardInterrupt = true;
            }
        }

        public void KeyboardCharInsert(char c)
        {
            KeyboardBuffer[KeyboardBufferEnd] = c;
            KeyboardBufferEnd = (KeyboardBufferEnd + 1) % 64;
        }

        public char ReadKeyboardBuffer()
        {
            char k = KeyboardBuffer[KeyboardBufferBeg];
            KeyboardBufferBeg = (KeyboardBufferBeg + 1) % 64;
            return (k);
        }

        public bool KeyboardBufferStatus()
        {
            if (KeyboardBufferBeg != KeyboardBufferEnd)
            {
                return (true);
            }
            return (false);
        }

        public void OnPortRead(ushort port, byte value)
        {
            if (checkBox1.Checked)
            {
                bool skip = false;
                if (checkBox5.Checked)
                {
                    if (port == 0x7C || port == 0xE8)
                    {
                        skip = true;
                    }
                    else if (LastPortInp[port] == value)
                    {
                        skip = true;
                    }
                }
                if (!skip && port != 0xED)
                {
                    LastPortInp[port] = value;
                    SendToListBox(string.Format("< INP {0:X2},{1:X2}", port, value));
                }
            }

            if (port >= 0x7C && port <= 0x7F)
            {
                if (port == 0x7C)
                {
                    H89IO.Port174Q[2] = 0xFF;
                    H17ReadNextByte();
                }
                else if (port == 0x7D)
                {
                    H89IO.Port174Q[3] &= 0xF7;  //  reset sync detect flag
                }
                else if (port == 0x7F)
                {
                    H17Service();
                }
                if (bH17WriteGate)
                {
                    bH17Read = false;
                }
            }
        }

        public void OnPortWrite(ushort port, byte value)
        {
            if (checkBox3.Checked)
            {
                bool skip = false;
                if (checkBox5.Checked)
                {
                    if (port == 0x7C || port == 0xE8)
                    {
                        skip = true;
                    }
                    else if (LastPortOut[port] == value)
                    {
                        skip = true;
                    }
                }
                if (!skip && port != 0xF0)
                {
                    LastPortOut[port] = value;
                    SendToListBox(string.Format("> OUT {0:X2},{1:X2}", port, value));
                }
            }
            if (port == 0xF2)
            {
                if ((value & 0x40) == 0)
                {
                    H17Side = 0;
                }
                else if ((value & 0x40) == 0x40)
                {
                    H17Side = 1;
                }
            }
        }

        //  writing to the H17 control port 177Q
        public void H17Control(ushort port,byte value)
        {
            //  0000.0001 = write gate enable
            if ((value & 0x01) != 0)
            {
                bH17WriteGate = true;
                H89IO.Port174Q[1] |= 0x80;  //  set transmit buffer empty flag
                if (bH17Read)
                {
                    H17WriteState = 1;
                }
                else
                {
                    H17WriteState = 2;
                }
            }
            else
            {
                H89IO.Port174Q[1] &= 0x7F;  //  reset transmit buffer empty flag
                bH17WriteGate = false;
                bH17Write = false;
            }
            //  0001.0000 = motor on
            if ((value & 0x10) != 0)
            {
                bH17MotorOn = true;
            }
            else
            {
                bH17MotorOn = false;
                H17WriteState = 0;
            }
            //  0000.0010 = drive select 0
            if ((value & 0x02) != 0)
            {
                H17DriveSelect |= 1;
                H17Drive = 0;
            }
            //  0000.0100 = drive select 1
            if ((value & 0x04) != 0)
            {
                H17DriveSelect |= 2;
                H17Drive = 1;
            }
            //  0000.1000 = drive select 2
            if ((value & 0x08) != 0)
            {
                H17DriveSelect |= 4;
                H17Drive = 2;
            }

            if (bH17MotorOn && H17DriveSelect != 0)
            {
                if (H17Drive != 0)  //  if no volume is loaded, create an empty one
                {
                    if (DiskImage[H17Drive].DiskImageBuffer == null)
                    {
                        int drive_type = 0;
                        int image_size = MaxImageSize;
                        if (H17Drive == 0)
                        {
                            drive_type = comboBox3.SelectedIndex;
                        }
                        else if (H17Drive == 1)
                        {
                            drive_type = comboBox4.SelectedIndex;
                        }
                        else if (H17Drive == 2)
                        {
                            drive_type = comboBox5.SelectedIndex;
                        }
                        switch (drive_type)
                        {
                            case 0:
                                DiskImage[H17Drive].Tracks = 40;
                                DiskImage[H17Drive].Sides = 1;
                                break;
                            case 1:
                                DiskImage[H17Drive].Tracks = 40;
                                DiskImage[H17Drive].Sides = 2;
                                break;
                            case 2:
                                DiskImage[H17Drive].Tracks = 80;
                                DiskImage[H17Drive].Sides = 1;
                                break;
                            case 3:
                                DiskImage[H17Drive].Tracks = 80;
                                DiskImage[H17Drive].Sides = 2;
                                break;
                        }
                        image_size = (DiskImage[H17Drive].Tracks * DiskImage[H17Drive].Sides * 2560);
                        DiskImage[H17Drive].DiskImageBuffer = new byte[image_size];
                        if (H17Drive == 1)
                        {
                            label2.Text = string.Format("BLANK {0}K BYTES", image_size / 1024);
                            this.Refresh();
                        }
                        else
                        {
                            label3.Text = string.Format("BLANK {0}K BYTES", image_size / 1024);
                            this.Refresh();
                        }
                    }
                }
            }
            else
            {
                H17DriveSelect = 0;
                H17WriteSector = 0;
            }
            //  0010.0000 = head step direction (0 = out)
            if ((value & 0x20) != 0)
            {
                H17Direction = 1;
            }
            else
            {
                H17Direction = 0;
            }
            //  0100.0000 = head step
            if ((value & 0x40) != 0)
            {
                if (H17Direction == 1)
                {
                    if (H17Track < DiskImage[H17Drive].Tracks)
                    {
                        if (port >= 0x78 && port <= 0x7B)
                        {
                            H89IO.Port170Q[3] &= 0x0D; //  turn off track0 flag
                        }
                        else
                        {
                            H89IO.Port174Q[3] &= 0x0D; //  turn off track0 flag
                        }
                        H17Track++;
                    }
                }
                else
                {
                    if (H17Track > 0)
                    {
                        H17Track--;
                    }
                    if (H17Track == 0)
                    {
                        if (port >= 0x78 && port <= 0x7B)
                        {
                            H89IO.Port170Q[3] |= 0x02;  //  turn on track0 flag
                        }
                        else
                        {
                            H89IO.Port174Q[3] |= 0x02;  //  turn on track0 flag
                        }
                    }
                }
            }
            //  1000.0000 = low ram write enable
            if ((value & 0x80) != 0)
            {
                bH17RamEnable = true;
            }
            else
            {
                bH17RamEnable = false;
            }
        }

        public void H17Write(ushort port, byte value)
        {
            int p = port - 0x7C;
            if (bH17WriteGate)
            {
                if (p == 0) //  port 174Q
                {
                    if (bH17Write)
                    {
                        //  write byte into disk image buffer
                        if (H17WriteState == 2)
                        {
                            H17Sector = H17WriteSector;
                        }
                        H17WriteNextByte(value);
                    }
                    else
                    {
                        if (value == 0xFD)  //  sync char turns on write mode
                        {
                            if (DiskWriteIndex != 6)
                            {
                                DiskWriteIndex = 0;
                            }
                            bH17Write = true;
                        }
                    }
                    //SendToListBox(string.Format("< {0:X2},{1:X2}", port, value));
                }
            }
            if (p == 2 && value == 0xFD)
            {
                if ((H89IO.Port174Q[3] & 0x08) == 0x00)
                {
                    H89IO.Port174Q[3] |= 0x08;  //  sync detect flag
                    DiskReadCheckSum = 0xFD;    //  reset checksum
                    DiskReadIndex = 0;          //  reset data read buffer index
                    DiskWriteIndex = 0;         //  reset data write buffer index
                    H17ReadByte(0xFD);          //  send sync byte
                    H17WriteState = 0;
                    bH17Read = true;
                }
            }
            H89IO.Port174Q[p] = value;
        }

        public bool H17DiskWriteProtect()
        {
            if (H17DriveSelect != 0)
            {
                return (bH17WriteProtect[H17Drive]);
            }
            return (false);
        }

        public void H17Service()
        {
            if (bH17MotorOn)
            {
                H17ServiceIndexHoles();
            }
        }

        public void H17ServiceIndexHoles()
        {
            if (bH17DoorOpen[H17Drive])
            {
                return;
            }
            if (bH17Hole)
            {
                H17ServiceHoleOn();
            }
            else
            {
                H17ServiceHoleOff();
            }
        }

        public void H17ServiceHoleOn()
        {
            if (H17HoleTick >= H17HoleTickMax)
            {
                H17Hole(false);
            }
        }

        public void H17ServiceHoleOff()
        {
            int delay = H17SectorTickMax;
            switch (H17Index)
            {
                case 9:
                case 10:
                    delay = H17SectorTickMax / 2;
                    break;
            }
            if (H17SectorTick >= delay)
            {
                H17NextSector();
                H17Hole(true);
                H17SectorTick = 0;
            }
        }

        public void H17NextSector()
        {
            H17Index = (H17Index + 1) % 11;
            if (H17Index == 10)
            {
                if (checkBox6.Checked)
                {
                    SendToListBox("INDEX HOLE");
                }
            }
            else
            {
                H17Sector = H17Index;
                if (H17Index == 0)
                {
                    H17Revs++;
                }
                if (checkBox6.Checked)
                {
                    SendToListBox(string.Format("SECTOR = {0}", H17Sector));
                }
            }
        }

        public void H17Hole(bool state)
        {
            if (state)
            {
                H89IO.Port174Q[3] |= 0x01;  //  set hole detect bit
                H17HoleTick = 0;
            }
            else
            {
                H89IO.Port174Q[3] &= 0x0E;  //  turn off hole detect bit
            }
            bH17Hole = state;
        }

        //  reading from an image
        public void H17ReadNextByte()
        {
            int image_index = H17Drive;
            int track = (H17Track * DiskImage[image_index].Sides) + H17Side;
            H89IO.Port174Q[1] &= 0xFE;  //  reset receive data available bit
            if (DiskImage[image_index].DiskImageBuffer != null)
            {
                byte c = 0x00;
                switch (DiskReadIndex)
                {
                    case 1: //  volume number
                        if (track == 0)
                        {
                            c = 0x00;
                        }
                        else
                        {
                            c = (byte)H17DiskVolume[image_index];
                        }
                        if (checkBox6.Checked)
                        {
                            SendToListBox(string.Format("? Track {0:D2}/{1}/{2:D3} Sector {3}", H17Track, H17Side, track, H17Sector));
                        }
                        break;
                    case 2: //  track number
                        c = (byte)track;
                        break;
                    case 3:
                        c = (byte)H17Sector;
                        break;
                    case 4:
                        c = (byte)DiskReadCheckSum;
                        break;
                    case 5:
                        c = 0xFD;
                        DiskReadCheckSum = c;
                        H89IO.Port174Q[3] |= 0x08;
                        break;
                    case 262:
                        c = (byte)DiskReadCheckSum;
                        break;
                    case 263:
                        if (checkBox7.Checked)
                        {
                            SendToListBox(string.Format("< Track {0:D2}/{1}/{2:D3} Sector {3} read", H17Track, H17Side, track, H17Sector));
                        }
                        DiskReadIndex = 0;
                        bH17Read = false;
                        //if (H17Track == 5 && H17Sector == 1)
                        //{
                        //    BreakPoint();
                        //}
                        return;
                    default:
                        {
                            int index = H17GetImageOffset(image_index, H17Side, H17Track, H17Sector);
                            int n = index + (DiskReadIndex - 6);
                            c = DiskImage[image_index].DiskImageBuffer[n];
                        }
                        break;
                }
                H17ReadByte(c);
            }
        }

        public int H17GetImageOffset(int drive, int side, int track, int sector)
        {
            int sides = DiskImage[drive].Sides;
            int offset = (track * sides * 2560) + (side * 2560) + (sector * 256);
            return (offset);
        }

        public void H17ReadByte(byte c)
        {
            H89IO.Port174Q[0] = c;
            DiskReadCheckSum ^= c;
            DiskReadCheckSum <<= 1;
            if ((DiskReadCheckSum & 0x0100) != 0)   //  went through the carry-bit, wrap back to bit 1
            {
                DiskReadCheckSum = (DiskReadCheckSum & 0xFF) | 0x01;
            }
            H89IO.Port174Q[1] |= 0x01;  //  receive data available
            DiskReadIndex++;    //  index for bytes being read from the disk image
            DiskWriteIndex++;     //  index for bytes being written to the disk image
        }

        //  writing to an image
        //  getting bytes from the H17 device for an image
        public void H17WriteNextByte(byte c)
        {
            int track = (H17Track * DiskImage[H17Drive].Sides) + H17Side;
            if (DiskWriteIndex >= 256 + 6)
            {
                bDiskImageDirty[H17Drive] = true;
                if (checkBox7.Checked)
                {
                    SendToListBox(string.Format("> Track {0:D2}/{1}/{2:D3} Sector {3} write", H17Track, H17Side, track, H17Sector));
                }
                UpdateDiskButtons();
                DiskWriteIndex = 0;
                H17WriteSector = (H17WriteSector + 1) % 10;
                //H17Sector = (H17Sector + 1) % 10;
                //H17Index = H17Sector;
                return;
            }
            else if (DiskWriteIndex < 6)
            {
                if (DiskWriteIndex == 0)
                {
                    if (H17Track > 0)
                    {
                        if (c != 0)
                        {
                            H17DiskVolume[H17Drive] = c;
                        }
                    }
                }
                DiskWriteIndex++;
                if (DiskWriteIndex == 6)
                {
                    bH17Write = false;
                }
                return;
            }
            H17GetByte(c);
        }

        public void H17GetByte(byte c)
        {
            int image_index = H17Drive;
            int index = H17GetImageOffset(image_index, H17Side, H17Track, H17Sector);
            int n = index + (DiskWriteIndex - 6);
            if (n < DiskImage[image_index].DiskImageBuffer.Length)
            {
                DiskImage[image_index].DiskImageBuffer[n] = c;
                DiskWriteIndex++;
            }
        }

        public byte H17ReadByte(int drive, int side, int track, int sector, int offset)
        {
            int index = H17GetImageOffset(drive, side, track, sector) + offset;
            if (index < DiskImage[drive].DiskImageBuffer.Length)
            {
                byte c = DiskImage[drive].DiskImageBuffer[index];
                return (c);
            }
            return (0);
        }

        public void BreakPoint()
        {
            bBreak = true;
            H89Z80.StatementsToFetch = 1;
        }

        public void H19Init()
        {
            H19RenderSurface = new H19Panel();
            H19RenderSurface.Text = "H89 Emulator Output";
            H19RenderSurface.ClientSize = new Size(900, 512);
            H19RenderSurface.CausesValidation = false;
            H19RenderSurface.Visible = true;

            try
            {
                H19RenderParams = new PresentationParameters();
                H19RenderParams.BackBufferHeight = H19RenderSurface.ClientSize.Height;
                H19RenderParams.BackBufferWidth = H19RenderSurface.ClientSize.Width;
                H19RenderParams.DeviceWindowHandle = H19RenderSurface.Handle;
                H19RenderParams.IsFullScreen = false;
                H19RenderDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, DeviceType.Hardware, H19RenderSurface.Handle, H19RenderParams);
            }
            catch (DeviceNotSupportedException e)
            {
                MessageBox.Show(string.Format("{0}", e.Message));
                Close();
                return;
            }

            H19FontSprite = new SpriteBatch(H19RenderDevice);

            H19RenderSurface.PreviewKeyDown += new PreviewKeyDownEventHandler(panel1_PreviewKeyDown);
            H19RenderSurface.Paint += new PaintEventHandler(H19Form_Paint);
            H19RenderSurface.GotFocus += new EventHandler(H19RenderSurface_GotFocus);
            H19RenderSurface.LostFocus += new EventHandler(H19RenderSurface_LostFocus);

            H19RenderSurface.FormClosing += new FormClosingEventHandler(H19RenderSurface_FormClosing);

            H19LoadFont();
            H19Clear();

            bH19Init = true;
        }

        void H19RenderSurface_FormClosing(object sender, FormClosingEventArgs e)
        {
            button6_Click(null, null);
            bH19Init = false;
        }

        public void H19LoadFont()
        {
            BinaryReader reader = new BinaryReader(File.Open(string.Format("{0}\\ROMS\\2716_444-29_H19FONT.BIN", Application.StartupPath), FileMode.Open));
            for (int i = 0; i < 128; i++)
            {
                System.Drawing.Bitmap nor = new System.Drawing.Bitmap(H19FontWidth, H19FontHeight);
                System.Drawing.Bitmap rev = new System.Drawing.Bitmap(H19FontWidth, H19FontHeight);
                for (int j = 0; j < H19FontHeight; j += H19FontHeightStep)
                {
                    byte c = reader.ReadByte();
                    int bit = 0x80;
                    for (int k = 0; k < H19FontWidth; k++)
                    {
                        if ((c & bit) != 0)
                        {
                            nor.SetPixel(k, j, H19ScreenColor);
                            rev.SetPixel(k, j, System.Drawing.Color.Black);
                        }
                        else
                        {
                            nor.SetPixel(k, j, System.Drawing.Color.Black);
                            rev.SetPixel(k, j, H19ScreenColor);
                        }
                        bit >>= 1;
                    }
                }
                
                Rectangle rect = new Rectangle(0, 0, nor.Width, nor.Height);
                System.Drawing.Imaging.BitmapData bitmap_data = nor.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, nor.PixelFormat);
                int count = bitmap_data.Stride * nor.Height;
                byte[] nor_bytes = new byte[count];
                Marshal.Copy(bitmap_data.Scan0, nor_bytes, 0, count);
                nor.UnlockBits(bitmap_data);
                H19Font[i] = new Texture2D(H19RenderDevice, H19FontWidth, H19FontHeight, 1, TextureUsage.None, H19RenderDevice.PresentationParameters.BackBufferFormat);
                H19Font[i].SetData<byte>(nor_bytes);

                bitmap_data = rev.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, rev.PixelFormat);
                Marshal.Copy(bitmap_data.Scan0, nor_bytes, 0, count);
                rev.UnlockBits(bitmap_data);
                H19Font[i + 128] = new Texture2D(H19RenderDevice, H19FontWidth, H19FontHeight, 1, TextureUsage.None, H19RenderDevice.PresentationParameters.BackBufferFormat);
                H19Font[i + 128].SetData<byte>(nor_bytes);
            }
            reader.Close();
        }

        public void H19Update(byte c)
        {
            //  non-printable special characters
            if (c < ' ' || c > '~')
            {
                //SendToListBox(string.Format("H19 C={0}", c));
                if (c == 0x1B) //  escape sequence coming
                {
                    bEsc = true;
                }
                else if (c == 0x07)
                {
                    SystemSounds.Beep.Play();
                }
                else if (c == 0x08) //  backspace
                {
                    if (H19ScreenPos.X > 0)
                    {
                        H19ScreenPos.X--;
                    }
                }
                else if (c == 0x09) //  tab
                {
                    H19ScreenPos.X += 8;
                    H19ScreenPos.X -= (H19ScreenPos.X % 8);
                    if (H19ScreenPos.X > 79)
                    {
                        H19ScreenPos.X = 79;
                    }
                }
                else if (c == 0x0A) //  linefeed
                {
                    H19ScreenPos.Y++;
                    if (H19ScreenPos.Y > 23)
                    {
                        H19ScrollUp();
                        H19ScreenPos.Y = 23;
                    }
                }
                else if (c == 0x0D) //  carriage return
                {
                    H19ScreenPos.X = 0;
                }
                H19Paint();
                return;
            }
            //  processing an ESC sequence
            if (bEsc)
            {
                if (bH19CursorAddressing)
                {
                    if (H19State == 0)
                    {
                        H19ScreenPos.Y = (c - ' ');
                        if (H19ScreenPos.Y < 0)
                        {
                            H19ScreenPos.Y = 0;
                        }
                        else if (H19ScreenPos.Y > 24)
                        {
                            H19ScreenPos.Y = 24;
                        }
                        H19State++;
                    }
                    else
                    {
                        H19ScreenPos.X = (c - ' ');
                        if (H19ScreenPos.X < 0)
                        {
                            H19ScreenPos.X = 0;
                        }
                        else if (H19ScreenPos.X > 79)
                        {
                            H19ScreenPos.X = 79;
                        }
                        bH19CursorAddressing = false;
                        bEsc = false;
                    }
                    H19Paint();
                    return;
                }
                else if (bH19SetMode)
                {
                    switch ((char)c)
                    {
                        case '1':
                            bH19Line25 = true;
                            break;
                        case '4':
                            bH19BlockCursor = true;
                            break;
                        case '5':
                            bH19CursorOn = false;
                            break;
                        case '6':
                            bH19KeypadShift = true;
                            break;
                        default:
                            SendToListBox(string.Format("H19SET {0} UNDEFINED", c));
                            break;
                    }
                    bH19SetMode = false;
                    bEsc = false;
                }
                else if (bH19ResetMode)
                {
                    switch ((char)c)
                    {
                        case '1':
                            bH19Line25 = false;
                            break;
                        case '4':
                            bH19BlockCursor = false;
                            break;
                        case '5':
                            bH19CursorOn = true;
                            break;
                        case '6':
                            bH19KeypadShift = false;
                            break;
                        default:
                            SendToListBox(string.Format("H19RESET {0} UNDEFINED", c));
                            break;
                    }
                    bH19ResetMode = false;
                    bEsc = false;
                }
                switch ((char)c)
                {
                    case 'E':
                        H19Clear();
                        bEsc = false;
                        break;
                    case 'H':
                        H19ScreenPos.X = 0;
                        H19ScreenPos.Y = 0;
                        bEsc = false;
                        break;
                    case 'C':
                        if (H19ScreenPos.X < 79)
                        {
                            H19ScreenPos.X++;
                        }
                        bEsc = false;
                        break;
                    case 'D':
                        if (H19ScreenPos.X > 0)
                        {
                            H19ScreenPos.X--;
                        }
                        bEsc = false;
                        break;
                    case 'B':
                        if (H19ScreenPos.Y < 23)
                        {
                            H19ScreenPos.Y++;
                        }
                        bEsc = false;
                        break;
                    case 'A':
                        if (H19ScreenPos.Y > 0)
                        {
                            H19ScreenPos.Y--;
                        }
                        bEsc = false;
                        break;
                    case 'I':
                        if (H19ScreenPos.Y > 0)
                        {
                            H19ScreenPos.Y--;
                        }
                        else
                        {
                            H19ScrollDown();
                        }
                        bEsc = false;
                        break;
                    case 'n':
                        KeyboardCharInsert((char)0x1B);
                        KeyboardCharInsert('Y');
                        KeyboardCharInsert((char)(' ' + H19ScreenPos.Y));
                        KeyboardCharInsert((char)(' ' + H19ScreenPos.X));
                        bKeyboardInterrupt = true;
                        bEsc = false;
                        break;
                    case 'j':
                        H19SavedPos = H19ScreenPos;
                        bEsc = false;
                        break;
                    case 'k':
                        H19ScreenPos = H19SavedPos;
                        bEsc = false;
                        break;
                    case 'Y':
                        bH19CursorAddressing = true;
                        H19State = 0;
                        break;
                    case 'F':
                        bH19GraphicsMode = true;
                        bEsc = false;
                        break;
                    case 'G':
                        bH19GraphicsMode = false;
                        bEsc = false;
                        break;
                    case 'J':
                        H19EraseToEndOfPage();
                        bEsc = false;
                        break;
                    case 'p':
                        bH19ReverseVideo = true;
                        bEsc = false;
                        break;
                    case 'q':
                        bH19ReverseVideo = false;
                        bEsc = false;
                        break;
                    case 'x':
                        bH19SetMode = true;
                        H19State = 0;
                        break;
                    case 'y':
                        bH19ResetMode = true;
                        H19State = 0;
                        break;
                    case 'N':
                        H19DeleteChar();
                        bEsc = false;
                        break;
                    case 'z':
                        H19Clear();
                        bH19BlockCursor = false;
                        bH19CursorAddressing = false;
                        bH19CursorOn = true;
                        bH19GraphicsMode = false;
                        bH19Line25 = false;
                        bH19ResetMode = false;
                        bH19ReverseVideo = false;
                        bH19SetMode = false;
                        bEsc = false;
                        break;
                    case 'l':
                        H19EraseCell(0, H19ScreenPos.Y, 79, H19ScreenPos.Y);
                        bEsc = false;
                        break;
                    default:
                        SendToListBox(string.Format("ESC {0} UNDEFINED", c));
                        bEsc = false;
                        break;
                }
                H19Paint();
                return;
            }

            H19DrawCell(H19ScreenPos.X, H19ScreenPos.Y, c);
            if (H19ScreenPos.X < 79)
            {
                H19ScreenPos.X++;
            }
        }

        public void H19DrawCell(int cell_x, int cell_y, int i)
        {
            if (i >= 0 && i < 128)
            {
                if (bH19GraphicsMode)
                {
                    if (i >= 94)
                    {
                        if (i == 94)
                        {
                            i = 127;
                        }
                        else if (i == 95)
                        {
                            i = 31;
                        }
                        else
                        {
                            i -= 96;
                            if (i < 0)
                            {
                                i = 0;
                            }
                        }
                    }
                }
                if (bH19ReverseVideo)
                {
                    i += 128;
                }
                H19Cell[cell_x, cell_y] = (byte)i;
                H19Paint();
            }
        }

        public void H19RedrawCells()
        {
            for (int j = 0; j < 25; j++)
            {
                for (int i = 0; i < 80; i++)
                {
                    H19DrawCell(i, j, H19Cell[i, j]);
                }
            }
        }

        public void H19EraseCell(int cell_x1, int cell_y1, int cell_x2, int cell_y2)
        {
            int lines = (cell_y2 - cell_y1) + 1;
            int chars = (cell_x2 - cell_x1) + 1;
            for (int j = 0; j < lines; j++)
            {
                for (int i = 0; i < chars; i++)
                {
                    H19Cell[cell_x1 + i, cell_y1 + j] = (byte)' ';
                }
            }
        }

        public void H19EraseToEndOfLine()
        {
            H19EraseCell(H19ScreenPos.X, H19ScreenPos.Y, 79 - H19ScreenPos.X, H19ScreenPos.Y);
        }

        public void H19EraseToEndOfPage()
        {
            H19EraseToEndOfLine();
            if (H19ScreenPos.Y < 23)
            {
                H19EraseCell(0, H19ScreenPos.Y + 1, 79, 23);
            }
        }

        public void H19Clear()
        {
            for (int j = 0; j < 25; j++)
            {
                for (int i = 0; i < 80; i++)
                {
                    H19Cell[i, j] = (byte)' ';
                }
            }

            H19ScreenPos.X = 0;
            H19ScreenPos.Y = 0;
            H19Paint();

            /*  charset test
            for (int i = 0; i < 64; i++)
            {
                H19DrawCell(i, 20, i);
            }
            for (int i = 0; i < 64; i++)
            {
                H19DrawCell(i, 21, i + 64);
            }
            */
        }

        public void H19ScrollUp()
        {
            for (int j = 0; j < 23; j++)
            {
                for (int i = 0; i < 80; i++)
                {
                    H19Cell[i, j] = H19Cell[i, j + 1];
                }
            }
            for (int i = 0; i < 80; i++)
            {
                H19Cell[i, 23] = (byte)' ';
            }
        }

        public void H19ScrollDown()
        {
            for (int j = 23; j > 0; j--)
            {
                for (int i = 0; i < 80; i++)
                {
                    H19Cell[i, j] = H19Cell[i, j - 1];
                }
            }
            for (int i = 0; i < 80; i++)
            {
                H19Cell[i, 0] = (byte)' ';
            }
        }

        public void H19ScrollLeft()
        {
            for (int j = 0; j < 23; j++)
            {
                for (int i = 0; i < 79; i++)
                {
                    H19Cell[i, j] = H19Cell[i + 1, j];
                }
            }
            for (int j = 0; j < 23; j++)
            {
                H19Cell[79, j] = (byte)' ';
            }
        }

        public void H19ScrollRight()
        {
            for (int j = 0; j < 23; j++)
            {
                for (int i = 79; i > 0; i++)
                {
                    H19Cell[i, j] = H19Cell[i - 1, j];
                }
            }
            for (int j = 0; j < 23; j++)
            {
                H19Cell[0, j] = (byte)' ';
            }
        }

        private void H19DeleteChar()
        {
            int j = H19ScreenPos.Y;
            for (int i = 0; i < 79; i++)
            {
                H19Cell[i, j] = H19Cell[i + 1, j];
            }
            H19Cell[79, j] = (byte)' ';
        }

        private void H19Paint()
        {
            H19RenderSurface.Invalidate();
        }

        void H19Form_Paint(object sender, PaintEventArgs e)
        {
            if (H19RenderDevice == null)
            {
                return;
            }

            switch (H19RenderDevice.GraphicsDeviceStatus)
            {
                case GraphicsDeviceStatus.Lost:
                    return;
                    break;
                case GraphicsDeviceStatus.NotReset:
                    H19RenderDevice.Reset(H19RenderParams);
                    return;
                    break;
            }

            Microsoft.Xna.Framework.Graphics.Color color = new Microsoft.Xna.Framework.Graphics.Color(H19ScreenColor.R, H19ScreenColor.G, H19ScreenColor.B);
            Microsoft.Xna.Framework.Vector2 p = Microsoft.Xna.Framework.Vector2.Zero;

            H19RenderDevice.Clear(Microsoft.Xna.Framework.Graphics.Color.Black);

            Microsoft.Xna.Framework.Matrix scale = Microsoft.Xna.Framework.Matrix.CreateScale(H19Scale.X, H19Scale.Y, 1);
            H19FontSprite.Begin(SpriteBlendMode.None, SpriteSortMode.Deferred, SaveStateMode.None, scale);
            for (int j = 0; j < 25; j++)
            {
                for (int i = 0; i < 80; i++)
                {
                    int c = H19Cell[i, j];
                    p.X = i * H19ScreenCellWidth;
                    p.Y = j * H19ScreenCellHeight;
                    H19FontSprite.Draw(H19Font[c], p, color);
                }
            }
            if (bH19CursorOn)
            {
                if (bH19CursorVis)
                {
                    int i = (126 - 3) - 96;
                    if (bH19BlockCursor)
                    {
                        i += 128;
                    }
                    p.X = H19ScreenPos.X * H19ScreenCellWidth;
                    p.Y = H19ScreenPos.Y * H19ScreenCellHeight;
                    H19FontSprite.Draw(H19Font[i], p, color);
                }
            }
            H19FontSprite.End();

            H19RenderDevice.Present();
        }

        //  RUN/STOP button
        private void button4_Click(object sender, EventArgs e)
        {
            if (bRunning)
            {
                BreakPoint();
                return;
            }

            bRunning = true;
            button4.Text = "STOP";
            GlobalTick = Environment.TickCount & Int32.MaxValue;
            UpdateTick = GlobalTick;
            SetFocus();
            H89Z80.event_next_event = int.MaxValue;
            H89Z80.Execute();
            button4.Text = "RUN";
            bRunning = false;
            if (bReset)
            {
                ResetSystem();
                bReset = false;
            }
            if (bLoadROM)
            {
                ResetSystem();
                bLoadROM = false;
            }
            else if (bBreak)
            {
                SendToListBox("* Break *");
                bBreak = false;
            }
            UpdateStats();
        }

        private void ResetSystem()
        {
            H89Z80.Reset();
            H89IO.ResetPorts();
            BreakPointAddress = 0;
            textBox1.Text = "000.000";
            LoadROM();
            //  reset H17 flags
            bH17Read = false;
            bH17RamEnable = false;
            bH17WriteGate = false;
            bH17Write = false;
            bH17Hole = false;
            H17Revs = 0;
            H17Index = 0;
            H17Sector = 0;
            H17Side = 0;
            H17Track = 0;
            H17DriveSelect = 0;
            H17HoleTick = 0;
            H17SectorTick = 0;
            //  reset H19 flags
            bH19CursorAddressing = false;
            bH19GraphicsMode = false;
            bH19ReverseVideo = false;
            bH19CursorOn = true;
            bKeyboardInterrupt = false;
            //
            bReset = false;
            UpdateStats();
            H19Clear();
        }

        private void UpdateStats()
        {
            label4.Text = string.Format("SEC {0:D2}", H17Sector);
            label5.Text = string.Format("PC: {0}", ToOctalAdr(H89Z80.Status.PC));
            label6.Text = string.Format("SP: {0}", ToOctalAdr(H89Z80.Status.RegisterSP.w));
            label7.Text = string.Format("HL: {0}", ToOctalAdr(H89Z80.Status.RegisterHL.w));
            label8.Text = string.Format("BC: {0}", ToOctalAdr(H89Z80.Status.RegisterBC.w));
            label9.Text = string.Format("DE: {0}", ToOctalAdr(H89Z80.Status.RegisterDE.w));
            label10.Text = string.Format("AF: {0}", ToOctalAdr(H89Z80.Status.RegisterAF.w));
            label11.Text = string.Format("(M) {0}", ToOctal((ushort)H89MEM.ReadByte(H89Z80.Status.RegisterHL.w)));
            label12.Text = string.Format("TIC {0}", ToOctalAdr(H89MEM.ReadWord(0x201B)));
            label13.Text = string.Format("IM: {0}", H89Z80.Status.IM);
            label14.Text = string.Format("TRK {0:D2}/{1}", H17Track, H17Side);
            label15.Text = (bH17MotorOn) ? "MON 1" : "MON 0";
            label16.Text = string.Format("DRV {0}", H17Drive);
            label17.Text = string.Format("HOL {0}", bH17Hole ? 1 : 0);
            label18.Text = string.Format("DIR {0}", H17Direction);
            int tr0 = H89IO.Port174Q[3] & 0x02;
            label19.Text = string.Format("TR0 {0:D3}", tr0 != 0 ? 1 : 0);
            label20.Text = string.Format("REV {0}", H17Revs);
            label21.Text = string.Format("VOL {0:D3}", H17DiskVolume[H17Drive]);
            label22.Text = string.Format("BYTES {0:D3}", DiskReadIndex);
            label23.Text = string.Format("STT {0}", H17WriteState);
            label27.Text = string.Format("IDX {0:D2}", H17Index);
        }

        private void LoadROM()
        {
            //  load MTR ROM
            // dcp add
            Stream stream1 = new FileStream(string.Format("{0}\\ROMS\\"+comboBox1.Text, Application.StartupPath), FileMode.Open);
            stream1.Read(H89MEM.Raw, 0x0000, (int)stream1.Length);
            stream1.Close();
            /* dcp remove
             if (comboBox1.SelectedIndex == 0)
            {
                Stream stream = new FileStream(string.Format("{0}\\ROMS\\2716_444-62_MTR89.BIN", Application.StartupPath), FileMode.Open);
                stream.Read(H89MEM.Raw, 0x0000, (int)stream.Length);
                stream.Close();
            }
            else if (comboBox1.SelectedIndex == 1)
            {
                Stream stream = new FileStream(string.Format("{0}\\ROMS\\2732_444-142_MTR90A.ROM", Application.StartupPath), FileMode.Open);
                stream.Read(H89MEM.Raw, 0x0000, (int)stream.Length);
                stream.Close();
            }
            else if (comboBox1.SelectedIndex == 2)
            {
                Stream stream = new FileStream(string.Format("{0}\\ROMS\\2732_mttest2.bin", Application.StartupPath), FileMode.Open);
                stream.Read(H89MEM.Raw, 0x0000, (int)stream.Length);
                stream.Close();
            }
            */
            //  load H-17 ROM
            {
                Stream stream = new FileStream(string.Format("{0}\\ROMS\\2716_444-19_H17.ROM", Application.StartupPath), FileMode.Open);
                stream.Read(H89MEM.Raw, 0x1800, (int)stream.Length);
                stream.Close();
            }
        }

        //private void SystemTick()
        //{
        //    H89Z80.event_next_event = H89Z80.tstates + 1;
        //    H89Z80.Execute();
        //}

        //private void ClockInterrupt()
        //{
        //    H89Z80.Status.I = 1;
        //    H89Z80.Interrupt();
        //}

        //  STEP button clicked
        private void button5_Click(object sender, EventArgs e)
        {
            bRunning = true;
            button4.Text = "RUN";
            H89Z80.StatementsToFetch = 1;
            H89Z80.event_next_event = H89Z80.tstates + 1;
            H89Z80.Execute();
            DisassembleNextStatement(H89Z80.Status.PC, 1);
            bRunning = false;
            UpdateStats();
        }

        //  RESET button clicked
        private void button6_Click(object sender, EventArgs e)
        {
            button4.Text = "RUN";
            H19Clear();
            if (bRunning)
            {
                bRunning = false;
                bReset = true;
            }
            else
            {
                ResetSystem();
            }
        }

        //  New ROM selected
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button4.Text = "RUN";
            H19Clear();
            if (bRunning)
            {
                bRunning = false;
                bLoadROM = true;
            }
            else
            {
                ResetSystem();
            }
        }

        private void UpdateDiskButtons()
        {
            if (bDiskImageDirty[0])
            {
                button1.BackColor = System.Drawing.Color.Yellow;
            }
            else
            {
                button1.BackColor = SystemColors.Control;
            }
            if (bDiskImageDirty[1])
            {
                button2.BackColor = System.Drawing.Color.Yellow;
            }
            else
            {
                button2.BackColor = SystemColors.Control;
            }
            if (bDiskImageDirty[2])
            {
                button3.BackColor = System.Drawing.Color.Yellow;
            }
            else
            {
                button3.BackColor = SystemColors.Control;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (bDiskImageDirty[0])
            {
                if (SaveDiskImage(0) == false)
                {
                    return;
                }
                bDiskImageDirty[0] = false;
                UpdateDiskButtons();
                return;
            }
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                label1.Text = openFileDialog1.SafeFileName.ToUpper();
                BinaryReader reader = new BinaryReader(File.Open(openFileDialog1.FileName, FileMode.Open));
                DiskImage[0].DiskImageBuffer = new byte[reader.BaseStream.Length];
                reader.Read(DiskImage[0].DiskImageBuffer, 0, (int)reader.BaseStream.Length);
                reader.Close();
                SetDiskVolume(0);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (bDiskImageDirty[1])
            {
                if (SaveDiskImage(1) == false)
                {
                    return;
                }
                bDiskImageDirty[1] = false;
                UpdateDiskButtons();
                return;
            }
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                label2.Text = openFileDialog1.SafeFileName.ToUpper();
                BinaryReader reader = new BinaryReader(File.Open(openFileDialog1.FileName, FileMode.Open));
                DiskImage[1].DiskImageBuffer = new byte[reader.BaseStream.Length];
                reader.Read(DiskImage[1].DiskImageBuffer, 0, (int)reader.BaseStream.Length);
                reader.Close();
                SetDiskVolume(1);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (bDiskImageDirty[2])
            {
                if (SaveDiskImage(2) == false)
                {
                    return;
                }
                bDiskImageDirty[2] = false;
                UpdateDiskButtons();
                return;
            }
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                label3.Text = openFileDialog1.SafeFileName.ToUpper();
                BinaryReader reader = new BinaryReader(File.Open(openFileDialog1.FileName, FileMode.Open));
                DiskImage[2].DiskImageBuffer = new byte[reader.BaseStream.Length];
                reader.Read(DiskImage[2].DiskImageBuffer, 0, (int)reader.BaseStream.Length);
                reader.Close();
                SetDiskVolume(2);
            }
        }

        private bool SaveDiskImage(int image_index)
        {
            DialogResult r = MessageBox.Show(string.Format("Save currently loaded image in SY{0}?", image_index), "SAVE", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Yes)
            {
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    BinaryWriter writer = new BinaryWriter(File.Open(saveFileDialog1.FileName, FileMode.Create));
                    int length = DiskImage[image_index].Tracks * DiskImage[image_index].Sides * 2560;
                    if (length == 0)
                    {
                        length = 40 * 2560;
                    }
                    writer.Write(DiskImage[image_index].DiskImageBuffer, 0, length);
                    writer.Close();

                    //MessageBox.Show(string.Format("Disk image for SY{0} saved successfully.", image_index), "DISK SAVED");
                    return (true);
                }
                else
                {
                    return (false);
                }
            }
            else if (r == DialogResult.No)
            {
                return (true);
            }
            return (false);
        }

        private void SetDiskVolume(int image_index)
        {
            byte vol_flags = 0;
            H17DiskVolume[image_index] = 0x00;
                //  check for HDOS disk image
            if (Form2.IsHDOSDisk(DiskImage[image_index].DiskImageBuffer))
            {
                H17DiskVolume[image_index] = (byte)DiskImage[image_index].DiskImageBuffer[0x900];
                vol_flags = DiskImage[image_index].DiskImageBuffer[0x910];
                switch (vol_flags)
                {
                    case 0:
                        DiskImage[image_index].Tracks = 40;
                        DiskImage[image_index].Sides = 1;
                        break;
                    case 1:
                        DiskImage[image_index].Tracks = 40;
                        DiskImage[image_index].Sides = 2;
                        break;
                    case 2:
                        DiskImage[image_index].Tracks = 80;
                        DiskImage[image_index].Sides = 1;
                        break;
                    case 3:
                        DiskImage[image_index].Tracks = 80;
                        DiskImage[image_index].Sides = 2;
                        break;
                    default:
                        DiskImage[image_index].Tracks = 40;
                        DiskImage[image_index].Sides = 1;
                        break;
                }
                SetDriveSpecs(image_index, vol_flags);
            }
            else
            {
                //DiskImage[image_index].Tracks = 40;
                //DiskImage[image_index].Sides = 1;
                //SetDriveSpecs(image_index, 0);
                if (DiskImage[image_index].DiskImageBuffer.Length < 200000)
                {
                    DiskImage[image_index].Tracks = 40;
                    DiskImage[image_index].Sides = 1;
                    SetDriveSpecs(image_index, 0);
                }
                else if (DiskImage[image_index].DiskImageBuffer.Length < 400000)
                {
                    DiskImage[image_index].Tracks = 40;
                    DiskImage[image_index].Sides = 2;
                    SetDriveSpecs(image_index, 2);
                }
                else
                {
                    DiskImage[image_index].Tracks = 80;
                    DiskImage[image_index].Sides = 2;
                    SetDriveSpecs(image_index, 3);
                }
            }
        }

        private void SetDriveSpecs(int image_index, byte vol_flags)
        {
            switch (image_index)
            {
                case 0:
                    comboBox3.SelectedIndex = (int)vol_flags;
                    comboBox3.Enabled = false;
                    comboBox3.Refresh();
                    break;
                case 1:
                    comboBox4.SelectedIndex = (int)vol_flags;
                    comboBox4.Enabled = false;
                    comboBox4.Refresh();
                    break;
                case 2:
                    comboBox5.SelectedIndex = (int)vol_flags;
                    comboBox5.Enabled = false;
                    comboBox5.Refresh();
                    break;
            }
        }

        private void DisassembleNextStatement(ushort address, int n)
        {
            SendToListBox("- DISASSEMBLE -");
            for (int i = 0; i < n; i++)
            {
                if (i == 0)
                {
                    SendToListBox(string.Format("{0}: {1} <<", ToOctal(address), H89Z80Disasm.Disassemble(ref address)));
                }
                else
                {
                    SendToListBox(string.Format("{0}: {1}", ToOctal(address), H89Z80Disasm.Disassemble(ref address)));
                }
            }
            DisassembleAddressNext = address;
        }

        private string ToOctalAdr(int v)
        {
            if (v > 255)
            {
                int x;
                x = (v / 256);
                int d3 = (x % 8);
                x = x / 8;
                int d4 = (x % 8);
                x = x / 8;
                int d5 = (x % 8);
                x = (v % 256);
                int d0 = (x % 8);
                x = x / 8;
                int d1 = (x % 8);
                x = x / 8;
                int d2 = (x % 8);
                return (string.Format("{0}{1}{2}.{3}{4}{5}", d5, d4, d3, d2, d1, d0));
            }
            else
            {
                int x;
                int d0 = v % 8;
                x = v / 8;
                int d1 = x % 8;
                x = x / 8;
                int d2 = x % 8;
                return (string.Format("000.{0}{1}{2}", d2, d1, d0));
            }
        }

        private string ToOctal(int v)
        {
            if (v > 255)
            {
                int x;
                x = (v / 256);
                int d3 = (x % 8);
                x = x / 8;
                int d4 = (x % 8);
                x = x / 8;
                int d5 = (x % 8);
                x = (v % 256);
                int d0 = (x % 8);
                x = x / 8;
                int d1 = (x % 8);
                x = x / 8;
                int d2 = (x % 8);
                return (string.Format("{0}{1}{2}.{3}{4}{5}", d5, d4, d3, d2, d1, d0));
            }
            else
            {
                int x;
                int d0 = v % 8;
                x = v / 8;
                int d1 = x % 8;
                x = x / 8;
                int d2 = x % 8;
                return (string.Format("{0}{1}{2}", d2, d1, d0));
            }
        }

        private ushort ToDecimal(string octal_address)
        {
            int hi = 0;
            int lo = 0;
            if (octal_address.Length == 7)
            {
                string adr_hi = octal_address.Substring(0, 3);
                string dec_hi = Converter.OctToDec.Convert(adr_hi);
                hi = int.Parse(dec_hi);
                string adr_lo = octal_address.Substring(4, 3);
                string dec_lo = Converter.OctToDec.Convert(adr_lo);
                lo = int.Parse(dec_lo);
            }
            return ((ushort)((hi * 256) + lo));
        }

        private void SendToListBox(string s)
        {
            listBox1.Items.Add(s);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        void listBox1_DoubleClick(object sender, EventArgs e)
        {
            string sel = listBox1.SelectedItem.ToString();
            if (sel.Contains(':'))
            {
                string adr = sel.Substring(0, 7);
                BreakPointAddress = ToDecimal(adr);
                textBox1.Text = ToOctal(BreakPointAddress);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
        }

        void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                BreakPointAddress = ToDecimal(textBox1.Text);
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
        }

        void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                DisassembleAddress = ToDecimal(textBox2.Text);
                DisassembleNextStatement(DisassembleAddress, 10);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (DisassembleAddress != 0)
            {
                DisassembleNextStatement(DisassembleAddressNext, 10);
            }
        }

        void SetFocus()
        {
            if (!bH19Init || H19RenderSurface == null)
            {
                H19Init();
            }
            H19RenderSurface.Focus();
        }

        void H19RenderSurface_GotFocus(object sender, EventArgs e)
        {
            button9.Text = "ONLINE";
            button9.BackColor = SystemColors.Control;
            button9.Enabled = false;
        }

        void H19RenderSurface_LostFocus(object sender, EventArgs e)
        {
            button9.Text = "OFFLINE";
            button9.BackColor = System.Drawing.Color.Red;
            button9.Enabled = true;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                H19ScreenColor = colorDialog1.Color;
                button8.BackColor = H19ScreenColor;
                H19LoadFont();
            }
            //SetFocus();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SetFocus();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (bH17DoorOpen[0])
            {
                button10.Text = "DOOR";
                bH17DoorOpen[0] = false;
            }
            else
            {
                button10.Text = "CLOSE";
                bH17DoorOpen[0] = true;
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (bH17DoorOpen[1])
            {
                button11.Text = "DOOR";
                bH17DoorOpen[1] = false;
            }
            else
            {
                button11.Text = "CLOSE";
                bH17DoorOpen[1] = true;
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (bH17DoorOpen[2])
            {
                button12.Text = "DOOR";
                bH17DoorOpen[2] = false;
            }
            else
            {
                button12.Text = "CLOSE";
                bH17DoorOpen[2] = true;
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Eject the disk in drive SY0?", "EJECT", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DiskImage[0].DiskImageBuffer = null;
                DiskImage[0].Sides = 0;
                DiskImage[0].Tracks = 0;
                comboBox3.Enabled = true;
                bDiskImageDirty[0] = false;
                UpdateDiskButtons();
                label1.Text = "Empty";
            }
        }

        private void button14_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Eject the disk in drive SY1?", "EJECT", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DiskImage[1].DiskImageBuffer = null;
                DiskImage[1].Sides = 0;
                DiskImage[1].Tracks = 0;
                comboBox4.Enabled = true;
                bDiskImageDirty[1] = false;
                UpdateDiskButtons();
                label2.Text = "Empty";
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Eject the disk in drive SY2?", "EJECT", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DiskImage[2].DiskImageBuffer = null;
                DiskImage[2].Sides = 0;
                DiskImage[2].Tracks = 0;
                comboBox5.Enabled = true;
                bDiskImageDirty[2] = false;
                UpdateDiskButtons();
                label3.Text = "Empty";
            }
        }
    }
}
