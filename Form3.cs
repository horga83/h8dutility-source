using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

namespace H8DUtility
{
    public partial class Form3 : Form
    {
        bool bCheckAborted = false;
        bool bSVDStarted = false;
        bool bSVDConnected = false;
        string[] FullFileName = new string[3];
        string[] SafeFileName = new string[3];

        public Form3()
        {
            InitializeComponent();
            CenterToParent();
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            string[] port_list = SerialPort.GetPortNames();
            if (port_list.Length > 0)
            {
                for (int i = 0; i < port_list.Length; i++)
                {
                    comboBox1.Items.Add(string.Format("{0}", port_list[i]));
                }
                comboBox1.SelectedIndex = 0;

                serialPort1.Encoding = new UTF8Encoding();
            }
            else
            {
                SendToListBox("No COM ports found");
                SendToListBox("");
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            byte[] send_buffer = new byte[2];

            //  reset the SVD
            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }

            bSVDConnected = false;

            if (SVD_Connect())
            {
                if (SVD_Stop())
                {
                    //System.Threading.Thread.Sleep(250);
                    //serialPort1.BreakState = true;
                    //System.Threading.Thread.Sleep(250);
                    //serialPort1.BreakState = false;
                    //System.Threading.Thread.Sleep(250);
                    SendToListBox("Resetting the SVD.");
                    send_buffer[0] = 0x04;
                    try
                    {
                        serialPort1.Write(send_buffer, 0, 1);
                        SendToListBox("SVD Reset.");

                        bSVDConnected = false;
                        SVD_Connect();
                    }
                    catch (TimeoutException t)
                    {
                        SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
                    }
                }
            }

            serialPort1.Close();
        }

        private void SendToListBox(string s)
        {
            listBox1.ClearSelected();
            listBox1.Items.Add(s);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
            Application.DoEvents();
        }

        //  we're just concerned about Heathkit disk images (10 sector, 40 track, 256-byte sector size)
        private void SendToSVD(int svd_disk)
        {
            //  send image to SVD
            int idx = 0;
            int sectors = 10;
            int sector_size = 256;
            int tracks = 40;
            byte[] send_buffer = new byte[2];

            string file_name = SafeFileName[svd_disk].ToUpper();
            if (svd_disk == 2)
            {
                textBox1.Text = file_name;
            }
            else if (svd_disk == 1)
            {
                textBox2.Text = file_name;
            }
            else
            {
                textBox3.Text = file_name;
            }

            Application.DoEvents();

            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }

            if (!SVD_Connect())
            {
                serialPort1.Close();
                return;
            }

            if (!SVD_Stop())
            {
                serialPort1.Close();
                return;
            }

            byte[] svd_data;
            if (file_name.Contains(".H8D") || file_name.Contains(".H17"))
            {
                //  convert the H8D disk image to SVD format, store in svd_data[]
                Process p = new Process();
                p.StartInfo.FileName = "tosvd.exe";
                p.StartInfo.Arguments = file_name;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(FullFileName[svd_disk]);
                p.Start();
                string proc_out = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                Encoding enc = Encoding.GetEncoding(1252);
                svd_data = enc.GetBytes(proc_out);
            }
            else
            {
                //  already in SVD format, just load it into svd_data[]
                BinaryReader reader = new BinaryReader(File.Open(FullFileName[svd_disk], FileMode.Open));
                svd_data = new byte[reader.BaseStream.Length];
                reader.Read(svd_data, 0, (int)reader.BaseStream.Length);
                reader.Close();
            }

            SendToListBox(string.Format("Uploading {0} to DISK#{1}", file_name, 2 - svd_disk));
            send_buffer[0] = 0x20;
            try
            {
                serialPort1.Write(send_buffer, 0, 1);
                if (serialPort1.ReadByte() != 0x20)
                {
                    SendToListBox("ERROR! Command sync error.");
                    serialPort1.Close();
                    return;
                }
            }
            catch (TimeoutException e)
            {
                SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
                serialPort1.Close();
                return;
            }

