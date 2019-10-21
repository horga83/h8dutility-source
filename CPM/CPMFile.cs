using H8DReader;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CPM
    {
    public class CPMFile
        {
        // test bit 7 for the following filename chars
        private const byte FRead = 9; //  Read only
        private const byte FSys = 10; // 1 = true
        private const byte FChg = 11; // bit 7 means file changed
        private const byte FMask = 0x80; // bit mask
        private const int BuffLen = 0x2000; // buffer size

        // Disk types
        private const byte H37disktype = 5; // location of H37 disk type
        private const byte DISK_ROW = 5;

        private const byte DISK_VAL = 6;

        /*
        Disk type: byte 5 in sector 0 on H-37 disks (starting from 0) to define disk parameters
        Allocation Block size: number of bytes in an the smallest block used by CP/M on the disk. must be a multiple of 128 (0x80)
        Directory Stat: start of directory entries in bytes
        Allocation Block Number Size: number of bytes used in directory entry to reference an allocation block
        Dir Size: number of bytes used for the directory
        */
        // Disk type, Allocation block size, Directory start, Allocation block byte size, dir size, interleave, Sectors per Track, Sector Size
        private int[,] DiskType =
        {
            {0x6f, 0x800, 0x2800, 2, 0x2000, 3, 5, 1024}, // H37 96tpi ED DS
            {0x62, 0x400, 0x2000, 1, 0x1000, 3, 16, 256}, // H37 48tpi DD DS
            {0x63, 0x400, 0x2000, 1, 0x2000, 3, 9, 512}, // H37 48tpi DD DS
            {0x6b, 0x800, 0x2000, 2, 0x2000, 3, 16, 256}, // H37 48tpi ED DS
            {0x00, 0x100, 0x1e00, 1, 0x800, 4, 10, 256}, // H17 48tpi SD SS
            {0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 256} // Default H17 48tpi SD SS
        };


        /*
        H37 disk identification at byte 6 on the first sector
        MSB = 6 for H37
        LSB
        Bit 4 1 = 48tpi in 96tpi drive
        Bit 3 0 = 48 tpi drive, 1 = 96 tpi
        Bit 2 1 = extended double density
        Bit 1 1 = double density, 0 = single density
        Bit 0 1 = double sided, 0 = single sided
        */
        private string fname;
        private byte[] fnameb;
        private bool readOnly; // Read only file
        private bool sys; // system file
        private bool chg; // disk changed - not used
        private uint fsize; // file size 
        private List<FCBlist> FCBfirst;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FCBlist : IComparable<FCBlist>
            {
            public byte[] fcb { get; set; } // 16 file control block numbers

            //FCBlist* nxtptr; // pointer to next fcb it needed
            public int fcbnum { get; set; } // number of 128 byte records in this extant
            public int extantnum { get; set; } // extant number

            public FCBlist()
                {
                fcb = new byte[16];
                fcb[0] = 0;
                fcbnum = 0;
                extantnum = 0;
                }

            public int Compare(FCBlist x, FCBlist other)
                {
                if (other == null) return 1;
                if (x.extantnum > other.extantnum) return 1;
                else if (x.extantnum == other.extantnum)
                    return 0;
                else return -1;
                }

            public int CompareTo(FCBlist other)
                {
                if (other == null) return 1;
                if (this.extantnum > other.extantnum) return 1;
                else if (this.extantnum == other.extantnum)
                    return 0;
                else return -1;
                }
            }

        //private FCBlist FCBfirst;


        //CPMDisk* diskptr;  // pointer to next file name

        // Constructor
        // CPMDisk() : fname(), readonly (false), sys(false), chg(false)
        public CPMFile()
            {
            fname = "";
            fnameb = new byte[11] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            readOnly = false;
            sys = false;
            chg = false;
            FCBfirst = new List<FCBlist>();
            }


        // destructor
        ~CPMFile()
            {
            }
    //************ Add  CP/M File
    /*
     * Open CP/M File
     * Process list of Windows files
     * Return successful file additions to disk
     * Calls InsertFileCpm()
     */

    public void AddFileCPM()
    {
        var maxDiskLen = 10 * 256 * 40;   // assume H8D H17 format
        int result = 0,
            fileCnt =0,
            albNum = 2;             // first available allocation block
        byte [] cpmBuff = new byte[maxDiskLen];
        for (var i = 0; i < maxDiskLen; i++) cpmBuff[i] = 0xe5;      // Format disk

        var startDir = H8DReader.Form1.label3str;   // check if a working folder is selected
//        FileStream file = null;

        if (startDir == "") startDir = "c:\\";
        var openFileDialog1 = new OpenFileDialog();
        openFileDialog1.InitialDirectory = startDir;
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
                    var file = File.OpenRead(filename);
                    var len = file.Length;                      // read entire file into buffer
                    byte[] filebuf = new byte[len];
                    var bin_file = new BinaryReader(file);
                    bin_file.Read(filebuf, 0, (int) len);
                    file.Close();
                    fileCnt++;
                    if (len > maxDiskLen - 0x1ee - 0x800) break;  // file is too large
                    // write file data to disk image
                    result += InsertFileCpm(ref cpmBuff, ref filebuf, len, filename, ref albNum);
                    var ttx = result;
                }

                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.DefaultExt = "H8D";
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string saveFileName = saveDialog.FileName.ToUpper();
                    BinaryWriter writer = new BinaryWriter(File.Open(saveFileName, FileMode.Create));
                    writer.Write(cpmBuff, 0, (int) maxDiskLen);
                    writer.Close();

                    MessageBox.Show(string.Format("Disk image {0} saved. {1} Files saved. {2} Files not Saved.", saveDialog.FileName, result, fileCnt - result), "DISK IMAGE SAVED");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
            }
        }
    }
