﻿using H8DReader;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
        /********** data values for reading .IMD disks       */
        private const int bufferSize = 800*1024;
        private byte[] buf = new byte[bufferSize];
        private const int sectorMax = 16 * 160;          // max number of tracks
        private int[] diskMap = new int[sectorMax];      // an array of buffer pointers in buf[] for each sector on the disk starting with track 0, side 0
        private int albNumSize = 2; // size of ALB size in directory
        private int albSize = 512;  // size of an alloction block
        private int dirSectStart = 15;          // starting sector for disk directory counting from 0. Also first ALB.
        private string DiskImageImdActive = "";
        private long diskSize = 0;
        private int sectorSize = 256;
        private List<DirList> fileNameList = new List<DirList>() ;

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
            {0x6f, 0x800, 0x2800, 2, 0x2000, 3, 5, 1024, 160}, // H37 96tpi ED DS
            {0x62, 0x400, 0x2000, 1, 0x1000, 3, 16, 256, 80}, // H37 48tpi DD SS
            {0x63, 0x400, 0x2000, 1, 0x2000, 3, 9, 512, 80}, // H37 48tpi DD DS
            {0x67, 0x400, 0x2800, 2, 0x1000, 3, 5, 1024, 160}, // H37 48tpi ED SS
            {0x6b, 0x800, 0x2000, 2, 0x2000, 3, 16, 256, 80}, // H37 48tpi ED DS
            {0x00, 0x100, 0x1e00, 1, 0x800, 4, 10, 256, 40}, // H17 48tpi SD SS
            {0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 256, 40} // Default H17 48tpi SD SS
        };


        /*
        H37 disk identification at byte 6 on the first sector
        MSB = 6 for H37
        LSB
        Bit 4 1 = 48tpi in 96tpi drive
        Bit 3 0 = 48 tpi drive, 1 = 96 tpi
        Bit 2 1 = extended double density, used in conjunction with bit 1 (0110B)
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

        public class DirList : IComparable<DirList>
            {
            public string fname;            // filename plus extension in 8 + " " + 3 format
            public byte[] fnameB = new byte[11];               // byte array version of file name
            public int fsize;              // file size in Kb
            public string flags;            // flags for system and R/O
            public int fcbNumSize;
            public List<FCBlist> fcbList;

            public DirList()
                {
                fname = "";
                fsize = 0;
                fcbList = new List<FCBlist>();
                fcbNumSize = 1;
                }

            public DirList(string tFname, int tFsize, string tFlags)
                {
                this.fname = tFname;
                this.fsize = tFsize;
                this.flags = tFlags;
                this.fcbList = new List<FCBlist>();
                fcbNumSize = 1;

                }
            public int CompareTo(DirList other)
                {
                if (other == null) return 1;
                return string.Compare(this.fname, other.fname);
                }

            public bool Equals(DirList other)
                {
                if (other == null) return false;
                return (this.fname.Equals(other.fname));
                }
            }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FCBlist : IComparable<FCBlist>
            {
            public int[] fcb { get; set; } // 16 file control block numbers

            //FCBlist* nxtptr; // pointer to next fcb it needed
            public int fcbnum { get; set; } // number of 128 byte records in this extant
            public int extantnum { get; set; } // extant number

            public FCBlist()
                {
                fcb = new int[16];
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

        //*************** Read IMD Directory
        public List<DirList> ReadImdDir(string diskFileName, ref long diskTotal)
            {
            // Check if file already in memory. If not, then process
            // open file: fileName
            // check H37 file type in byte 6
            // get disk parameters
            // Read directory gathering file names and sizes
            // update fcbList with fcb list for each file
            // add file names listBox2.Items
            // update file count and total file size
            var sectorSizeList = new int[] { 128, 256, 512, 1024, 2048, 4096, 8192 };     // IMD values
            int result = 0;
            UTF8Encoding encoding = new UTF8Encoding();

            if (diskFileName != DiskImageImdActive) // check if data already in memory
            {
                FileStream file = File.OpenRead(diskFileName); // read entire file into an array of byte
                BinaryReader fileByte = new BinaryReader(file);
                Int32 fileLen = (int) file.Length;
                //byte[] buf = new byte[bufferSize];
                try
                {
                    if (fileByte.Read(buf, 0, bufferSize) != fileLen)
                    {
                        MessageBox.Show("IMD file read error", "Error", MessageBoxButtons.OK);
                        return null;
                    }
                }
                catch
                {
                    MessageBox.Show("File buffer too small", "Error", MessageBoxButtons.OK);
                    return null;
                }

                DiskImageImdActive = diskFileName;
                diskSize = fileLen;
                fileNameList.Clear();

            }
            else return fileNameList;       // list is current do nothing

            int bufPtr = 0, firstSector;
            while (buf[bufPtr] != 0x1a && bufPtr < bufferSize) bufPtr++;             // look for end of text comment in IMD file
            if (bufPtr < bufferSize && buf[bufPtr + 1] < 6) // process as IMD file
                {
                bufPtr += 4;
                var spt = buf[bufPtr++]; // sectors per track
                sectorSize = sectorSizeList[buf[bufPtr]];
                var skewMap = new int[spt];

                for (var i = 0; i < spt; i++) skewMap[i] = buf[++bufPtr]; // load skewmap

                firstSector = ++bufPtr;
                int ctr,
                    //allocBlock = 0,
                    //albNumSize = 1,
                    dirStart = 0,
                    dirSizeD = 0,
                    sptD = 0,
                    sectorSizeD = 0,
                    numTrack = 0;

                //
                // map sectors
                // bufPtr already points to first sector marker
                /**** use class variable for the following two variables ************/
                // var sectorMax = spt * 160;          // max number of tracks
                //var diskMap = new int[sectorMax];
                var sectorCnt = 0;
                //var filePtr = firstSector;
                while (sectorCnt < sectorMax)
                    {
                    //int t1 = sectorCnt % spt;
                    //int t2 = skewMap[sectorCnt % spt];
                    //int t3 = (sectorCnt / spt) * spt;
                    diskMap[(sectorCnt / spt) * spt + skewMap[sectorCnt % spt] - 1] = bufPtr;         // bufPtr points to sector marker

                    // int t4 = buf[bufPtr];
                    switch (buf[bufPtr])
                        {
                        case 1:
                        case 3:
                        case 5:
                        case 7:
                            bufPtr += sectorSize + 1;
                            break;
                        case 2:
                        case 4:
                        case 6:
                        case 8:
                            bufPtr += 2;
                            break;
                        case 0:
                            bufPtr++;
                            break;
                        default:
                            MessageBox.Show("Error - IMD sector marker out of scope", "Error",
                                MessageBoxButtons.OK);
                            break;
                        }

                    if (((sectorCnt + 1) % spt) == 0 && sectorCnt > 0) bufPtr += 5 + spt;     // skip track header and interleave info
                    sectorCnt++;
                    }
                //

                var diskType = (int)buf[diskMap[0] + 6];


                for (ctr = 0; ctr < DiskType.GetLength(0); ctr++) // search DiskType array for values
                    if (diskType == DiskType[ctr, 0])
                    {
                        albSize = DiskType[ctr, 1]; // ALB Size
                        albNumSize = DiskType[ctr, 3]; // size of ALB size in directory
                        dirStart = DiskType[ctr, 2]; // physical start of directory
                        dirSizeD = DiskType[ctr, 4]; // size of the directory
                        sptD = DiskType[ctr, 6]; // sectors per track  
                        sectorSizeD = DiskType[ctr, 7]; // sector size
                        numTrack = DiskType[ctr, 8];
                        diskSize = diskTotal = numTrack * spt * sectorSize / 1024;
                        dirSectStart = dirStart / sectorSize;
                        break;
                    }

                // error if no match found
                if (ctr == DiskType.GetLength(0))
                    MessageBox.Show("Error - CP/M Disk Type not found in IMD File", "Error", MessageBoxButtons.OK);
                else
                    result = 1;


                if ((spt != sptD || sectorSize != sectorSizeD) && result == 1)
                {
                    MessageBox.Show("Error - sector/track or sector size mismatch", "Error", MessageBoxButtons.OK);
                    result = 0;
                }

                if (result == 1) // done error checking, read directory
                    {
                    // Read Dir
                    var diskUsed = 0;
                    for (var i = 0; i < dirSizeD / sectorSize; i++)
                        {
                        bufPtr = diskMap[(int)(dirStart / sectorSize) + i];
                        if (buf[bufPtr++] % 2 > 0)            // IMD sector marker is odd. data should contain sector size
                            {
                            for (var dirPtr = 0; dirPtr < sectorSize; dirPtr += 32)
                                {
                                if (buf[bufPtr + dirPtr] != 0xe5)
                                    {
                                    var flagStr = "";
                                    if ((buf[bufPtr + dirPtr + 9] & 0x80) > 0) flagStr += "R/O";
                                    if ((buf[bufPtr + dirPtr + 10] & 0x80) > 0) flagStr += " S";
                                    if ((buf[bufPtr + dirPtr + 11] & 0x80) > 0) flagStr += " W";
                                    for (int k = 9; k < 12; k++)
                                        buf[bufPtr + dirPtr + k] &= 0x7f;         // mask high bit for string conversion

                                    string fnameStr = encoding.GetString(buf, bufPtr + dirPtr + 1, 11);
                                    //fnameStr = fnameStr.Insert(8, " ");
                                    int fileDirSize = buf[bufPtr + dirPtr + 15] * 128;
                                    var temp = new DirList(fnameStr, fileDirSize, flagStr);            // temp storage
                                    Array.Copy(buf, bufPtr + dirPtr + 1, temp.fnameB, 0, 11); // copy byte filename
                                    diskUsed += fileDirSize;
                                    temp.fcbNumSize = albNumSize;
                                    var tempFcb = new FCBlist
                                        {
                                        extantnum = buf[bufPtr + dirPtr + 12],
                                        fcbnum = buf[bufPtr + dirPtr + 15]
                                        };
                                    for (var k = 16; k < 32; k++) tempFcb.fcb[k - 16] = (int)buf[bufPtr + dirPtr + 16 + ((k - 16) * albNumSize)];
                                    temp.fcbList.Add(tempFcb);
                                    var obj = fileNameList.FirstOrDefault(x => x.fname == fnameStr);
                                    if (obj != null)            // directory entry exists
                                        {
                                        obj.fsize += fileDirSize;           // update file size
                                        obj.fcbList.Add(tempFcb);           // add file control block
                                        }
                                    else
                                        {
                                        fileNameList.Add(temp);
                                        }
                                    }
                                }
                                                        }
                        }
                    fileNameList.Sort();
                    //debug
                    //foreach (var f in fileNameList)
                    //{
                    //    var testStr = f.fname + " ";
                    //    foreach (var t in f.fcbList)
                    //    {
                    //        for (var i = 0; i < 16/f.fcbNumSize; i++)
                    //            testStr = testStr + t.fcb[i].ToString() + " ";
                    //    }

                    //    Console.WriteLine(testStr);
                    //}


                    }

                }

            if (result == 0)            // clear instance data
            {
                diskSize = 0;
                DiskImageImdActive = "";
                fileNameList.Clear();
            }
            return fileNameList;
            }
        //***************** Extract File CP/M IMD
        // Call ReadImdDisk to make sure image is in memory
        // Check to make sure file is in DirList
        public int ExtractFileCPMImd(Form1.DiskFileEntry diskFileEntry)
            {
            var diskImage = diskFileEntry.DiskImageName;
            //var fileNameListtemp = new List<CPMFile.DirList>();
            long diskUsed = 0, diskTotal = 0;
            int result =0;



            var fileNameListTemp = ReadImdDir(diskImage, ref diskTotal);
            var obj = fileNameList.FirstOrDefault(x => x.fname == diskFileEntry.FileName);
            if (obj != null)
            {
                var encoding = new UTF8Encoding();
                var dir = string.Format("{0}_Files", diskImage); // create directory name and check if directory exists
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                fnameb = encoding.GetBytes(diskFileEntry.FileName);

                // Create output File
                var name = diskFileEntry.FileName.Substring(0, 8).Trim(' ');
                var ext = diskFileEntry.FileName.Substring(8, 3).Trim(' ');
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
                        file_name = file_name + "1";
                    }

                var file_out = File.Create(file_name);
                var bin_out = new BinaryWriter(file_out);


                // Read file data from memory buffer
                byte[] wBuff = new byte[fileNameList[0].fsize*1024+256];          // write buffer = file size plus a buffer
                int wBPtr = 0;
                var t0 = 0;
                foreach (var f in obj.fcbList)
                {
                    t0++;
                    var fcbNum = f.fcbnum; // number of 128 byte CP/M records in the FCB
                    for (var i = 0; i < 16 / albNumSize && fcbNum > 0; i++) // read each fcb block in record. may be 8 or 16 values
                    {

                       if (f.fcb[i] > 0) // only process valid allocation blocks
                        {
                            var sectPerAlb = albSize / sectorSize;
                            //var t2 = f.fcb[i] * sectPerAlb + dirSectStart; // debug
                            for (var albCnt = 0;albCnt < sectPerAlb;albCnt++) // number of sectors to read in this allocation block
                            {
                                //var t3 = f.fcb[i] * sectPerAlb + dirSectStart + albCnt;
                                var bufPtr = diskMap[f.fcb[i] * sectPerAlb + dirSectStart + albCnt]; // location of sector in buf[]
                                var bufData = buf[bufPtr]; // get IMD sector marker. If odd, a sector worth of data follows

                                if (bufData % 2 > 0) // IMD sector marker. odd number equals sector worth of data
                                {
                                    bufPtr++; // point to first data byte
                                    int k = 0;                                      // declared outside for loop to preserve value
                                    for (;k < sectorSize / 128 && k < fcbNum;k++) // read only one sector or the number of fcb records left
                                        for (var j = 0; j < 128; j++)
                                            wBuff[wBPtr++] = buf[bufPtr++];
                                    fcbNum -= k; // decrement fcbnum counter by number of records read
                                }
                                else
                                    // IMD marker even, sector is compressed. next byte equals sector data
                                {
                                    bufPtr++;
                                    int k = 0;

                                    for (; k < sectorSize / 128 && k < fcbNum; k++)
                                        for (var j = 0; j < 128; j++)
                                            wBuff[wBPtr++] = buf[bufPtr];
                                    fcbNum -= k; // decrement fcbnum counter by number of records read

                                }

                            }
                        }
                    }
                }
                wBPtr--;
                bin_out.Write(wBuff, 0, wBPtr);
                bin_out.Close();
                file_out.Close();
                result = 1;

            }
            else
                MessageBox.Show(diskFileEntry.FileName + " error. File not found in DirList", "Error",MessageBoxButtons.OK);

            return result;
            }
        //************ Add  CP/M File ****************
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
                fileCnt = 0,
                albNum = 2;             // first available allocation block
            byte[] cpmBuff = new byte[maxDiskLen];
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
                        bin_file.Read(filebuf, 0, (int)len);
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
                        writer.Write(cpmBuff, 0, (int)maxDiskLen);
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
                dirSize = 0x800,
                interleave = 4,
                spt = 10,
                sectorSize = 0x100; // disk parameter values
            int albNumt = albNum;       // save in case there is an issue
            long diski = dirStart,   // CP/M disk index
                filei = 0;          // file buffer index
            byte extent = 0,
                extentNum = 0;
            int[] extList = new int[dirSize / 32];
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
            BuildSkew(ref skewMap, interleave, spt);
            var encoding = new ASCIIEncoding();
            filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
            filename8 = filename8.PadRight(8, ' ');
            string ext3 = Path.GetExtension(filename);
            _ = string.IsNullOrEmpty(ext3) ? ext3 = "   " : ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));

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
                    while (extListi >= 0) if (extList[extListi] > 0) cpmBuff[extList[extListi--]] = 0xe5;
                    albNum = albNumt;
                    result = 0;
                    break;
                    }
                // write upto 0x80 1128 byte CP/M records
                extList[extListi++] = (int)diski;          // list of disk entries to erase in case of failure
                cpmBuff[diski] = 0;
                char[] fn = filenameb.ToCharArray();
                for (var i = 1; i < 12; i++) cpmBuff[diski + i] = (byte)fn[i - 1];           // copy file name to dir entry
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
                var basePtr = albNum * allocBlock + dirStart - (albNum * allocBlock) % minBlock;
                while (filei < len && albCnt < 16)             // write up to 16 allocation blocks for this directory entry
                    {
                    int t1 = (albNum % (minBlock / allocBlock));
                    var startSector = (albNum % (minBlock / allocBlock)) * sectorBlock;
                    for (var i = 0; i < sectorBlock; i++)
                        {
                        var diskPtr = 0;
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

                    cpmBuff[diski + 16 + albCnt++] = (byte)albNum++;
                    basePtr = albNum * allocBlock + dirStart - (albNum * allocBlock) % minBlock;
                    }

                cpmBuff[diski + 12] = extentNum++;
                var t2 = (byte)(albCnt * allocBlock / 128);
                cpmBuff[diski + 15] = (byte)(albCnt * allocBlock / 128);


                }

            return result;
            }
        // ************** Extract File CP/M  *********************
        // inputs: path and filename, disk entry structure
        // output: requested file
        public int ExtractFileCPM(Form1.DiskFileEntry disk_file_entry)
            {
            var disk_image_file = disk_file_entry.DiskImageName;
            var result = 1; // assume success
            int temp;
            var diskType = 0;
            var ctr = 0; // for loop counter
            var maxBuffSize = 0x800;            // largest allocation block size
            int allocBlock = 0,
                dirStart = 0,
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

            // debug
            // var cpmFile = new CPMFile();
            //var temp1 = FCBfirst.Count;

            var encoding = new UTF8Encoding();
            var dir = string.Format("{0}_Files",
                disk_image_file); // create directory name and check if directory exists
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            fnameb = encoding.GetBytes(disk_file_entry.FileName);

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
                    file_name = file_name + "1";
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
                    break;

            // use default if no match found
            if (ctr == DiskType.GetLength(0))
                ctr = DiskType.GetLength(0) - 1;        // default H17 format
            allocBlock = DiskType[ctr, 1];      // ALB Size
            dirStart = DiskType[ctr, 2];        // physical start of directory
            dirSize = DiskType[ctr, 4];         // size of the directory
            interleave = DiskType[ctr, 5];      // disk interleave
            spt = DiskType[ctr, 6];             // sectors per track  
            sectorSize = DiskType[ctr, 7];      // sector size           //else
                                                // read the directory
            BuildSkew(ref skewMap, interleave, spt);
            // read directory sectors and place in sequential order in buff
            for (var i = 0; i < dirSize / allocBlock; i++)
                GetALB(ref buff, allocBlock * i, bin_file, i, dirStart, allocBlock, sectorSize, spt, skewMap);
            BuildDir(ref buff, dirSize, ref FCBfirst, fnameb);             // build FCB list for filename
            foreach (var f in FCBfirst)
                {
                var fcbNum = f.fcbnum;
                var cpmAlb = allocBlock / 128;              // number of 128 byte CP/M records in the allocation block
                var rBptr = 0;
                var wBptr = 0;
                for (var i = 0; i < 16; i++)
                    if (f.fcb[i] > 0)
                        {
                        rBptr = wBptr = 0;
                        GetALB(ref buff, 0, bin_file, f.fcb[i], dirStart, allocBlock, sectorSize, spt, skewMap);
                        for (var j = 0; j < cpmAlb; j++)
                            {
                            if (fcbNum > 0)
                                for (var l = 0; l < 128; l++) wBuff[wBptr++] = buff[rBptr++];
                            fcbNum--;
                            }
                        bin_out.Write(wBuff, 0, wBptr);
                        }
                }
            bin_out.Close();
            file_out.Close();

            bin_file.Close();
            file.Close();
            return result;
            }

        // ******************* read CP/M directory from buffer for given file name
        private void BuildDir(ref byte[] buff, int dirSize, ref List<FCBlist> fcbList, byte[] fNameB)
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
                        fcbList.Add(temp);
                        }
                    }

                bPtr += 32;
                }
            FCBfirst.Sort();
            // debug code to check FCBs in list
            //var testStr = "";
            //foreach (var f in FCBfirst)
            //    for (var i = 0; i < 16; i++)
            //        testStr = testStr + f.fcb[i].ToString() + " ";
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
                var errStr = "Error -  buffer read error, " + tx + " bytes read, " + minBlock + " bytes requested";
                if (tx != minBlock)            // read in enough data to handle skew conversion
                    MessageBox.Show(errStr, "Error", MessageBoxButtons.OK);
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

            while (logicalS < size)         // build physical to logical skew table
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
                    while (t < size && skew[t, 0] != logicalS) t++;         // find where logical sector is in the physical record
                    temp[logicalS, 0] = logicalS;
                    temp[logicalS, 1] = skew[t, 1];
                    }

                logicalS++;
                }

            for (t = 0; t < size; t++)              // copy logical to physical mapping to skew table
                {
                skew[t, 0] = temp[t, 0];
                skew[t, 1] = temp[t, 1];
                }
            }
        }
    }