            idx = 0;
            bool abort = false;
            //  4 byte disk header
            try
            {
                send_buffer[0] = (byte)svd_disk;
                serialPort1.Write(send_buffer, 0, 1);   //  disk number
                send_buffer[0] = (byte)sectors;
                serialPort1.Write(send_buffer, 0, 1);   //  sectors
                send_buffer[0] = (byte)tracks;
                serialPort1.Write(send_buffer, 0, 1);   //  tracks
                send_buffer[0] = 1;
                serialPort1.Write(send_buffer, 0, 1);   //  sector size (1 = 256 bytes)
            }
            catch (TimeoutException e)
            {
                SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
                serialPort1.Close();
                return;
            }
            label3.Text = string.Format("Disk#{0} Sectors={1} Tracks={2} Sector Size=1 WP=0", svd_disk, sectors, tracks);
            //  16 byte disk info - don't send this to the SVD
            //for (int i = 0; i < 16; i++)
            //{
            //    send_buffer[0] = svd_data[idx++];
            //    serialPort1.Write(send_buffer, 0, 1);
            //}
            idx += 16;
            //  40 tracks of 11 256-byte sector data (1 crc sector, 10 disk sectors)
            for (int track = 0; track < tracks; track++)
            {
                try
                {
                    //  crc sector (for SVD)
                    for (int i = 0; i < sector_size; i++)
                    {
                        send_buffer[0] = svd_data[idx++];
                        serialPort1.Write(send_buffer, 0, 1);
                    }
                    //  disk sectors
                    for (int sector = 0; sector < sectors; sector++)
                    {
                        for (int i = 0; i < sector_size; i++)
                        {
                            send_buffer[0] = svd_data[idx++];
                            serialPort1.Write(send_buffer, 0, 1);
                        }
                    }

                    SendToListBox(string.Format("Track {0} sent.", track));

                    if (serialPort1.ReadByte() != '>')
                    {
                        SendToListBox("ERROR! Track sync error.");
                        abort = true;
                        break;
                    }
                }
                catch (TimeoutException e)
                {
                    SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
                    abort = true;
                    break;
                }
            }
            if (abort)
            {
                SendToListBox("ERROR! Upload failed. Reset the SVD and try again.");
            }
            else
            {
                if (SVD_Start())
                {
                    SystemSounds.Beep.Play();
                }
            }

            serialPort1.Close();
        }

        private void GetFromSVD(int svd_disk)
        {
        }

        private bool SVD_Connect()
        {
            int tries = 0;
            byte[] send_buffer = new byte[16];
            byte[] read_buffer = new byte[16];
            ASCIIEncoding encoding = new ASCIIEncoding();

            if (bSVDConnected)
            {
                return (true);
            }

            SendToListBox("");
            SendToListBox(string.Format("Connecting to SVD on {0} at {1} baud", comboBox1.SelectedItem.ToString(), serialPort1.BaudRate));

            send_buffer[0] = 1;
            while (tries < 10)
            {
                serialPort1.Write(send_buffer, 0, 1);
                int n = 0;
                do
                {
                    try
                    {
                        int c = serialPort1.ReadByte();
                        if (c == 1)
                        {
                            read_buffer[n] = 0;
                            SendToListBox(string.Format("SVD Connected. Firmware Version: {0}", encoding.GetString(read_buffer)));
                            textBox4.Text = string.Format("{0}", encoding.GetString(read_buffer));
                            bSVDConnected = true;
                            bSVDStarted = false;
                            return (true);
                        }
                        if (n < 7)
                        {
                            read_buffer[n++] = (byte)c;
                        }
                    }
                    catch (TimeoutException e)
                    {
                        break;
                    }
                } while (true);
                tries++;
            }
            SendToListBox("ERROR! SVD Connection failed.");
            return (false);
        }

        private bool SVD_Start()
        {
            byte[] send_buffer = new byte[2];

            if (bSVDStarted)
            {
                return (true);
            }

            SendToListBox("Starting the SVD.");
            send_buffer[0] = 0x08;
            try
            {
                serialPort1.Write(send_buffer, 0, 1);
                if (serialPort1.ReadByte() != 0x08)
                {
                    SendToListBox("ERROR! SVD failed to start.");
                }
                else
                {
                    bSVDStarted = true;
                    SendToListBox("SVD Ready.");
                    return (true);
                }
            }
            catch (TimeoutException t)
            {
                SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
            }
            return (false);
        }

