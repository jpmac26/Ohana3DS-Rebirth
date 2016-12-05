using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Ohana3DS_Rebirth.Ohana.Animations
{
    class AnimBin
    {
        public class Entry
        {
            public string name;
            public string modelName;
            public int unk;
            //public CTPK texture; <----- add the texture variable here 

            public Entry(BinaryReader f)
            {
                read(f);
            }

            public void read(BinaryReader f)
            {
                name = new string(f.ReadChars(0x24));
                uint offset = f.ReadUInt32();
                uint textureOffset = f.ReadUInt32();
                unk = f.ReadInt32();
                for (int i = 0; i < 3; i++)
                    if (f.ReadInt32() != 0)
                        Console.WriteLine("Found unk" + i + " at offset "+f.BaseStream.Position);

                long returnOffset = f.BaseStream.Position;
                f.BaseStream.Seek(offset, SeekOrigin.Begin);
                long nameTableOffset = f.BaseStream.Position + f.ReadUInt32();
                uint nameCount = f.ReadUInt32();
                modelName = new string(f.ReadChars(0x24));


                f.BaseStream.Seek(textureOffset, SeekOrigin.Begin);
                //texture = new CTPK(f.BaseStream);  <-------------- make a new CTPK and pass it the Steam to read in the CTPK from  

                f.BaseStream.Seek(returnOffset,SeekOrigin.Begin);
            }
        }

        public AnimBin(Stream stream)
        {
            read(stream);
            Console.Write(stream.Position);
        }

        List<Entry> entries = new List<Entry>();

        public void read(Stream stream)
        {
            BinaryReader f = new BinaryReader(stream);

            uint unk = f.ReadUInt32();
            ushort entryCount = f.ReadUInt16();
            ushort unk2 = f.ReadUInt16();
            uint unk3 = f.ReadUInt32();
            for (int i = 0; i < entryCount; i++)
                entries.Add(new Entry(f));
        }
    }
}