// ************** Insert File CP/M *********************
/*
 * Assumes .H8D format, directory entries are written sequentially
 */
            private int InsertFileCpm(ref byte[] cpmBuff, ref byte[] fileBuff, long len, string filename, ref int albNum)
            {
                int result = 1;
                int allocBlock = 1024,
                    dirStart = 0x1e00,
                    allocBlockNum = 0,
                    dirSize = 0x800,
                    interleave = 4,
                    spt = 10,
                    sectorSize = 0x100; // disk parameter values
                int albNumt = albNum;       // save in case there is an issue
                long diski = dirStart,   // CP/M disk index
                    filei = 0;          // file buffer index
                byte extent = 0,
                    extentNum  = 0;
                int[] extList = new int[dirSize/32];
                int extListi = 0;

                var skewMap =
                    new int[16, 2]; // max number of sectors on a Heathkit floppy. Fill must be greater than buffer size

                for (var i = 0; i < 16; i++) // initialize skew table
                {
                    skewMap[i, 0] = 32;
                    skewMap[i, 1] = i;
                }

                // write the file to the disk image
                string filename8 = Path.GetFileNameWithoutExtension(filename);
                if (string.IsNullOrEmpty(filename8))
                {
                    return 0;
                }
                BuildSkew(ref skewMap,interleave,spt);
                var encoding = new ASCIIEncoding();
                filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
                filename8 = filename8.PadRight(8, ' ');
                string ext3 = Path.GetExtension(filename);
                if (string.IsNullOrEmpty(ext3))
                {
                    ext3 = "   ";
                }
                else
                {
                    ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));
                }
                ext3 = ext3.PadRight(3, ' ');
                var filenameb = string.Format(filename8 + ext3).ToUpper();
                while (filei < len)
                {
                    // find empty directory entry
                    diski = dirStart;
                    while (diski < dirStart + dirSize)
                    {
                        if (cpmBuff[diski] == 0xe5) break;
                        else diski += 32;
                    }

                    if (diski >= dirStart + dirSize)
                    {
                        // not enough room on disk, erase directory entries used so far
                        while (extListi >= 0) if(extList[extListi] > 0) cpmBuff[extList[extListi--]] = 0xe5;
                        albNum = albNumt;
                        result = 0;
                        break;
                    }
                    // write upto 0x80 1128 byte CP/M records
                    extList[extListi++] = (int) diski;          // list of disk entries to erase in case of failure
                    cpmBuff[diski] = 0;
                    char[] fn = filenameb.ToCharArray();
                    for (var i = 1; i < 12; i++) cpmBuff[diski+i] = (byte) fn[i-1];           // copy file name to dir entry
                    for (var i = 12; i < 32; i++) cpmBuff[diski + i] = 0;                   // zero out extent list and remaining bytes in directory entry

                    // update extent number and records in this extent
                    var start = albNum * allocBlock; // start of allocation block (AB) to read
                    var trackSize = spt * sectorSize;
                    var numTracksBlocks = start / trackSize; // integer number of tracks in block
                    var trackCnt = (float)trackSize / (float)allocBlock;
                    var sectorBlock = allocBlock / sectorSize; // sectors per AB
                    if (trackCnt % 2 != 0) trackCnt = 2;
                    else trackCnt = 1; // number of tracks for skew calculation
                    var minBlock = trackSize * (int)trackCnt; // minimum disk size to deal with due to skewing
                    var albCnt = 0;

                    // copy data to correct place in disk buffer using skew information
                    var basePtr = albNum * allocBlock + dirStart - (albNum * allocBlock)%minBlock;
                    while (filei < len && albCnt < 16)             // write up to 16 allocation blocks for this directory entry
                    {
                        int t1 = (albNum % (minBlock / allocBlock));
                        var startSector = (albNum % (minBlock / allocBlock)) * sectorBlock;
                        for (var i = 0; i < sectorBlock; i++)
                        {
                            var diskPtr =0;
                            if (startSector + i < spt)
                            {
                                t1 = skewMap[startSector + i, 1];
                                diskPtr = skewMap[startSector + i, 1] * sectorSize + basePtr;
                            }
                            else
                            {
                                t1 = skewMap[startSector + i - spt, 1];
                                diskPtr = skewMap[startSector + i - spt, 1] * sectorSize + trackSize + basePtr;
                            }

                            for (var ctrIndex = 0; ctrIndex < sectorSize; ctrIndex++)
                                if (filei < len)
                                    cpmBuff[diskPtr++] = fileBuff[filei++];
                                else cpmBuff[diskPtr++] = 0;                        // pad with zero's to the end of the CP/M block
                        }

                        cpmBuff[diski + 16 + albCnt++] = (byte) albNum++;
                        basePtr = albNum * allocBlock + dirStart - (albNum * allocBlock) % minBlock;
                    }

                    cpmBuff[diski + 12] = extentNum++;
                    var t2 = (byte) (albCnt * allocBlock / 128);
                    cpmBuff[diski + 15] = (byte) (albCnt * allocBlock/128);


                }

                return result;
            }
    // ************** Extract File CP/M  *********************
        public int ExtractFileCPM(string disk_image_file, Form1.DiskFileEntry disk_file_entry)
            {
            var result = 1; // assume success
            int temp;
            var diskType = 0;
            var ctr = 0; // for loop counter
            var maxBuffSize = 0x800;            // largest allocation block size
            int allocBlock = 0,
                dirStart = 0,
                allocBlockNum = 0,
                dirSize = 0,
                interleave = 0,
                spt = 0,
                sectorSize = 0; // disk parameter values
            var buff = new byte[maxBuffSize]; // disk buffer equal to largest allocation block size
            var wBuff = new byte[maxBuffSize];            //   write buffer
            //var directory = new List<FCBlist>();
            var skewMap =
                new int[16, 2]; // max number of sectors on a Heathkit floppy. Fill must be greater than buffer size

            for (var i = 0; i < 16; i++) // initialize skew table
                {
                skewMap[i, 0] = 32;
                skewMap[i, 1] = i;
                }

            // var cpmFile = new CPMFile();
            var temp1 = FCBfirst.Count;

            var encoding = new UTF8Encoding();
            var dir = string.Format("{0}_Files",
                disk_image_file); // create directory name and check if directory exists
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            fnameb = encoding.GetBytes(disk_file_entry.FileName);
            /*
            FCBlist start = new FCBlist // initialize FCB list
                {
                fcb = new byte[16],
                fcbnum = 0,
                extantnum = 0
                };
                */
            // Create output File
            var name = disk_file_entry.FileName.Substring(0, 8).Trim(' ');
            var ext = disk_file_entry.FileName.Substring(8, 3).Trim(' ');
            var file_name = string.Format("{0}\\{1}.{2}", dir, name, ext);

            if (File.Exists(file_name))
                {
                if (MessageBox.Show("File exists, Overwrite it?", "File Exists", MessageBoxButtons.YesNo) ==
                    DialogResult.No)
                    {
                    result = 0;
                    return result;
                    }
                else
                    {
                    file_name = file_name + "1";
                    }
                }

            var file_out = File.Create(file_name);
            var bin_out = new BinaryWriter(file_out);


            // Open input file
            var file = File.OpenRead(disk_image_file);
            var bin_file = new BinaryReader(file);
            temp = bin_file.Read(buff, 0, 256);
            var t = buff.Length;
            diskType = (int)buff[5];
            /* debug
             ctr = DiskType.GetLength(0);
            ctr = DiskType.GetLength(1);
            */
            for (ctr = 0; ctr < DiskType.GetLength(0); ctr++) // search DiskType array for values
                if (diskType == DiskType[ctr, 0])
                    {
                    allocBlock = DiskType[ctr, 1]; // ALB Size
                    dirStart = DiskType[ctr, 2]; // physical start of directory
                    allocBlockNum = DiskType[ctr, 3]; // bytes used for ALB
                    dirSize = DiskType[ctr, 4]; // size of the directory
                    interleave = DiskType[ctr, 5]; // disk interleave
                    spt = DiskType[ctr, 6]; // sectors per track  
                    sectorSize = DiskType[ctr, 7]; // sector size
                    break;
                    }

            // error if no match found
            if (ctr == DiskType.GetLength(0))
                {
                MessageBox.Show("Error -  CP/M Disk Type not found", "Error", MessageBoxButtons.OK);
                }
            else
                {
                // read the directory
                BuildSkew(ref skewMap, interleave, spt);
                for (var i = 0; i < dirSize / allocBlock; i++)
                    GetALB(ref buff, allocBlock * i, bin_file, i, dirStart, allocBlock, sectorSize, spt, skewMap);
                buildDir(ref buff, dirSize, name, ext, ref FCBfirst, fnameb);
                foreach (var f in FCBfirst)
                    {
                    var fcbNum = f.fcbnum;
                    var cpmAlb = allocBlock / 128;              // number of 128 byte CP/M records in the allocation block
                    var rBptr = 0;
                    var wBptr = 0;
                    for (var i = 0; i < 16; i++)
                        if(f.fcb[i] > 0)
                        {
                            rBptr = wBptr = 0;
                            GetALB(ref buff, 0, bin_file, f.fcb[i], dirStart, allocBlock, sectorSize, spt, skewMap);
                            for (var j = 0; j < cpmAlb; j++)
                            {
                                if (fcbNum > 0)
                                {
                                    for (var l = 0; l < 128; l++) wBuff[wBptr++] = buff[rBptr++];
                                }
                                fcbNum--;
                            }
                            bin_out.Write(wBuff, 0, wBptr);
                        }


                    }
                }
            bin_out.Close();
            file_out.Close();

            bin_file.Close();
            file.Close();
            return result;
            }

        // ******************* read CP/M directory from buffer for given file name
        private void buildDir(ref byte[] buff, int dirSize, string name, string ext, ref List<FCBlist> fcbList,
            byte[] fNameB)
            {
            int bPtr = 0, bPtr1 = 0;

            while (bPtr < dirSize)
                {
                if (buff[bPtr] != 0xe5) // valid directory entry
                    {
                    for (bPtr1 = 0; bPtr1 < 11; bPtr1++)
                        if ((byte)(buff[bPtr + bPtr1 + 1] & 0x7f) != fNameB[bPtr1])
                            break;

                    if (bPtr1 == 11) // filename matches
                        {
                        var temp = new FCBlist();
                        temp.extantnum = buff[bPtr + 12];
                        temp.fcbnum = buff[bPtr + 15];
                        for (var i = 16; i < 32; i++) temp.fcb[i - 16] = buff[bPtr + i];
                        FCBfirst.Add(temp);
                        }
                    }

                bPtr += 32;
                }
            FCBfirst.Sort();
            var testStr = "";
            foreach (var f in FCBfirst)
                for (var i = 0; i < 16; i++)
                    testStr = testStr + f.fcb[i].ToString() + " ";
            }

        // **************** Read one Allocation Block into buffer at offset
        private void GetALB(ref byte[] buf, int buffOffset, BinaryReader fp, int alb, int dirStart, int blockSize,
            int sectorSize, int spt, int[,] skew)
            {
            var start = alb * blockSize; // start of allocation block (AB) to read
            var trackSize = spt * sectorSize;
            var numTracksBlocks = start / trackSize; // integer number of tracks in block
            var trackCnt = (float)trackSize / (float)blockSize;
            var sectorBlock = blockSize / sectorSize; // sectors per AB
            if (trackCnt % 2 != 0) trackCnt = 2;
            else trackCnt = 1; // number of tracks for skew calculation
            var minBlock = trackSize * (int)trackCnt; // minimum disk size to deal with due to skewing
            var localBuf = new byte[minBlock];

            var startBlock = start / minBlock * minBlock; // allocation block to read

            fp.BaseStream.Seek(dirStart + startBlock, SeekOrigin.Begin);

            try
            {
                var tx = fp.Read(localBuf, 0, minBlock);
                var errStr = "Error -  buffer read error, "+tx + " bytes read, " + minBlock + " bytes requested";
                if (tx != minBlock)            // read in enough data to handle skew conversion
                    MessageBox.Show(errStr,"Error", MessageBoxButtons.OK);
                ;
                }
            catch
                {
                MessageBox.Show("Error -  CP/M file read error", "Error", MessageBoxButtons.OK);
                }

            var startSector = (alb % (minBlock / blockSize)) * sectorBlock;
            var bufIndex = buffOffset;
            var bufLocalIndex = 0;
            int t1;
            for (var i = 0; i < sectorBlock; i++)
                {
                if (startSector + i < spt)
                    {
                    t1 = skew[startSector + i, 1];
                    bufLocalIndex = skew[startSector + i, 1] * sectorSize;
                    }
                else
                    {
                    t1 = skew[startSector + i - spt, 1];
                    bufLocalIndex = skew[startSector + i - spt, 1] * sectorSize + trackSize;
                    }

                for (var ctrIndex = 0; ctrIndex < sectorSize; ctrIndex++) buf[bufIndex++] = localBuf[bufLocalIndex++];
                }
            }

//********************** Build Skew array for one Track *********************
        public void BuildSkew(ref int[,] skew, int intLv, int size)
            {
            var physicalS = 0;
            var logicalS = 0;
            var temp = new int[16, 2];
            var t = 0;

            while (logicalS < size)
                {
                if (skew[physicalS, 0] > size) // logical position not yet filled
                    {
                    skew[physicalS, 0] = (byte)logicalS++;
                    physicalS += intLv;
                    }
                else
                    {
                    physicalS++; // bump to next physical position
                    }

                if (physicalS >= size) physicalS = physicalS - size;
                }

            // convert to logical to physical format
            logicalS = 0;
            while (logicalS < size)
                {
                if (logicalS == skew[logicalS, 0])
                    {
                    temp[logicalS, 0] = logicalS;
                    temp[logicalS, 1] = skew[logicalS, 1];
                    }
                else
                    {
                    t = 0;
                    while (t < size && skew[t, 0] != logicalS) t++;
                    temp[logicalS, 0] = logicalS;
                    temp[logicalS, 1] = skew[t, 1];
                    }

                logicalS++;
                }

            for (t = 0; t < size; t++)
                {
                skew[t, 0] = temp[t, 0];
                skew[t, 1] = temp[t, 1];
                }
            }
        }
    }