        private bool SVD_Stop()
        {
            byte[] send_buffer = new byte[2];

            if (!bSVDStarted)
            {
                return (true);
            }

            SendToListBox("Stopping the SVD.");

            send_buffer[0] = 0x10;
            try
            {
                serialPort1.Write(send_buffer, 0, 1);
                if (serialPort1.ReadByte() != 0x10)
                {
                    SendToListBox("ERROR! SVD failed to respond.");
                }
                else
                {
                    bSVDStarted = false;
                    SendToListBox("SVD Stopped.");
                    return (true);
                }
            }
            catch (TimeoutException e)
            {
                SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
            }
            return (false);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "H8/H89 Disk Images|*.h8d;*.h17;*.svd";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                FullFileName[2] = openFileDialog1.FileName;
                SafeFileName[2] = openFileDialog1.SafeFileName;
                SendToSVD(2);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "H8/H89 Disk Images|*.h8d;*.h17;*.svd";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                FullFileName[1] = openFileDialog1.FileName;
                SafeFileName[1] = openFileDialog1.SafeFileName;
                SendToSVD(1);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "H8/H89 Disk Images|*.h8d;*.h17;*.svd";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                FullFileName[0] = openFileDialog1.FileName;
                SafeFileName[0] = openFileDialog1.SafeFileName;
                SendToSVD(0);
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = comboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }

            bSVDConnected = false;

            SVD_Connect();
            
