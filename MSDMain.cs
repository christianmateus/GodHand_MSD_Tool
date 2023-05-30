using GodHand_MSD_Tool.Enum;
using System;
using System.IO;
using System.Linq;

namespace GodHand_MSD_Tool
{
    public class MSDMain
    {
        public string filepath { get; set; }

        MSD msd = new MSD();
        public MSDMain(string filename, string command)
        {
            filepath = filename;
            if (command == "extract")
            {
                Extract();
            }
            else
            {
                Repack();
            }
        }

        public void Extract()
        {
            StreamWriter txt = new StreamWriter(Path.GetFileNameWithoutExtension(filepath) + ".txt");

            BinaryReader br = new BinaryReader(File.OpenRead(filepath));
            msd.MessageCount = br.ReadUInt32();
            msd.Offsets = new ulong[msd.MessageCount];

            for (int i = 0; i < msd.MessageCount; i++)
            {
                msd.Offsets[i] = br.ReadUInt64();
            }

            // Get all chars from message
            for (int i = 0; i < msd.Offsets.Length; i++)
            {
                msd.MessageChar = 0;
                while (msd.MessageChar != msd.EndTag)
                {
                    msd.MessageChar = br.ReadUInt16();

                    for (var item = 0; item < msd.CharCode.Values.Count; item++)
                    {
                        if (msd.CharCode.Values.ToArray()[item] == msd.MessageChar)
                        {
                            txt.Write(msd.CharCode.Keys.ToArray()[item]);
                            Console.Write(msd.CharCode.Keys.ToArray()[item]);
                            break;
                        }
                        if (item == msd.CharCode.Values.Count - 1)
                        {
                            if (msd.MessageChar < 256)
                            {
                                txt.Write("{00" + msd.MessageChar.ToString("X") + "}");
                            }
                            else
                            {
                                txt.Write("{" + msd.MessageChar.ToString("X") + "}");
                            }
                        }
                    }

                    if (msd.MessageChar == msd.EndTag)
                    {
                        // Check if there's padding {00 00} after END tag
                        try
                        {
                            if (br.ReadUInt16() == 0)
                            {
                                txt.Write("{0000}");
                            }
                            else
                            {
                                br.BaseStream.Position -= 2;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                txt.WriteLine("");
                Console.WriteLine("");
            }
            br.Close();
            txt.Close();
        }

        public void Repack()
        {
            int lineCount = GetLineCount();
            int messageLength = 0;
            msd.Offsets = new ulong[lineCount];

            StreamReader txt = new StreamReader(filepath);
            BinaryWriter bw = new BinaryWriter(File.Create(Path.GetFileNameWithoutExtension(filepath) + ".msd"));
            bw.Write(lineCount);

            // Create offsets area
            for (int i = 0; i < lineCount; i++)
            {
                bw.Write((long)0x00);
            }

            // Iterate through each message
            for (int msg = 0; msg < lineCount; msg++)
            {
                string line = txt.ReadLine();
                msd.Offsets[msg] = (ulong)(messageLength + 0x04 + (0x08 * lineCount));

                // Read every char
                for (int i = 0; i < line.Length; i++)
                {
                    // Verify if character is a identifier {xxxx}
                    if (line[i].ToString() == "{")
                    {
                        string codeValue = line.Substring(i + 1, 4);
                        byte[] lowByte = BitConverter.GetBytes(Convert.ToInt16(codeValue.Substring(0, 2), 16));
                        byte[] highByte = BitConverter.GetBytes(Convert.ToInt16(codeValue.Substring(2), 16));
                        bw.Write(highByte[0]);
                        bw.Write(lowByte[0]);
                        i += 5; // Jumps over the code identifier {xxxx}
                        messageLength += 2;

                        // Check if there's padding {00 00} after END tag
                        try
                        {
                            if (line[i] == 0 && line[i + 1] == 0)
                            {
                                bw.Write((short)0x00);
                                messageLength += 2;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                        // Read evey key in the dictionary and compare to the character in the string
                        for (var key = 0; key < msd.CharCode.Keys.Count; key++)
                        {
                            if (msd.CharCode.Keys.ToArray()[key] == line[i].ToString())
                            {
                                string value = msd.CharCode.Values.ToArray()[key].ToString();
                                bw.Write(Convert.ToInt16(value));
                                messageLength += 2;
                                break;
                            }
                        }
                    }
                }
            }

            for (var m = 0; m < 8; m++)
            {
                if (bw.BaseStream.Position % 16 != 0)
                {

                    bw.Write((short)0x00);
                }
            }
            txt.Close();
            bw.Close();
            UpdateOffsets();
        }

        private void UpdateOffsets()
        {
            BinaryWriter bw = new BinaryWriter(File.OpenWrite(Path.GetFileNameWithoutExtension(filepath) + ".msd"));
            bw.BaseStream.Position = 0x04;

            for (int i = 0; i < msd.Offsets.Length; i++)
            {
                bw.Write(msd.Offsets[i]);
            }
            bw.Close();
        }

        public int GetLineCount()
        {
            return File.ReadLines(filepath).Count();
        }

    }
}
