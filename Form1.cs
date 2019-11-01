using CPM;
using H8DUtility;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;


namespace H8DReader
    {

    public partial class Form1 : Form
        {
        public static string label3str = "";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CPMDirEntry
            {
            public byte flag;
            public byte[] filename;
            public byte[] fileext;
            public byte extent;
            public byte[] unused;
            public byte sector_count;
            public byte[] alloc_map;
            }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HDOSDiskInfo
            {
            public byte serial_num;
            public ushort init_date;
            public long dir_sector;
            public long grt_sector;
            public byte sectors_per_group;
            public byte volume_type;
            public byte init_version;
            public long rgt_sector;
            public ushort volume_size;
            public ushort phys_sector_size;
            public byte flags;
            public byte[] label;
            public ushort reserved;
            public byte sectors_per_track;
            }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HDOSDirEntry
            {
            public byte[] filename;
            public byte[] fileext;
            public byte project;
            public byte version;
            public byte cluster_factor;
            public byte flags;
            public byte flags2;
            public byte first_group_num;
            public byte last_group_num;
            public byte last_sector_index;
            public ushort creation_date;
            public ushort alteration_date;
            }

        public struct DiskFileEntry
            {
            public int ListBox2Entry;
            public string DiskImageName;
            public string FileName;
            public HDOSDirEntry HDOSEntry;
            }

        public ArrayList DiskFileList;

        public struct DiskLabelEntry
            {
            public int ListBox2Entry;
            public string DiskImageName;
            public string DiskLabelName;
            }

        public ArrayList DiskLabelList;
        public DiskLabelEntry RelabelEntry;

        public byte[] HDOSGrtTable;
        public byte[] FILEGrtAllocTable;

        public bool bImageList = false;

        public int FileCount = 0;
        public int TotalSize = 0;


        public GroupBox FileViewerBorder;
        public RichTextBox FileViewerBox;

        public bool bSVDConnected;

        //
        //
        //

        public Form1()
            {
            InitializeComponent();
            CenterToScreen();
            }

        private void Form1_Load(object sender, EventArgs e)
            {
            label6.Text = "Version 2.0 CP/M extract/Add, IMD Read/Extract";       // version number update Darrell Pelan

            FileViewerBorder = new GroupBox();
            FileViewerBorder.Size = new System.Drawing.Size(720, 580);
            FileViewerBorder.Location = new System.Drawing.Point(90, 30);
            FileViewerBorder.Text = "File Viewer";
            FileViewerBorder.ForeColor = Color.Black;
            FileViewerBorder.BackColor = Color.DarkGray;
            FileViewerBorder.Visible = false;

            this.Controls.Add(FileViewerBorder);

            FileViewerBox = new RichTextBox();
            FileViewerBox.Size = new System.Drawing.Size(700, 520);
            FileViewerBox.Location = new System.Drawing.Point(10, 20);
            FileViewerBox.Font = new Font(FontFamily.GenericMonospace, 10);
            FileViewerBox.BorderStyle = BorderStyle.FixedSingle;
            FileViewerBox.BackColor = Color.LightGray;
            FileViewerBox.ReadOnly = true;

            FileViewerBorder.Controls.Add(FileViewerBox);

            Button FileViewerButton = new Button();
            FileViewerButton.Name = "filebutton1";
            FileViewerButton.Text = "CLOSE";
            FileViewerButton.Location = new System.Drawing.Point((FileViewerBorder.Size.Width / 2) - (FileViewerButton.Size.Width / 2), 550);
            FileViewerButton.Click += new EventHandler(filebutton1_Click);
            FileViewerButton.BackColor = Color.LightGray;

            FileViewerBorder.Controls.Add(FileViewerButton);

            button2.Enabled = false;
            DisableButtons();
            /*
            string[] port_list = SerialPort.GetPortNames();
            if (port_list.Length > 0)
            {
                for (int i = 0; i < port_list.Length; i++)
                {
                    comboBox1.Items.Add(string.Format("{0}", port_list[i]));
                }
                comboBox1.SelectedIndex = 0;
            }

            serialPort1.Encoding = new UTF8Encoding();
            */
            folderBrowserDialog1.ShowNewFolderButton = false;
            DiskFileList = new ArrayList();
            DiskLabelList = new ArrayList();

            ReadData();
            FileCount = 0;
            // DCP
            if (folderBrowserDialog1.SelectedPath.Length > 0)
                {
                button1_init();
                }




            }
        // DCP
        private void button1_init()
            {
            button2.Enabled = false;        // Catalog button disabled in case no files are found
            button1_initA();
            label3str = label3.Text;        // used in CPMFile
            }

        private void button1_initA()        // dcp modified code to read files store in last used directory. initA is used both on startup and when Folder Button is clicked.
            {
            listBox1.Items.Clear();                             // clear file list
            label3.Text = folderBrowserDialog1.SelectedPath;    // display current working directory
            // set file extension types to scan directory
            string[] h8d_list = Directory.GetFiles(label3.Text, "*.h8d");
            string[] svd_list = Directory.GetFiles(label3.Text, "*.svd");
            string[] imd_list = Directory.GetFiles(label3.Text, "*.imd");
            string[] file_list = new string[h8d_list.Length + svd_list.Length + imd_list.Length];         // combine filename lists
            Array.Copy(h8d_list, file_list, h8d_list.Length);
            Array.Copy(svd_list, 0, file_list, h8d_list.Length, svd_list.Length);
            Array.Copy(imd_list, 0, file_list, h8d_list.Length + svd_list.Length, imd_list.Length);


            if (file_list.Length == 0)
                {
                listBox1.Items.Add("No image files found");
                label4.Text = "0 Files";
                bImageList = false;
                }
            else
                {
                foreach (string files in file_list)             // add file names to listbox1
                    {
                    string file_name;
                    file_name = files.Substring(files.LastIndexOf("\\") + 1).ToUpper();
                    listBox1.Items.Add(file_name);
                    string file_count = string.Format("{0} disk images", listBox1.Items.Count.ToString());
                    label4.Text = file_count;
                    }
                button2.Enabled = true;         // enable Catalog button
                bImageList = true;
                }
            }
        private void DisableButtons()
            {
            button3.Enabled = false;
            button4.Enabled = false;
            button5.Enabled = false;
            button7.Enabled = false;
            }

        private void ReadData()
            {
            if (File.Exists("H8DUtility.dat"))
                {
                StreamReader stream = File.OpenText("H8DUtility.dat");
                if (stream != null)
                    {
                    folderBrowserDialog1.SelectedPath = stream.ReadLine();
                    stream.Close();
                    }
                }
            }

        private void SaveData()
            {
            StreamWriter stream = File.CreateText("H8DUtility.dat");
            if (stream != null)
                {
                stream.WriteLine(folderBrowserDialog1.SelectedPath);
                stream.Close();
                }
            }

        private void button1_Click(object sender, EventArgs e)
            {
            button2.Enabled = false;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                SaveData();
                button1_initA();
                }
            }

        //*************** Catalog ********************
        private void button2_Click(object sender, EventArgs e)
            {
            //  catalog selected image(s)
            FileCount = 0;
            TotalSize = 0;
            listBox2.Items.Clear();
            DiskFileList.Clear();
            DiskLabelList.Clear();

            if (listBox1.SelectedIndex != -1)
            // one or more files selected in listbox1
                {
                foreach (var lb in listBox1.SelectedItems)
                    {
                    var file_name = label3.Text + "\\" + lb; // path + file name
                    listBox2.Items.Add(lb.ToString());
                    if (lb.ToString().Contains(".H8D"))
                        ProcessFile(file_name);
                    else
                        if (lb.ToString().Contains(".IMD")) ProcessFileImd(file_name);
                    else
                            if (lb.ToString().Contains(".H37")) ProcessFileH37(file_name);
                    }
                }
            // dcp TODO add .imd capability
            else // no files selected, so process all of them in listbox1
                {
                foreach (var lb in listBox1.Items)
                    {
                    var file_name = label3.Text + "\\" + lb;
                    listBox2.Items.Add(lb.ToString());

                    if (lb.ToString().Contains(".H8D"))
                        ProcessFile(file_name);
                    else
                        if (lb.ToString().Contains(".IMD")) ProcessFileImd(file_name);
                    else
                            if (lb.ToString().Contains(".H37")) ProcessFileH37(file_name);

                    }

                }
            // dcp TODO add .imd capability for H-37 disks

            if (FileCount == 0)
                {
                DisableButtons();
                }
            else
                {
                button3.Enabled = true;
                button4.Enabled = true;
                button5.Enabled = true;
                button7.Enabled = true;
                }
            // dcp changed KB to bytes
            listBox2.Items.Add(string.Format("Total Files {0,5:N0}, Total Size {1,5:N0} K", FileCount, TotalSize / 1024));
            listBox2.Items.Add("");
            }

        private ushort MakeBigEndian16(ushort data)
            {
            ushort h = (ushort)((data & (ushort)0x00FF) << 8);
            ushort l = (ushort)((data & (ushort)0xFF00) >> 8);
            ushort big_endian_16 = (ushort)(h | l);
            return (big_endian_16);
            }

        //******************************* Process File IMD ********************************
        private void ProcessFileImd(string DiskfileName)        // for .IMD disks
            {
            var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
            //var fileNameList = new List<CPMFile.DirList>();
            long diskUsed = 0, diskTotal = 0;
            var fileNameList = getCpmFile.ReadImdDir(DiskfileName,  ref diskTotal);
            int diskFileCnt = 0;

            if (fileNameList.Count > 0)
                {
                diskFileCnt = 0;
                diskUsed = 0;
                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add("  FILE   EXT SIZE   FLAGS  ");
                listBox2.Items.Add("======== === ==== =========");
                foreach (var f in fileNameList)
                    {
                    diskFileCnt++;
                    diskUsed += f.fsize;
                    DiskFileEntry disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = DiskfileName;
                    disk_file_entry.FileName = f.fname;
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    DiskFileList.Add(disk_file_entry);
                    listBox2.Items.Add(string.Format("{0} {1,4} {2}", f.fname, f.fsize / 1024, f.flags));
                    }

                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add(string.Format("Files {0}, Total {1,3:N0} K, Free {2,5:N0} K", diskFileCnt, diskUsed / 1024, diskTotal - diskUsed / 1024));
                listBox2.Items.Add("");
                TotalSize += (int)diskUsed;
                FileCount += diskFileCnt;
                }


            }
        //******************************* Process File H37 ********************************

        private void ProcessFileH37(string fileName)        // for .H37 disks
            {
            const int sectorSize = 2048;

            byte[] buf = new byte[sectorSize];
            UTF8Encoding encoding = new UTF8Encoding();

            FileStream file = File.OpenRead(fileName);
            BinaryReader bin_file = new BinaryReader(file);
            buf = bin_file.ReadBytes(sectorSize);
            }

        //***************** Process File **********************
        private void ProcessFile(string file_name)  // for .H8D disks
            {
            const int sector_size = 256;
            byte[] buf = new byte[sector_size];
            UTF8Encoding encoding = new UTF8Encoding();

            FileStream file = File.OpenRead(file_name);
            BinaryReader bin_file = new BinaryReader(file);
            buf = bin_file.ReadBytes(sector_size);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
                {
                HDOSDiskInfo disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort)(bin_file.ReadUInt16()); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long)(bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long)(bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long)(bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort)(bin_file.ReadUInt16()); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.phys_sector_size = (ushort)(bin_file.ReadUInt16()); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved = bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                string disk_label = string.Format("{0}", encoding.GetString(disk_info.label, 0, 60));
                disk_label = disk_label.Trim();

                HDOSGrtTable = new byte[256];
                file.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                if (CheckHDOSImageIntegrity() == false)
                    {
                    //  if volume number is 0, try as CP/M image
                    if (disk_info.serial_num == 0)
                        {
                        listBox2.Items.Add("HDOS - CP/M DUAL FORMAT");
                        listBox2.Items.Add(disk_label);
                        listBox2.Items.Add(string.Format("Volume #{0}", disk_info.serial_num.ToString()));
                        ReadCPMImage(file_name, ref bin_file, ref encoding);
                        }
                    else
                        {
                        listBox2.Items.Add("");
                        listBox2.Items.Add("!! GRT TABLE IS CORRUPT !!");
                        listBox2.Items.Add("");
                        }
                    return;
                    }

                DiskLabelEntry disk_label_entry = new DiskLabelEntry();
                disk_label_entry.DiskImageName = file_name;
                disk_label_entry.DiskLabelName = disk_label;
                disk_label_entry.ListBox2Entry = listBox2.Items.Count;
                DiskLabelList.Add(disk_label_entry);

                listBox2.Items.Add(disk_label);
                listBox2.Items.Add(string.Format("Volume #{0}", disk_info.serial_num.ToString()));

                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add("  FILE   EXT SIZE   DATE   ");
                listBox2.Items.Add("======== === ==== =========");

                file.Seek(disk_info.dir_sector, SeekOrigin.Begin);
                HDOSDirEntry entry = new HDOSDirEntry();
                entry.filename = new byte[8];
                entry.fileext = new byte[3];

                int fsize = 0;
                int entry_count = 0;
                int disk_file_count = 0;
                ushort total_size = 0;

                do
                    {
                    if (ReadHDOSDirEntry(bin_file, ref entry, ref entry_count) == false)
                        {
                        break;
                        }

                    fsize = ComputeHDOSFileSize(entry, disk_info.sectors_per_group);

                    if (fsize == -1)
                        {
                        listBox2.Items.Add("!! DIRECTORY IS CORRUPT !!");
                        listBox2.Items.Add("!!   FILESIZE FAILED    !!");
                        listBox2.Items.Add("");
                        return;
                        }
                    total_size += (ushort)fsize;
                    ushort day = (ushort)(entry.creation_date & 0x001F);
                    if (day == 0)
                        {
                        day = 1;
                        }
                    ushort mon = (ushort)((entry.creation_date & 0x01E0) >> 5);
                    string month = "Jan";
                    switch (mon)
                        {
                        case 1:
                            month = "Jan";
                            break;
                        case 2:
                            month = "Feb";
                            break;
                        case 3:
                            month = "Mar";
                            break;
                        case 4:
                            month = "Apr";
                            break;
                        case 5:
                            month = "May";
                            break;
                        case 6:
                            month = "Jun";
                            break;
                        case 7:
                            month = "Jul";
                            break;
                        case 8:
                            month = "Aug";
                            break;
                        case 9:
                            month = "Sep";
                            break;
                        case 10:
                            month = "Oct";
                            break;
                        case 11:
                            month = "Nov";
                            break;
                        case 12:
                            month = "Dec";
                            break;
                        }
                    ushort year = (ushort)((entry.creation_date & 0x7E00) >> 9);
                    if (year == 0)
                        {
                        year = 9;
                        }
                    else if (year + 70 > 99)
                        {
                        year = 99;
                        }
                    //if (month == "Inv" || year + 70 > 99)
                    //{
                    //    listBox2.Items.Add("!! DIRECTORY IS CORRUPT !!");
                    //    listBox2.Items.Add("!!     DATE FAILED      !!");
                    //    listBox2.Items.Add("");
                    //    return;
                    //}
                    string cre_date = string.Format("{0:D2}-{1}-{2}", day, month, year + 70);
                    string fname = encoding.GetString(entry.filename, 0, 8);
                    string f = fname.Replace('\0', ' ');
                    string fext = encoding.GetString(entry.fileext, 0, 3);
                    string e = fext.Replace('\0', ' ');
                    string item_name = string.Format("{0} {1} {2,4} {3}", f, e, fsize, cre_date);

                    DiskFileEntry disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = file_name;
                    disk_file_entry.FileName = string.Format("{0}{1}", f, e);
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    disk_file_entry.HDOSEntry = entry;
                    DiskFileList.Add(disk_file_entry);

                    listBox2.Items.Add(item_name);

                    disk_file_count++;
                    FileCount++;

                    } while (true);

                ushort total_free = ComputeHDOSFreeSize(disk_info.sectors_per_group);

                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add(string.Format("Files {0}, Total {1}, Free {2}", disk_file_count, total_size, total_free));
                listBox2.Items.Add("");

                TotalSize += (total_size * 256);
                }
            else
                {
                //  CP/M disk

                listBox2.Items.Add("CP/M DISK IMAGE");

                ReadCPMImage(file_name, ref bin_file, ref encoding);
                }
            file.Close();

            string file_count = string.Format("{0} files", FileCount);
            label5.Text = file_count;
            }

        private bool CheckHDOSImageIntegrity()
            {
            try
                {
                for (int i = 0; i < 256; i++)
                    {
                    if (i > 0 && i < 5)
                        {
                        continue;
                        }
                    if (i >= 200)
                        {
                        continue;
                        }
                    int grp_count = 0;
                    byte grp = HDOSGrtTable[i];
                    do
                        {
                        if (grp == 0)
                            {
                            break;
                            }
                        if (HDOSGrtTable[grp] == grp)
                            {
                            return (false);
                            }
                        grp = HDOSGrtTable[grp];
                        grp_count++;
                        } while (grp_count < 256);
                    if (grp_count == 256)
                        {
                        return (false);
                        }
                    }
                }
            catch (IndexOutOfRangeException e)
                {
                return (false);
                }
            return (true);
            }

        private int ComputeHDOSFileSize(HDOSDirEntry entry, ushort sectors_per_group)
            {
            int grp_count = 1;
            byte grp = entry.first_group_num;
            if (grp < 4 || grp >= 200)
                {
                return 0;
                }
            while (HDOSGrtTable[grp] != 0 && grp_count < 256)
                {
                if (grp < 4 || grp >= 200)
                    {
                    return (-1);
                    }
                grp = HDOSGrtTable[grp];
                grp_count++;
                }
            if (grp_count == 256)
                {
                return (-1);
                }

            int total_size = (((grp_count - 1) * sectors_per_group) + entry.last_sector_index);
            return (total_size);
            }

        private ushort ComputeHDOSFreeSize(ushort sectors_per_group)
            {
            ushort grp_count = 0;
            ushort grp = 0;
            while (HDOSGrtTable[grp] != 0 && grp_count < 256)
                {
                grp = HDOSGrtTable[grp];
                grp_count++;
                }
            if (grp_count == 256)
                {
                return (0);
                }
            return ((ushort)(grp_count * sectors_per_group));
            }

        private bool ReadHDOSDirEntry(BinaryReader bin_file, ref HDOSDirEntry entry, ref int entry_count)
            {
            do
                {
                entry.filename = bin_file.ReadBytes(8);
                entry.fileext = bin_file.ReadBytes(3);
                entry.project = bin_file.ReadByte();
                entry.version = bin_file.ReadByte();
                entry.cluster_factor = bin_file.ReadByte();
                entry.flags = bin_file.ReadByte();
                entry.flags2 = bin_file.ReadByte();
                entry.first_group_num = bin_file.ReadByte();
                entry.last_group_num = bin_file.ReadByte();
                entry.last_sector_index = bin_file.ReadByte();
                entry.creation_date = bin_file.ReadUInt16();
                entry.alteration_date = bin_file.ReadUInt16();

                entry_count++;
                if (entry_count == 22)
                    {
                    ushort max_entries = bin_file.ReadUInt16();
                    long cur_dir_blk = (bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                    long nxt_dir_blk = (bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                    bin_file.BaseStream.Seek(nxt_dir_blk, SeekOrigin.Begin);
                    entry_count = 0;
                    }

                if (entry.filename[0] == 0xFE || entry.filename[0] == 0x7F)
                    {
                    return (false);
                    }
                if (entry.filename[0] == 0x00)
                    {
                    continue;
                    }
                if (entry.filename[0] == 0xFF)
                    {
                    continue;
                    }

                return (true);

                } while (true);
            }

        //********************  dcp ReadCPMImage for .H8D format
        private void ReadCPMImage(string file_name, ref BinaryReader bin_file, ref UTF8Encoding encoding)
            {
            int fsize = 0;
            int entry_count = 0;
            int disk_file_count = 0;
            int total_free = 0;
            int total_size = 0;
            long disk_size = bin_file.BaseStream.Length / 1024 - 10;          // dcp disk size on disk

            listBox2.Items.Add("======== === ==== =========");
            listBox2.Items.Add("  FILE   EXT SIZE   DATE   ");
            listBox2.Items.Add("======== === ==== =========");

            CPMDirEntry entry = new CPMDirEntry();
            entry.filename = new byte[8];
            entry.fileext = new byte[3];
            entry.unused = new byte[2];
            entry.alloc_map = new byte[16];
            int offset = 0x1E00;
            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);

            do
                {
                if (ReadCPMDirEntry(bin_file, ref offset, ref entry, ref entry_count) == false)
                    {
                    break;
                    }

                fsize += (ushort)(entry.sector_count * 128);
                // dcp assumes directory entries are sequential

                if (entry.sector_count < 0x80)
                    {
                    if ((fsize % 1024) != 0)
                        {
                        fsize = (ushort)((fsize / 1024) + 1);
                        }
                    else
                        {
                        fsize = (ushort)(fsize / 1024);
                        }
                    string fname = encoding.GetString(entry.filename, 0, 8);
                    string f = fname.Replace('\0', ' ');
                    string fext = encoding.GetString(entry.fileext, 0, 3);
                    string e = fext.Replace('\0', ' ');
                    string item_name = string.Format("{0} {1} {2,4} -- N/A --", f, e, fsize);

                    DiskFileEntry disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = file_name;
                    disk_file_entry.FileName = string.Format("{0}{1}", f, e);
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    DiskFileList.Add(disk_file_entry);

                    listBox2.Items.Add(item_name);
                    total_size += fsize;

                    disk_file_count++;
                    FileCount++;
                    fsize = 0;
                    }
                } while (true);

            total_free = (int)disk_size - total_size;  // dcp

            listBox2.Items.Add("======== === ==== =========");
            listBox2.Items.Add(string.Format("Files {0}, Total {1}, Free {2}", disk_file_count, total_size, total_free));
            listBox2.Items.Add("");

            TotalSize += (total_size * 1024);
            }

        private bool ReadCPMDirEntry(BinaryReader bin_file, ref int offset, ref CPMDirEntry entry, ref int entry_count)
            {
            bool result = false;        // dcp default return value
            do
                {
                try
                    {
                    entry.flag = bin_file.ReadByte();
                    entry.filename = bin_file.ReadBytes(8);
                    entry.fileext = bin_file.ReadBytes(3);
                    for (int i = 0; i < 3; i++) entry.fileext[i] = (byte)(entry.fileext[i] & 0x7f);    // mask bit 7 to account for funky ASCII conversion
                    entry.extent = bin_file.ReadByte();
                    entry.unused = bin_file.ReadBytes(2);
                    entry.sector_count = bin_file.ReadByte();
                    entry.alloc_map = bin_file.ReadBytes(16);
                    // check for erased sector - all 0xE5 and adjust directory start point if needed
                    if (entry.flag == 0xE5 && entry.filename[0] == 0xE5)
                        {
                        if (entry_count == 0)
                            {
                            break;
                            }

                        if (offset == 0x1E00)
                            {
                            offset = 0x2200;
                            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);
                            }
                        else if (offset == 0x2200)
                            {
                            offset = 0x2600;
                            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);
                            }
                        else
                            {
                            break;
                            }

                        entry_count = 0;
                        continue;
                        }

                    if (entry.flag != 0xE5) // dcp not an erased directory entry
                        {
                        entry_count++;
                        result = true;
                        }
                    else break;

                    /* dcp
                    if (entry.flag != 0)
                    {
                        continue;
                    }
                    */
                    }
                catch
                    {
                    result = false;
                    break;
                    }
                } while (result == false);

            return result;
            }

        private void textBox1_TextChanged(object sender, EventArgs e)
            {
            //  search file list
            //string search_text = textBox1.Text;
            //if (search_text.Length > 0)
            //{
            //    if (listBox2.Items.Count > 0)
            //    {
            //        int i = listBox2.FindString(search_text);
            //        listBox2.SelectedItem = i;
            //    }
            //}
            //textBox1.Clear();
            }

        private void button3_Click(object sender, EventArgs e)
            {
            StreamWriter stream = File.CreateText(string.Format("{0}\\H8DCATALOG.TXT", label3.Text));
            if (stream != null)
                {
                stream.WriteLine("SEBHC DISK IMAGE CATALOG");
                stream.WriteLine("========================");
                stream.WriteLine(string.Format("{0} Disk Images", listBox1.Items.Count));
                stream.WriteLine(string.Format("{0} Total Files", FileCount));
                stream.WriteLine("");
                for (int i = 0; i < listBox2.Items.Count; i++)
                    {
                    string str = listBox2.Items[i].ToString();
                    stream.WriteLine(str);
                    }
                stream.Close();
                }
            MessageBox.Show(string.Format("Catalog text file saved to {0}\\H8DCATALOG.TXT", label3.Text));
            }

        private void button4_Click(object sender, EventArgs e)
            {
            //  dump to HTML document
            StreamWriter stream = File.CreateText(string.Format("{0}\\H8DCATALOG.HTML", label3.Text));
            if (stream != null)
                {
                int line = 0;
                stream.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">");
                stream.WriteLine("<!-- Created by Les Bird's H8D Utility program. http://www.lesbird.com/sebhc -->");
                stream.WriteLine("<html>");
                stream.WriteLine("<head>");
                stream.WriteLine("<meta content=\"text/html; charset=ISO-8859-1\"");
                stream.WriteLine("http-equiv=\"content-type\">");
                stream.WriteLine("<title></title>");
                stream.WriteLine("</head>");
                stream.WriteLine("<body>");
                stream.WriteLine("<div style=\"text-align: center;\">");
                stream.WriteLine("<p><big><big style=\"font-weight: bold;\"><big><span style=\"font-family: monospace;\">SEBHC DISK IMAGE CATALOG</span></big></big></big></p>");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine(string.Format("<span style=\"font-family: monospace;\">Disk Images: {0}</span><br>", listBox1.Items.Count));
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine(string.Format("<span style=\"font-family: monospace;\">Total Files: {0}</span><br>", FileCount));
                stream.WriteLine("<br>");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                for (int i = 0; i < listBox2.Items.Count; i++)
                    {
                    string str = listBox2.Items[i].ToString();
                    if (str.Contains(".H8D") || str.Contains("Total Files"))
                        {
                        stream.WriteLine("<hr style=\"width: 100%; height: 2px; font-family: monospace;\">");
                        stream.WriteLine("<br>");
                        line = 0;
                        }
                    if (line > 0 && line < 5 || str.Contains("========"))
                        {
                        stream.WriteLine(HTMLFormat(listBox2.Items[i].ToString(), false));
                        }
                    else
                        {
                        stream.WriteLine(HTMLFormat(listBox2.Items[i].ToString(), true));
                        }
                    stream.WriteLine("<br>");
                    line++;
                    }
                stream.WriteLine("<hr style=\"width: 100%; height: 2px; font-family: monospace;\">");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine("<span style=\"font-family: monospace;\">{0}</span><br>", DateTime.Now.ToString());
                stream.WriteLine("</div>");
                stream.WriteLine("</body>");
                stream.WriteLine("</html>");
                stream.Close();
                }
            MessageBox.Show(string.Format("Catalog HTML file saved to {0}\\H8DCATALOG.HTML", label3.Text));
            }

        private string HTMLFormat(string str, bool bold)
            {
            string html_formatted_str = str;
            for (int i = 0; i < html_formatted_str.Length; i++)
                {
                if (html_formatted_str[i] == ' ')
                    {
                    html_formatted_str = html_formatted_str.Remove(i, 1);
                    html_formatted_str = html_formatted_str.Insert(i, "&nbsp;");
                    }
                }
            if (bold)
                {
                return (string.Format("<span style=\"font-family: monospace; font-weight: bold;\">{0}</span>", html_formatted_str));
                }
            else
                {
                return (string.Format("<span style=\"font-family: monospace;\">{0}</span>", html_formatted_str));
                }
            }

        private void button8_Click(object sender, EventArgs e)
            {
            //  rename files
            }

        private void button5_Click(object sender, EventArgs e)
            {
            //  view file
            int idx = listBox2.SelectedIndex;
            if (idx != -1)
                {
                foreach (DiskFileEntry entry in DiskFileList)
                    {
                    if (entry.ListBox2Entry == idx)
                        {
                        ViewFile(entry.DiskImageName, entry);
                        return;
                        }
                    }
                }
            }

        private void ViewFile(string disk_image_file, DiskFileEntry disk_file_entry)
            {
            //  view the selected file
            if (FileViewerBorder.Visible)
                {
                FileViewerBox.Clear();

                FileViewerBorder.Visible = false;

                listBox2.Enabled = true;
                listBox1.Enabled = true;
                button9.Enabled = true;
                button7.Enabled = true;
                button4.Enabled = true;
                button3.Enabled = true;
                if (bImageList)
                    {
                    button2.Enabled = true;
                    }
                button1.Enabled = true;
                return;
                }

            FileStream file = File.OpenRead(disk_image_file);
            BinaryReader bin_file = new BinaryReader(file);
            byte[] buf = bin_file.ReadBytes(256);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
                {
                HDOSDiskInfo disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long)(bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long)(bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long)(bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.phys_sector_size = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                HDOSGrtTable = new byte[256];
                bin_file.BaseStream.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                int bytes_to_read = 0;
                int fsize = ComputeHDOSFileSize(disk_file_entry.HDOSEntry, disk_info.sectors_per_group);
                int grp = disk_file_entry.HDOSEntry.first_group_num;

                UTF8Encoding encoding = new UTF8Encoding();

                listBox1.Enabled = false;
                listBox2.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;
                button7.Enabled = false;
                button9.Enabled = false;

                FileViewerBorder.Visible = true;

                bool eof = false;

                do
                    {
                    int sector_addr = grp * (disk_info.sectors_per_group * 256);
                    bin_file.BaseStream.Seek(sector_addr, SeekOrigin.Begin);
                    bytes_to_read = disk_info.sectors_per_group * 256;
                    byte[] buffer = bin_file.ReadBytes(bytes_to_read);
                    for (int i = 0; i < buffer.Length; i++)
                        {
                        if (eof)
                            {
                            buffer[i] = 0;
                            }
                        else
                            {
                            if (buffer[i] == 0)
                                {
                                eof = true;
                                }
                            }
                        }
                    string t = encoding.GetString(buffer);
                    FileViewerBox.AppendText(t);
                    grp = HDOSGrtTable[grp];
                    } while (grp != 0 && !eof);

                bytes_to_read = disk_file_entry.HDOSEntry.last_sector_index * 256;
                if (bytes_to_read != 0)
                    {
                    byte[] buffer = bin_file.ReadBytes(bytes_to_read);
                    for (int i = 0; i < buffer.Length; i++)
                        {
                        if (eof)
                            {
                            buffer[i] = 0;
                            }
                        else
                            {
                            if (buffer[i] == 0)
                                {
                                eof = true;
                                }
                            }
                        }
                    string t = encoding.GetString(buffer);
                    FileViewerBox.AppendText(t);
                    }

                FileViewerBorder.BringToFront();
                FileViewerBox.BringToFront();
                }
            else
                {

                }
            }

        private void filebutton1_Click(object sender, EventArgs e)
            {
            button5_Click(sender, e);
            }

        // DCP added option for CP/M file
        private void button6_Click(object sender, EventArgs e)
            {
            if (MessageBox.Show("Click Yes for an HDOS File, No for CP/M", "DOS File Type", MessageBoxButtons.YesNo) ==
                DialogResult.Yes)
                addHDOS(sender, e);
            else
                {
                var cpmFile = new CPMFile();
                cpmFile.AddFileCPM();
                }
            }

        private void addHDOS(object sender, EventArgs e)
            {
            //  add file

            FileStream file = File.OpenRead("EMPTY1S40T.h8d");
            BinaryReader bin_file = new BinaryReader(file);
            long len = file.Length;
            byte[] diskbuf = new byte[len];
            bin_file.Read(diskbuf, 0, (int)len);
            file.Close();
            long diskImageLen = len;
            // diskbuf contains contents of the empty disk image to be filled in by a selection of files

            // initialize the file GRT allocation table
            FILEGrtAllocTable = new byte[256];
            System.Array.Clear(FILEGrtAllocTable, 0, 256);

            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                try
                    {
                    foreach (String filename in openFileDialog1.FileNames)
                        {
                        file = File.OpenRead(filename);
                        bin_file = new BinaryReader(file);
                        len = file.Length;
                        byte[] filebuf = new byte[len];
                        bin_file.Read(filebuf, 0, (int)len);
                        file.Close();
                        // write file data to disk image
                        InsertFile(ref diskbuf, ref filebuf, len, filename);
                        }

                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.DefaultExt = "H8D";
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                        {
                        string saveFileName = saveDialog.FileName.ToUpper();
                        BinaryWriter writer = new BinaryWriter(File.Open(saveFileName, FileMode.Create));
                        long length = diskImageLen;
                        writer.Write(diskbuf, 0, (int)length);
                        writer.Close();

                        MessageBox.Show(string.Format("Disk image {0} saved.", saveDialog.FileName), "DISK IMAGE SAVED");
                        }
                    }
                catch (Exception ex)
                    {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                    }
                }
            }

        private void InsertFile(ref byte[] diskbuf, ref byte[] filebuf, long len, string filename)
            {
            long diski = 0x0900; // diskbuf index
            long filei = 0; // filebuf index

            HDOSDiskInfo disk_info = new HDOSDiskInfo();
            disk_info.serial_num = diskbuf[diski]; // size 1
            disk_info.init_date = (ushort)(diskbuf[diski + 1]); // MakeBigEndian16((ushort)diskbuf[diski + 1]); // size 2
            disk_info.dir_sector = (long)(diskbuf[diski + 3] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 3]); // size 2
            disk_info.grt_sector = (long)(diskbuf[diski + 5] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 5]); // size 2
            disk_info.sectors_per_group = (byte)diskbuf[diski + 7]; // size 1
            disk_info.volume_type = (byte)diskbuf[diski + 8]; // size 1
            disk_info.init_version = (byte)diskbuf[diski + 9]; // size 1
            disk_info.rgt_sector = (long)(diskbuf[diski + 10] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 10]); // size 2
            disk_info.volume_size = (ushort)(diskbuf[diski + 12]); // MakeBigEndian16((ushort)diskbuf[diski + 12]); // size 2
            disk_info.flags = (byte)diskbuf[diski + 14]; // size 1
            disk_info.phys_sector_size = (ushort)(diskbuf[diski + 15]); // MakeBigEndian16((ushort)diskbuf[diski + 15]); // size 2
            // copy GRT table from disk buffer to working array
            HDOSGrtTable = new byte[256];
            System.Array.Copy(diskbuf, disk_info.grt_sector, HDOSGrtTable, 0, 256);

            // clear out the file GRT allocation table (do this for each file added to the image)
            System.Array.Clear(FILEGrtAllocTable, 0, 256);

            // write the file to the disk image
            string filename8 = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(filename8))
                {
                return;
                }
            filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
            filename8 = filename8.PadRight(8, ' ');
            string ext3 = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext3))
                ext3 = "   ";
            else
                ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));

            ext3 = ext3.PadRight(3, ' ');
            InsertHDOSDirEntry(ref diskbuf, ref disk_info, filename8, ext3, len);

            // copy file contents to diskbuf
            int n = 0;
            long k = 0;
            int group_size = disk_info.sectors_per_group * 256;
            int grp = 0;
            while (len > 0)
                {
                grp = FILEGrtAllocTable[n++];
                long bytes_to_copy = Math.Min(group_size, len);
                if (grp != 0)
                    {
                    long addr = grp * group_size;
                    System.Array.Copy(filebuf, k, diskbuf, addr, bytes_to_copy);
                    k += bytes_to_copy;
                    HDOSGrtTable[grp] = FILEGrtAllocTable[n];
                    }
                len -= bytes_to_copy;
                }

            // copy GRT table from working array back to disk buffer
            System.Array.Copy(HDOSGrtTable, 0, diskbuf, disk_info.grt_sector, 256);
            }

        private void InsertHDOSDirEntry(ref byte[] diskbuf, ref HDOSDiskInfo disk_info, string filename8, string ext3, long len)
            {
            long i = disk_info.dir_sector;
            long entry_len = 23;
            long max_entries = entry_len * 44;
            bool more_entries = true;
            while (more_entries)
                {
                if (diskbuf[i] == 0xFF || diskbuf[i] == 0xFE)
                    {
                    char[] c = filename8.ToCharArray();
                    bool z = false;
                    for (int n = 0; n < 8; n++)
                        {
                        if (!z && ((c[n] >= '0' && c[n] <= '9') || (c[n] >= 'A' && c[n] <= 'Z') || c[n] == '-' || c[n] == '_'))
                            diskbuf[i++] = (byte)c[n];
                        else
                            {
                            diskbuf[i++] = 0;
                            z = true;
                            }
                        }
                    c = ext3.ToCharArray();
                    z = false;
                    for (int n = 0; n < 3; n++)
                        {
                        if (!z && ((c[n] >= '0' && c[n] <= '9') || (c[n] >= 'A' && c[n] <= 'Z') || c[n] == '-' || c[n] == '_'))
                            {
                            diskbuf[i++] = (byte)c[n];
                            }
                        else
                            {
                            diskbuf[i++] = 0;
                            z = true;
                            }
                        }
                    diskbuf[i++] = 0;
                    diskbuf[i++] = 0;
                    diskbuf[i++] = 3;
                    diskbuf[i++] = 0;
                    diskbuf[i++] = 0;
                    byte grtfirst = 0; // need to compute this from RGT table
                    byte grtlast = 0; // need to compute this from RGT table
                    byte secindex = 0; // need to compute this
                    GetGRTFirstLast(ref grtfirst, ref grtlast, ref secindex, disk_info.sectors_per_group, len);
                    diskbuf[i++] = grtfirst;
                    diskbuf[i++] = grtlast;
                    diskbuf[i++] = secindex;
                    diskbuf[i++] = 0x63;
                    diskbuf[i++] = 0x19;
                    diskbuf[i++] = 0x63;
                    diskbuf[i++] = 0x19;
                    return;
                    }
                i += entry_len;
                }
            }

        private void GetGRTFirstLast(ref byte grtfirst, ref byte grtlast, ref byte secindex, byte sectors_per_group, long len)
            {
            grtfirst = HDOSGrtTable[0];
            grtlast = 0;
            secindex = 0;
            int i = grtfirst;
            int n = 0;
            while (true)
                {
                FILEGrtAllocTable[n++] = (byte)i;
                len -= (sectors_per_group * 256);
                if (len <= 0)
                    {
                    grtlast = (byte)i;
                    len = (Math.Abs(len) / 256) + 1;
                    secindex = (byte)len;
                    HDOSGrtTable[0] = HDOSGrtTable[i];
                    return;
                    }
                i = HDOSGrtTable[i];
                }
            }

        //************************* Extract a file *******************
        // process a file list from form 1
        private void button7_Click(object sender, EventArgs e)
            {
            //  extract file
            int files_extracted = 0;
            var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
            var diskTotal = 0;


            int idx = listBox2.SelectedIndex;
            if (idx != -1)
                {
                for (int i = 0; i < listBox2.SelectedItems.Count; i++)
                    {
                        idx = listBox2.SelectedIndices[i];
                    foreach (DiskFileEntry entry in DiskFileList)
                        {
                         if (entry.ListBox2Entry == idx)
                            {
                            // dcp changed Extract file to return 1 if successful
                            if (entry.DiskImageName.Contains(".IMD"))
                            {
                                //var fileNameList = getCpmFile.ReadImdDir(entry.DiskImageName, ref diskTotal);
                                files_extracted += getCpmFile.ExtractFileCPMImd(entry);
                            }
                            else
                                files_extracted += ExtractFile(entry);
                            break;
                            }
                        }
                    }
                listBox2.ClearSelected();
                }
            else
                {
                if (MessageBox.Show(string.Format("There are a total of {0} files. Extract all files?", DiskFileList.Count), "EXTRACT FILES", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                    foreach (DiskFileEntry entry in DiskFileList)
                        {
                        files_extracted += ExtractFile(entry);
                        }
                    }
                }

            if (files_extracted > 0)
                {
                string message = string.Format("{0} file(s) extracted", files_extracted);
                MessageBox.Show(this, message, "H8D Utility");
                }
            }

        private int ExtractFile(DiskFileEntry disk_file_entry)
            {
            int result = 1;             // dcp extracted file count to deal with CP/M file extract fail
            var disk_image_file = disk_file_entry.DiskImageName;
            var file = File.OpenRead(disk_image_file);
            BinaryReader bin_file = new BinaryReader(file);
            byte[] buf = bin_file.ReadBytes(256);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
                {
                HDOSDiskInfo disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort)bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long)(bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long)(bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long)(bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.phys_sector_size = (ushort)(bin_file.ReadUInt16()); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved = (ushort)(bin_file.ReadUInt16()); // bin_file.ReadByte(); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                HDOSGrtTable = new byte[256];
                bin_file.BaseStream.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                UTF8Encoding encoding = new UTF8Encoding();

                string dir = string.Format("{0}_Files", disk_image_file);
                if (!Directory.Exists(dir))
                    {
                    Directory.CreateDirectory(dir);
                    }

                string delims = " \0";
                string name = string.Format("{0}", encoding.GetString(disk_file_entry.HDOSEntry.filename)).Trim(delims.ToCharArray());
                string ext = string.Format("{0}", encoding.GetString(disk_file_entry.HDOSEntry.fileext)).Trim(delims.ToCharArray());
                string file_name = string.Format("{0}\\{1}.{2}", dir, name, ext);

                FileStream file_out = File.Create(file_name);
                BinaryWriter bin_out = new BinaryWriter(file_out);

                int bytes_to_read = 0;
                int fsize = ComputeHDOSFileSize(disk_file_entry.HDOSEntry, disk_info.sectors_per_group);
                int grp = disk_file_entry.HDOSEntry.first_group_num;

                bool eof = false;
                bool is_text = false;
                bool file_check = false;

                do
                    {
                    int sector_addr = grp * (disk_info.sectors_per_group * 256);
                    bin_file.BaseStream.Seek(sector_addr, SeekOrigin.Begin);
                    bytes_to_read = disk_info.sectors_per_group * 256;
                    byte[] buffer = bin_file.ReadBytes(bytes_to_read);
                    if (!file_check)
                        {
                        if (buffer[0] == 0x09 || buffer[0] == 0x0A || buffer[0] == 0x0D || buffer[0] >= 0x20 && buffer[0] < 0x7F)
                            {
                            is_text = true;
                            }
                        file_check = true;
                        }
                    if (is_text)
                        {
                        for (int i = 0; i < buffer.Length; i++)
                            {
                            if (!eof)
                                {
                                if (buffer[i] == 0)
                                    {
                                    eof = true;
                                    }
                                }
                            else
                                {
                                buffer[i] = 0;
                                }
                            }
                        }
                    bin_out.Write(buffer);
                    grp = HDOSGrtTable[grp];
                    } while (grp != 0 && !eof);

                bytes_to_read = disk_file_entry.HDOSEntry.last_sector_index * 256;
                if (bytes_to_read != 0)
                    {
                    byte[] buffer = bin_file.ReadBytes(bytes_to_read);
                    if (is_text)
                        {
                        for (int i = 0; i < buffer.Length; i++)
                            {
                            if (!eof)
                                {
                                if (buffer[i] == 0)
                                    {
                                    eof = true;
                                    }
                                }
                            else
                                {
                                buffer[i] = 0;
                                }
                            }
                        }
                    bin_out.Write(buffer);
                    }

                file_out.Close();
                }
            else
                {
                // dcp Add CPM Extract
                var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
                result = getCpmFile.ExtractFileCPM(disk_file_entry);
                }

            return result;
            }


        private void button9_Click(object sender, EventArgs e)
            {
            Form h89ldr = new Form2();
            h89ldr.ShowDialog();
            this.Refresh();
            }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
            {
            if (textBox2.ReadOnly)
                {
                return;
                }

            textBox2.Clear();
            textBox2.Enabled = false;
            button10.Enabled = false;
            RelabelEntry.ListBox2Entry = -1;
            RelabelEntry.DiskImageName = "";
            RelabelEntry.DiskLabelName = "";

            int idx = listBox2.SelectedIndex;
            if (idx != -1)
                {
                if (listBox2.SelectedItems.Count != 1)
                    {
                    return;
                    }
                foreach (DiskLabelEntry entry in DiskLabelList)
                    {
                    if (entry.ListBox2Entry == idx)
                        {
                        textBox2.Text = entry.DiskLabelName;
                        textBox2.Enabled = true;
                        RelabelEntry = entry;
                        break;
                        }
                    }
                }
            }

        private void textBox2_TextChanged(object sender, EventArgs e)
            {
            if (!button10.Enabled)
                {
                button10.Enabled = true;
                }
            }

        private void button10_Click(object sender, EventArgs e)
            {
            textBox2.ReadOnly = true;
            if (MessageBox.Show(string.Format("Change selected HDOS disk label to \"{0}\". This will change the label inside the disk file. Are you sure?", textBox2.Text), "RELABEL DISK", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                if (RelabelEntry.ListBox2Entry != -1)
                    {
                    listBox2.Items[RelabelEntry.ListBox2Entry] = textBox2.Text;
                    string s = textBox2.Text.PadRight(60, ' ');
                    RelabelEntry.DiskLabelName = s;
                    RelabelDisk();
                    }
                }
            textBox2.ReadOnly = false;
            }

        private void RelabelDisk()
            {
            UTF8Encoding encoding = new UTF8Encoding();
            BinaryWriter write_stream = new BinaryWriter(File.OpenWrite(RelabelEntry.DiskImageName));

            write_stream.Seek(0x0911, SeekOrigin.Begin);    //  seek to label position in disk image
            write_stream.Write(encoding.GetBytes(RelabelEntry.DiskLabelName), 0, RelabelEntry.DiskLabelName.Length);

            write_stream.Close();
            }

        private void button11_Click_1(object sender, EventArgs e)
            {
            Form svd_panel = new Form3();
            svd_panel.ShowDialog();
            this.Refresh();
            }

        private void button12_Click(object sender, EventArgs e)
            {
            //MessageBox.Show("This is a fairly stable release of Les Bird's H89 emulator. Click OK to continue");
            Form4 emulator = new Form4();
            emulator.ShowDialog();
            emulator.Unload();
            this.Refresh();
            }
        }
    }