            serialPort1.Close();
        }

        private void SaveFromSVD(int svd_disk)
        {
            int idx = 0;
            byte[] send_buffer = new byte[2];

            saveFileDialog1.DefaultExt = "H8D";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.PortName = comboBox1.SelectedItem.ToString();
                    serialPort1.Open();
                }
                serialPort1.DiscardInBuffer();
                if (SVD_Stop())
                {
                    send_buffer[0] = 0x02;
                    try
                    {
                        serialPort1.Write(send_buffer, 0, 1);
                        if (serialPort1.ReadByte() != 0x02)
                        {
                            SendToListBox("ERROR! SVD did not respond.");
                            serialPort1.Close();
                            return;
                        }
                        send_buffer[0] = (byte)svd_disk;
                        serialPort1.Write(send_buffer, 0, 1);
                        svd_disk = 2 - svd_disk;    //  convert to Heath drive numbering
                        int sectors = serialPort1.ReadByte();
                        int tracks = serialPort1.ReadByte();
                        int blocks_per_sector = serialPort1.ReadByte();
                        int sector_size = (blocks_per_sector + 1) * 128;
                        int wrtp = serialPort1.ReadByte();
                        label3.Text = string.Format("Disk#{0} Sectors={1} Tracks={2} Sector Size={3} WP={4}", svd_disk, sectors, tracks, blocks_per_sector, wrtp);
                        int file_size = 16 + (((sectors + 1) * sector_size) * tracks);
                        byte[] read_buffer = new byte[file_size];
                        //  16 byte SVD disk header
                        read_buffer[idx++] = 0x32;  //  version 2.0
                        read_buffer[idx++] = 0x2E;
                        read_buffer[idx++] = 0x30;
                        read_buffer[idx++] = 0x0A;  //  linefeed
                        //  sectors
                        read_buffer[idx++] = (byte)(0x30 + (sectors / 10));
                        read_buffer[idx++] = (byte)(0x30 + (sectors % 10));
                        read_buffer[idx++] = 0x0A;
                        //  tracks
                        read_buffer[idx++] = (byte)(0x30 + (tracks / 10));
                        read_buffer[idx++] = (byte)(0x30 + (tracks % 10));
                        read_buffer[idx++] = 0x0A;
                        //  blocks per sector
                        read_buffer[idx++] = (byte)(0x30 + blocks_per_sector);
                        read_buffer[idx++] = 0x0A;
                        //  ??
                        read_buffer[idx++] = (byte)(0x30 + 1);
                        read_buffer[idx++] = 0x0A;
                        //  write protect
                        read_buffer[idx++] = (byte)(0x30 + wrtp);
                        read_buffer[idx++] = 0x0A;

                        bool quit = false;
                        int cmd = serialPort1.ReadByte();
                        for (int track = 0; track < tracks; track++)
                        {
                            for (int sector = 0; sector < sectors + 1; sector++)
                            {
                                serialPort1.DiscardInBuffer();
                                if (cmd == '+')
                                {
                                    //  transmit next block
                                    send_buffer[0] = (byte)'n';
                                    serialPort1.Write(send_buffer, 0, 1);
                                }
                                else if (cmd == '>')
                                {
                                    send_buffer[0] = (byte)'N';
                                    serialPort1.Write(send_buffer, 0, 1);
                                }
                                else if (cmd == '!')
                                {
                                    SendToListBox("Disk completed.");
                                    quit = true;
                                    break;
                                }
                                else
                                {
                                    SendToListBox(string.Format("WARNING! Unknown command {0} at index {1}", cmd, idx));
                                    serialPort1.Close();
                                    return;
                                }
                                for (int i = 0; i < sector_size; i++)
                                {
                                    read_buffer[idx++] = (byte)serialPort1.ReadByte();
                                }
                                int chk_sum = serialPort1.ReadByte();
                                //SendToListBox(string.Format("Track {0} Sector {1} received.", track, sector + 1));
                                cmd = serialPort1.ReadByte();
                            }
                            if (quit)
                            {
                                break;
                            }
                            if (cmd == '>')
                            {
                                SendToListBox(string.Format("Track {0} received.", track));
                                cmd = serialPort1.ReadByte();
                            }
                            else
                            {
                                SendToListBox(string.Format("WARNING! Expected '>' but got {0} at index {1}", cmd, idx));
                                serialPort1.Close();
                                return;
                            }
                        }

                        serialPort1.DiscardInBuffer();

                        SendToListBox(string.Format("Disk#{0} received successfully.", svd_disk));
                        SendToListBox(string.Format("Saving file: {0}", saveFileDialog1.FileName));
                        //  save file to disk in SVD format
                        string file_name = Path.GetFileName(saveFileDialog1.FileName).ToUpper();
                        string options = file_name;
                        if (file_name.Contains(".H8D"))
                        {
                            file_name = file_name.Replace(".H8D", ".SVD");
                            options = string.Format("-o H8D {0}", file_name);
                        }
                        else if (file_name.Contains(".H17"))
                        {
                            file_name = file_name.Replace(".H17", ".SVD");
                            options = string.Format("-o H17 {0}", file_name);
                        }
                        BinaryWriter write_stream = new BinaryWriter(File.Create(file_name));
                        write_stream.Write(read_buffer, 0, file_size);
                        write_stream.Close();
                        //  convert to other format if necessary
                        string save_file_name = saveFileDialog1.FileName.ToUpper();
                        if (!save_file_name.Contains(".SVD"))
                        {
                            Process p = new Process();
                            p.StartInfo.FileName = "tosvd.exe";
                            p.StartInfo.Arguments = options;
                            p.StartInfo.CreateNoWindow = true;
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(save_file_name);
                            p.Start();
                            string proc_out = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();

                            Encoding enc = Encoding.GetEncoding(1252);
                            byte[] svd_data = enc.GetBytes(proc_out);
                            write_stream = new BinaryWriter(File.Create(save_file_name));
                            write_stream.Write(svd_data, 0, svd_data.Length);
                            write_stream.Close();
                            //  delete the temporary .SVD file
                            File.Delete(file_name);
                        }

                        SendToListBox("Done.");
                    }
                    catch (TimeoutException t)
                    {
                        SendToListBox("ERROR! Action timed out. Reset the SVD or check the connection.");
                        serialPort1.Close();
                        return;
                    }
                }
                
                serialPort1.Close();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //  save disk 0 (index = 2)
            SaveFromSVD(2);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //  save disk 1 (index = 1)
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //  save disk 2 (index = 0)
        }

        private void button9_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length > 0)
            {
                SendToSVD(2);
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Length > 0)
            {
                SendToSVD(1);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (textBox3.Text.Length > 0)
            {
                SendToSVD(0);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (bCheckAborted)
            {
                bCheckAborted = false;
                return;
            }
            if (MessageBox.Show("Changing the baud rate will require a reset and you will lose all images in the device. Continue?", "Notice", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (MessageBox.Show("Reset the SVD to put it in auto-baud detection mode then click OK", "Reset for auto baud", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    bSVDConnected = false;

                    if (serialPort1.IsOpen)
                    {
                        serialPort1.Close();
                    }
                    serialPort1.BaudRate = checkBox1.Checked ? 115200 : 57600;
                    serialPort1.PortName = comboBox1.SelectedItem.ToString();
                    serialPort1.Open();

                    SVD_Connect();

                    serialPort1.Close();

                    return;
                }
            }
            bCheckAborted = true;
            checkBox1.Checked = !checkBox1.Checked;
        }
    }
}
