using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace H8DUtility
{
    public partial class Form2 : Form
    {
        public volatile bool bAbortTransfer;
        public bool bNoClient;
        public byte[] read_buffer;
        public int ReadTimeoutValue;

        //
        //  H89LDR HOST
        //

        public Form2()
        {
            InitializeComponent();
            CenterToParent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            string[] port_list = SerialPort.GetPortNames();
            if (port_list.Length > 0)
            {
                for (int i = 0; i < port_list.Length; i++)
                {
                    comboBox1.Items.Add(string.Format("{0}", port_list[i]));
                }
                comboBox1.SelectedIndex = 0;

                read_buffer = new byte[0xA00];   //  size of 1 track (10 - 256 byte sectors)

                serialPort1.Encoding = new UTF8Encoding();
                serialPort1.BaudRate = 9600;
                //serialPort1.BaudRate = 19200;

                ReadTimeoutValue = serialPort1.ReadTimeout;

                button4.Enabled = true;
                button5.Enabled = true;

                if (checkBox2.Checked)
                {
                    checkBox1.Enabled = false;
                    textBox2.Enabled = false;
                }
                else
                {
                    if (checkBox1.Checked)
                    {
                        textBox2.Enabled = true;
                    }
                    else
                    {
                        textBox2.Enabled = false;
                    }
                }
            }
            else
            {
                SendToListBox("No COM ports found");

                comboBox1.Enabled = false;
                checkBox1.Enabled = false;
                checkBox2.Enabled = false;
                button4.Enabled = false;
                button5.Enabled = false;
            }

            textBox5.Text = "0";

            button1.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
            bAbortTransfer = false;
            bNoClient = true;
        }

        static public bool IsHDOSDisk(byte[] track_buffer)
        {
            if ((track_buffer[0] == 0xAF && track_buffer[1] == 0xD3 && track_buffer[2] == 0x7D && track_buffer[3] == 0xCD) ||   //  V1.x
                (track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x20) ||   //  V2.x
                (track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x30) ||   //  V3.x
                (track_buffer[0] == 0xC3 && track_buffer[1] == 0x1D && track_buffer[2] == 0x24 && track_buffer[3] == 0x20))     //  V? Super-89
            {
                return (true);
            }
            return(false);
        }

        static public string GetHDOSLabel(byte[] track_buffer)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] l = new byte[60];
            for (int i = 0; i < 60; i++)
            {
                l[i] = track_buffer[0x911 + i];
            }
            string disk_label = string.Format("{0}", encoding.GetString(l, 0, 60));
            disk_label = disk_label.Trim();

            return (disk_label);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //  RECEIVE button clicked
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.CreatePrompt = false;
            saveFileDialog1.OverwritePrompt = true;
            saveFileDialog1.Filter = "H8/H89 Disk Images|*.h8d";
            saveFileDialog1.DefaultExt = "H8D";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //  open the serial port
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();

                if (!CheckClientReady())
                {
                    serialPort1.Close();
                    return;
                }

                serialPort1.ReadTimeout = SerialPort.InfiniteTimeout;

                int c;
                int volume_num;
                int volume_override = -1;

                string drive_str = comboBox2.SelectedItem.ToString();
                if (drive_str.Equals("SY0"))
                {
                    read_buffer[0] = (byte)'0';
                }
                else if (drive_str.Equals("SY1"))
                {
                    read_buffer[0] = (byte)'1';
                }
                else if (drive_str.Equals("SY2"))
                {
                    read_buffer[0] = (byte)'2';
                }
                serialPort1.Write(read_buffer, 0, 1);
                c = serialPort1.ReadByte();

                if (c == '?')
                {
                    c = '0';
                }

                SendToListBox(string.Format("Drive SY{0} selected", (char)c));

                byte disk_type = 255;
                
                read_buffer[0] = (byte)'Q';
                serialPort1.Write(read_buffer, 0, 1);
                c = serialPort1.ReadByte();
                if (c == 'Q')
                {
                    c = serialPort1.ReadByte();
                    disk_type = (byte)c;
                    SendToListBox(string.Format("Disk Query returned {0}", disk_type));
                }
                
                if (checkBox1.Checked && !checkBox2.Checked)
                {
                    read_buffer[0] = (byte)'V';
                    serialPort1.Write(read_buffer, 0, 1);

                    volume_override = int.Parse(textBox2.Text);

                    c = serialPort1.ReadByte();
                    if (c != 'V')
                    {
                        SendToListBox("!! (V) Volume verification failed !!");
                        serialPort1.Close();
                        return;
                    }
                }

                read_buffer[0] = (byte)'T';
                serialPort1.Write(read_buffer, 0, 1);
                volume_num = serialPort1.ReadByte();

                c = serialPort1.ReadByte();
                if (c != 'T')
                {
                    SendToListBox("!! (T) Volume verification failed !!");
                    serialPort1.Close();
                    return;
                }

                read_buffer[0] = (byte)'V';
                serialPort1.Write(read_buffer, 0, 1);
                if (volume_override == -1)
                {
                    read_buffer[0] = (byte)volume_num;
                }
                else
                {
                    read_buffer[0] = (byte)volume_override;
                }
                serialPort1.Write(read_buffer, 0, 1);

                c = serialPort1.ReadByte();
                if (c != 'V')
                {
                    SendToListBox("!! Volume assignment failed !!");
                    serialPort1.Close();
                    return;
                }

                SendToListBox(string.Format("Saving File: {0}", saveFileDialog1.FileName));
                SendToListBox(string.Format("Disk Volume #{0}", volume_num));
                if (volume_override != -1)
                {
                    SendToListBox(string.Format("Volume Override #{0}", volume_override));
                }
                //textBox2.Text = string.Format("{0}", volume_num.ToString());
                //SendToListBox(string.Format("Disk Sides {0}", (checkBox3.Checked) ? 2 : 1));
                //SendToListBox(string.Format("Disk Tracks {0}", (checkBox4.Checked) ? 80 : 40));

                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = true;
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                comboBox1.Enabled = false;

                bAbortTransfer = false;

                if (checkBox3.Checked || checkBox4.Checked)
                {
                    //  setup the disk parameters (1 or 2 sided, 40 or 80 track)
                    if (!checkBox3.Checked && !checkBox4.Checked)
                    {
                        disk_type = 0;
                        read_buffer[0] = (byte)'4';
                    }
                    else if (checkBox3.Checked && !checkBox4.Checked)
                    {
                        disk_type = 1;
                        read_buffer[0] = (byte)'5';
                    }
                    else if (!checkBox3.Checked && checkBox4.Checked)
                    {
                        disk_type = 2;
                        read_buffer[0] = (byte)'6';
                    }
                    else if (checkBox3.Checked && checkBox4.Checked)
                    {
                        disk_type = 3;
                        read_buffer[0] = (byte)'7';
                    }
                    serialPort1.Write(read_buffer, 0, 1);
                    c = serialPort1.ReadByte();
                    if (c >= '4' && c <= '7')
                    {
                        SendToListBox(string.Format("Disktype {0} set on client", c - '4'));
                    }
                }

                //  open stream
                BinaryWriter write_stream = new BinaryWriter(File.Create(saveFileDialog1.FileName));
                //  read serial port data

                read_buffer[0] = (byte)'R';
                serialPort1.Write(read_buffer, 0, 1);

                int total_tracks = 40;
                int target_tracks = total_tracks;
                int total_sides = 1;
                int track_size = 0xA00;
				bool bSide2 = false;

                if (disk_type != 255)
                {
                    if (disk_type == 0)
                    {
                        // 40 track 1 side
                        total_tracks = 40;
                        target_tracks = 40;
                        total_sides = 1;
                    }
                    else if (disk_type == 1)
                    {
                        // 40 track 2 sides
                        total_tracks = 80;
                        target_tracks = 40;
                        total_sides = 2;
                    }
                    else if (disk_type == 2)
                    {
                        // 80 track 1 side
                        total_tracks = 80;
                        target_tracks = 80;
                        total_sides = 1;
                    }
                    else if (disk_type == 3)
                    {
                        // 80 track 2 side
                        total_tracks = 160;
                        target_tracks = 80;
                        total_sides = 2;
                    }

                    SendToListBox(string.Format("{0} tracks {1} side(s)", target_tracks, total_sides));
                }

                for (int i = 0; i < total_tracks; i++)
                {
                    if (bAbortTransfer)
                    {
                        read_buffer[0] = (byte)'?';
                        serialPort1.Write(read_buffer, 0, 1);

                        SendToListBox("!! Transfer aborted !!");
                        break;
                    }

                    int current_track = i;
                    if (total_sides != 1)
                    {
                        if ((i % 2) != 0)
                        {
                            bSide2 = true;
                        }
                        else
                        {
                            bSide2 = false;
                        }
                        current_track = i / 2;
                    }

                    SendToListBox(string.Format("Reading track {0} side {1}", current_track, bSide2 ? 2 : 1));

                    textBox3.Text = string.Format("{0}/{1}/{2}", current_track, target_tracks - 1, bSide2 ? 2 : 1);

                    serialPort1.DiscardInBuffer();

                    read_buffer[0] = (byte)'R';
                    serialPort1.Write(read_buffer, 0, 1);

                    for (int j = 0; j < track_size; j++)
                    {
                        read_buffer[j] = (byte)serialPort1.ReadByte();
                        Application.DoEvents();
                    }

                    c = serialPort1.ReadByte();

                    if (c == 'r')
                    {
                        SendToListBox(string.Format("!! Read error on track {0} side {1} !!", i, bSide2 ? 2 : 1));
                    }
                    else if (c != 'R')
                    {
                        SendToListBox("!! Handshake error !!");
                        break;
                    }
                    else if (i == 0)
                    {
                        if (IsHDOSDisk(read_buffer))
                        {
                            textBox4.Text = GetHDOSLabel(read_buffer);
                            disk_type = read_buffer[0x910];
                        }
                        else
                        {
                            textBox4.Text = "CP/M DISK IMAGE";
                        }
                        if (disk_type == 255)
                        {
                            disk_type = 0;
                            if (disk_type == 0)
                            {
                                // 40 track 1 side
                                total_tracks = 40;
                                target_tracks = 40;
                                total_sides = 1;
                            }
                            else if (disk_type == 1)
                            {
                                // 40 track 2 sides
                                total_tracks = 80;
                                target_tracks = 40;
                                total_sides = 2;
                            }
                            else if (disk_type == 2)
                            {
                                // 80 track 1 side
                                total_tracks = 80;
                                target_tracks = 80;
                                total_sides = 1;
                            }
                            else if (disk_type == 3)
                            {
                                // 80 track 2 side
                                total_tracks = 160;
                                target_tracks = 80;
                                total_sides = 2;
                            }

                            SendToListBox(string.Format("{0} tracks {1} side(s)", target_tracks, total_sides));
                        }
                    }
                    else if (volume_num == 0)
                    {
                    }

                    write_stream.Write(read_buffer, 0, track_size);
                }

                write_stream.Close();

                serialPort1.ReadTimeout = ReadTimeoutValue;

                CheckClientReady();

                serialPort1.Close();

                SendToListBox(string.Format("File {0} saved", saveFileDialog1.FileName));
                if (volume_override == -1)
                {
                    SendToListBox(string.Format("Disk Volume #{0}", volume_num));
                }
                else
                {
                    SendToListBox(string.Format("Disk Volume #{0} - Override Volume #{1}", volume_num, volume_override));
                }

                SystemSounds.Beep.Play();

                SendToListBox("Done");
                textBox3.Text = "Done";

                comboBox1.Enabled = true;
                button6.Enabled = true;
                button5.Enabled = true;
                button4.Enabled = true;
                button3.Enabled = false;
                button2.Enabled = true;
                button1.Enabled = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //  SEND button clicked
            openFileDialog1.AddExtension = true;
            openFileDialog1.DefaultExt = "H8D";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "H8/H89 Disk Images|*.h8d;*.h17";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //  open the serial port
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();

                if (!CheckClientReady())
                {
                    serialPort1.Close();
                    return;
                }

                serialPort1.ReadTimeout = SerialPort.InfiniteTimeout;

                int c = 0;
                int track_size = 0xA00;

                byte[] send_buffer = new byte[2];

                string drive_str = comboBox2.SelectedItem.ToString();
                if (drive_str.Equals("SY0"))
                {
                    read_buffer[0] = (byte)'0';
                }
                else if (drive_str.Equals("SY1"))
                {
                    read_buffer[0] = (byte)'1';
                }
                else if (drive_str.Equals("SY2"))
                {
                    read_buffer[0] = (byte)'2';
                }
                serialPort1.Write(read_buffer, 0, 1);
                c = serialPort1.ReadByte();

                if (c == '?')
                {
                    c = '0';
                }

                SendToListBox(string.Format("Drive SY{0} selected", (char)c));

                //  open stream, read in 1st track to get volume info
                BinaryReader read_stream = new BinaryReader(File.Open(openFileDialog1.FileName, FileMode.Open));
                read_stream.Read(read_buffer, 0, track_size);

                int volume_num;

                if (checkBox2.Checked)
                {
                    //  auto-detect the HDOS images
                    if (IsHDOSDisk(read_buffer))
                    {
                        volume_num = read_buffer[0x900];
                        textBox2.Text = volume_num.ToString();
                        textBox4.Text = GetHDOSLabel(read_buffer);
                    }
                    else
                    {
                        //  probably a CP/M disk so set the volume to 0
                        volume_num = 0;
                        textBox2.Text = "0";
                        textBox4.Text = "CP/M DISK IMAGE";
                    }
                }
                else
                {
                    if (checkBox1.Checked)
                    {
                        volume_num = int.Parse(textBox2.Text);
                    }
                    else
                    {
                        volume_num = read_buffer[0x900];
                    }
                }
                //  setup the disk parameters (1 or 2 sided, 40 or 80 track)
                byte disk_type = 255;
                if (checkBox3.Checked || checkBox4.Checked) // force override any auto-detect disk type settings
                {
                    //  setup the disk parameters (1 or 2 sided, 40 or 80 track)
                    if (!checkBox3.Checked && !checkBox4.Checked)
                    {
                        disk_type = 0;
                    }
                    else if (checkBox3.Checked && !checkBox4.Checked)
                    {
                        disk_type = 1;
                    }
                    else if (!checkBox3.Checked && checkBox4.Checked)
                    {
                        disk_type = 2;
                    }
                    else if (checkBox3.Checked && checkBox4.Checked)
                    {
                        disk_type = 3;
                    }
                }

                int total_tracks = (checkBox4.Checked) ? 80 : 40;

                int target_tracks = 40;
                bool bDoubleSided = false;

                if (disk_type == 255)
                {
                    if (IsHDOSDisk(read_buffer))
                    {
                        disk_type = read_buffer[0x910];
                    }
                    else
                    {
                        long length = read_stream.BaseStream.Length;
                        if (length >= 400000)
                        {
                            disk_type = 3;
                        }
                        else if (length >= 200000)
                        {
                            disk_type = 1; // favor DS 40 track over SS 80 track
                        }
                        else
                        {
                            disk_type = 0;
                        }
                    }
                }
                if (disk_type == 0)
                {
                    total_tracks = 40;
                    target_tracks = 40;
                    bDoubleSided = false;
                }
                else if (disk_type == 1)
                {
                    total_tracks = 80;
                    target_tracks = 40;
                    bDoubleSided = true;
                }
                else if (disk_type == 2)
                {
                    total_tracks = 80;
                    target_tracks = 80;
                    bDoubleSided = false;
                }
                else if (disk_type == 3)
                {
                    total_tracks = 160;
                    target_tracks = 80;
                    bDoubleSided = true;
                }

                if (disk_type == 0)
                {
                    send_buffer[0] = (byte)'4';
                }
                else if (disk_type == 1)
                {
                    send_buffer[0] = (byte)'5';
                }
                else if (disk_type == 2)
                {
                    send_buffer[0] = (byte)'6';
                }
                else if (disk_type == 3)
                {
                    send_buffer[0] = (byte)'7';
                }
                serialPort1.Write(send_buffer, 0, 1);
                c = serialPort1.ReadByte();
                if (c >= '4' && c <= '7')
                {
                    SendToListBox(string.Format("Disktype {0} set on client", c - '4'));
                }
                else
                {
                    disk_type = 0;
                }

                //  send volume number to the client
                send_buffer[0] = (byte)'V';
                serialPort1.Write(send_buffer, 0, 1);
                send_buffer[0] = (byte)volume_num;
                serialPort1.Write(send_buffer, 0, 1);

                //  wait for reply
                c = serialPort1.ReadByte();
                if (c != 'V')
                {
                    SendToListBox("!! Volume set failed !!");
                    read_stream.Close();
                    serialPort1.Close();
                    return;
                }

                if (MessageBox.Show(string.Format("Ready to send {0} to device {1}?", openFileDialog1.SafeFileName, drive_str), "Write image", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    SendToListBox("Operation aborted");

                    read_stream.Close();
                    serialPort1.Close();
                    return;
                }

                SendToListBox(string.Format("Sending File: {0}", openFileDialog1.SafeFileName));
                SendToListBox(string.Format("Disk Volume #{0} Sides:{1} Tracks:{2}", volume_num, (bDoubleSided ? 2 : 1), target_tracks));
                textBox2.Text = string.Format("{0}", volume_num);

                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = true;
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                comboBox1.Enabled = false;

                bAbortTransfer = false;

                //  put client in disk write mode
                send_buffer[0] = (byte)'W';
                serialPort1.Write(send_buffer, 0, 1);

                bool bSide2 = false;
                for (int i = 0; i < total_tracks; i++)
                {
                    if (bAbortTransfer)
                    {
                        send_buffer[0] = (byte)'?';
                        serialPort1.Write(send_buffer, 0, 1);

                        SendToListBox("!! Transfer aborted !!");
                        listBox1.Update();
                        break;
                    }

                    int current_track = i;
                    if (bDoubleSided)
                    {
                        if ((i % 2) != 0)
                        {
                            bSide2 = true;
                        }
                        else
                        {
                            bSide2 = false;
                        }
                        current_track = i / 2;
                    }

                    SendToListBox(string.Format("Writing track {0} side {1}", current_track, bSide2 ? 2 : 1));

                    textBox3.Text = string.Format("{0}/{1}/{2}", current_track, target_tracks - 1, bSide2 ? 2 : 1);

                    //  notify client we are sending a track
                    send_buffer[0] = (byte)'W';
                    serialPort1.Write(send_buffer, 0, 1);

                    if (i > 0)
                    {
                        read_stream.Read(read_buffer, 0, track_size);
                    }

                    //  sending as one big stream doesn't seem to work (no handshaking?) so I send one byte at a time
                    for (int j = 0; j < track_size; j++)
                    {
                        send_buffer[0] = read_buffer[j];
                        serialPort1.Write(send_buffer, 0, 1);
                        Application.DoEvents();
                    }

                    //  wait for confirmation from client
                    c = serialPort1.ReadByte();
                    if (c != 'W')
                    {
                        SendToListBox("!! Handshake error !!");
                        listBox1.Update();
                        break;
                    }
                }

                if (bAbortTransfer)
                {
                    textBox2.Text = "";
                    textBox4.Text = "";
                }
                else
                {
                    SendToListBox(string.Format("File {0} sent", openFileDialog1.SafeFileName));
                    SendToListBox(string.Format("Disk Volume #{0}", volume_num));
                }

                SystemSounds.Beep.Play();

                textBox3.Text = "Done";
                SendToListBox("Done");

                CheckClientReady();

                read_stream.Close();
                serialPort1.Close();

                SendToListBox("");

                comboBox1.Enabled = true;
                button6.Enabled = true;
                button5.Enabled = true;
                button4.Enabled = true;
                button3.Enabled = false;
                button2.Enabled = true;
                button1.Enabled = true;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                textBox2.Enabled = true;
                textBox2.Text = "0";
            }
            else
            {
                textBox2.Enabled = false;
                textBox2.Text = "From image";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (bNoClient)
            {
                SendLoader();
            }
            else
            {
                SendToListBox("Aborting...");
                bAbortTransfer = true;
            }
        }

        private void SendLoader()
        {
            //  open stream
            using (BinaryReader read_stream = new BinaryReader(File.Open("H89LDR3.BIN", FileMode.Open)))
            {
                SendToListBox("Sending H89LDR3.BIN to target machine...");
                listBox1.Update();

                int length = (int)read_stream.BaseStream.Length;

                byte[] stream_buffer = new byte[length];
                read_stream.Read(stream_buffer, 0, length);

                read_stream.Close();

                //  open the serial port
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();

                byte[] send_buffer = new byte[2];

                //  send it in reverse byte order
                for (int j = length - 1; j >= 0; j--)
                {
                    send_buffer[0] = stream_buffer[j];
                    serialPort1.Write(send_buffer, 0, 1);
                }

                SendToListBox("H89LDR3.BIN sent successfully");

                Application.DoEvents();

                for (int i = 0; i < 2; i++)
                {
                    if (CheckClientReady())
                    {
                        if (MessageBox.Show("Client is ready. Save to a disk on the target machine?", "Save to target?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            send_buffer[0] = (byte)'S';
                            serialPort1.Write(send_buffer, 0, 1);
                        }
                        break;
                    }
                }

                serialPort1.Close();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            serialPort1.PortName = comboBox1.SelectedItem.ToString();
            serialPort1.Open();

            if (bNoClient)
            {
                CheckClientExist();
            }
            else
            {
                CheckClientReady();
            }

            serialPort1.Close();
        }

        private void CheckClientExist()
        {
            if (!CheckClientReady())
            {
                listBox1.Items.Add("");
                listBox1.Items.Add("Client is not running or not connected.");
                listBox1.Items.Add("");
                listBox1.Items.Add("If the  client program  has not been entered yet go ahead");
                listBox1.Items.Add("and key it in as instructed in the H89LDR README file and");
                listBox1.Items.Add("then click the  SAVE  button to create a  bootable client");
                listBox1.Items.Add("disk on the target machine.");
                listBox1.Items.Add("");
                listBox1.Items.Add("If you already have a  bootable client  disk then boot it");
                listBox1.Items.Add("now and click the  CLIENT STATUS  button when ready.");
                listBox1.Items.Add("");
                listBox1.SelectedIndex = listBox1.Items.Count - 1;

                button3.Text = "SAVE";
                button3.Enabled = true;

                button1.Enabled = false;
                button2.Enabled = false;

                bNoClient = true;
            }
        }

        private bool CheckClientReady()
        {
            bool ready = false;

            read_buffer[0] = (byte)'?';
            serialPort1.Write(read_buffer, 0, 1);

            try
            {
                int c = serialPort1.ReadByte();
                if (c == '?')
                {
                    SendToListBox("Client is ready");
                    if (bNoClient)
                    {
                        SetButton3Active();
                    }
                    button6.Enabled = true;
                    ready = true;
                }
                else
                {
                    SendToListBox("Client is not ready");
                    button6.Enabled = false;
                }
            }
            catch (TimeoutException e)
            {
                SendToListBox(e.Message);
                button6.Enabled = false;
            }

            return (ready);
        }

        private void SetButton3Active()
        {
            button3.Text = "ABORT";
            button3.Enabled = false;

            button1.Enabled = true;
            button2.Enabled = true;

            bNoClient = false;
        }

        private void SendToListBox(string s)
        {
            listBox1.Items.Add(s);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                checkBox1.Enabled = false;
                textBox2.Enabled = false;
            }
            else
            {
                checkBox1.Enabled = true;
                if (checkBox1.Checked)
                {
                    textBox2.Enabled = true;
                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            bool is_open = serialPort1.IsOpen;
            if (!is_open)
            {
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }

            if (CheckClientReady())
            {
                byte[] send_buffer = new byte[16];

                if (MessageBox.Show("Save the loader to a disk on the target machine?", "Save Loader", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    send_buffer[0] = (byte)'S';
                    serialPort1.Write(send_buffer, 0, 1);

                    try
                    {
                        int c = serialPort1.ReadByte();
                        if (c == 'S')
                        {
                            SendToListBox("Save successful");
                        }
                        else
                        {
                            SendToListBox("!! Save failed !!");
                        }
                    }
                    catch (TimeoutException t)
                    {
                        SendToListBox(t.Message);
                    }
                }
            }

            if (!is_open)
            {
                serialPort1.Close();
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            serialPort1.PortName = comboBox1.SelectedItem.ToString();
            serialPort1.Open();

            if (!CheckClientReady())
            {
                serialPort1.Close();
                return;
            }

            serialPort1.ReadTimeout = SerialPort.InfiniteTimeout;

            byte[] track_buffer = new byte[5120];

            int c = 0;

            string drive_str = comboBox2.SelectedItem.ToString();
            if (drive_str.Equals("SY0"))
            {
                read_buffer[0] = (byte)'0';
            }
            else if (drive_str.Equals("SY1"))
            {
                read_buffer[0] = (byte)'1';
            }
            else if (drive_str.Equals("SY2"))
            {
                read_buffer[0] = (byte)'2';
            }
            serialPort1.Write(read_buffer, 0, 1);
            c = serialPort1.ReadByte();

            string side_str = comboBox3.SelectedItem.ToString();
            if (side_str.Equals("SIDE2"))
            {
                read_buffer[0] = (byte)'B';
            }
            else
            {
                read_buffer[0] = (byte)'A';
            }
            serialPort1.Write(read_buffer, 0, 1);
            c = serialPort1.ReadByte();

            read_buffer[0] = (byte)'Z';
            serialPort1.Write(read_buffer, 0, 1);
            c = serialPort1.ReadByte();
            if (c == 'Z')
            {
                c = int.Parse(textBox5.Text);
                read_buffer[0] = (byte)c;
                serialPort1.Write(read_buffer, 0, 1);
                c = serialPort1.ReadByte(); // low order byte count
                int n = c;
                c = serialPort1.ReadByte(); // high order byte count
                n += (c * 256);
                SendToListBox(string.Format("{0} bytes", n));
                for (int i = 0; i < n; i++)
                {
                    track_buffer[i] = (byte)serialPort1.ReadByte();
                }
                serialPort1.Close();

                int count = 0;
                while (count < n)
                {
                    string s = "";
                    for (int i = 0; i < 16; i++)
                    {
                        if (count + i < n)
                        {
                            s = string.Concat(s, string.Format("{0,2:X2} ", track_buffer[count + i]));
                        }
                        else
                        {
                            s = string.Concat(s, "XX ");
                        }
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (count + i < n)
                        {
                            c = track_buffer[count + i];
                            if (c >= ' ' && c <= 126)
                            {
                                s = string.Concat(s, string.Format("{0}", (char)c));
                            }
                            else
                            {
                                s = string.Concat(s, ".");
                            }
                        }
                        else
                        {
                            s = string.Concat(s, ".");
                        }
                    }
                    SendToListBox(s);
                    count += 16;
                }
            }
        }
    }
}
