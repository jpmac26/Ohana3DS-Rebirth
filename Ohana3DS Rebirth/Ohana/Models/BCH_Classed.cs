using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

using Ohana3DS_Rebirth.Ohana.Models.PICA200;

namespace Ohana3DS_Rebirth.Ohana.Models
{
    class BCH_Classed
    {
        public hdr header = new hdr();
        public bchContentHeader contentHeader = new bchContentHeader();
        public class hdr
        {
            public string magic;
            public byte backwardCompatibility;
            public byte forwardCompatibility;
            public ushort version;

            public uint mainHeaderOffset;
            public uint stringTableOffset;
            public uint gpuCommandsOffset;
            public uint dataOffset;
            public uint dataExtendedOffset;
            public uint relocationTableOffset;

            public uint mainHeaderLength;
            public uint stringTableLength;
            public uint gpuCommandsLength;
            public uint dataLength;
            public uint dataExtendedLength;
            public uint relocationTableLength;
            public uint uninitializedDataSectionLength;
            public uint uninitializedDescriptionSectionLength;

            public ushort flags;
            public ushort addressCount;
            public void Read(MemoryStream data)
            {
                BinaryReader input = new BinaryReader(data);
                magic = IOUtils.readString(input, 0);
                data.Seek(4, SeekOrigin.Current);
                backwardCompatibility = input.ReadByte();
                forwardCompatibility = input.ReadByte();
                version = input.ReadUInt16();

                mainHeaderOffset = input.ReadUInt32();
                stringTableOffset = input.ReadUInt32();
                gpuCommandsOffset = input.ReadUInt32();
                dataOffset = input.ReadUInt32();
                if (backwardCompatibility > 0x20) dataExtendedOffset = input.ReadUInt32();
                relocationTableOffset = input.ReadUInt32();

                mainHeaderLength = input.ReadUInt32();
                stringTableLength = input.ReadUInt32();
                gpuCommandsLength = input.ReadUInt32();
                dataLength = input.ReadUInt32();
                if (backwardCompatibility > 0x20) dataExtendedLength = input.ReadUInt32();
                relocationTableLength = input.ReadUInt32();

                uninitializedDataSectionLength = input.ReadUInt32();
                uninitializedDescriptionSectionLength = input.ReadUInt32();

                if (backwardCompatibility > 7)
                {
                    flags = input.ReadUInt16();
                    addressCount = input.ReadUInt16();
                }

            }
        }
        public void offsetfix(MemoryStream data)
        {
            BinaryReader input = new BinaryReader(data);
            BinaryWriter writer = new BinaryWriter(data);
            for (uint o = header.relocationTableOffset; o < header.relocationTableOffset + header.relocationTableLength; o += 4)
            {
                data.Seek(o, SeekOrigin.Begin);
                uint value = input.ReadUInt32();
                uint offset = value & 0x1ffffff;
                byte flags = (byte)(value >> 25);

                switch (flags)
                {
                    case 0:
                        data.Seek((offset * 4) + header.mainHeaderOffset, SeekOrigin.Begin);
                        writer.Write(peek(input) + header.mainHeaderOffset);
                        break;

                    case 1:
                        data.Seek(offset + header.mainHeaderOffset, SeekOrigin.Begin);
                        writer.Write(peek(input) + header.stringTableOffset);
                        break;

                    case 2:
                        data.Seek((offset * 4) + header.mainHeaderOffset, SeekOrigin.Begin);
                        writer.Write(peek(input) + header.gpuCommandsOffset);
                        break;

                    case 7:
                    case 0xc:
                        data.Seek((offset * 4) + header.mainHeaderOffset, SeekOrigin.Begin);
                        writer.Write(peek(input) + header.dataOffset);
                        break;
                }

                //The moron that designed the format used different flags on different versions, instead of keeping compatibility.
                data.Seek((offset * 4) + header.gpuCommandsOffset, SeekOrigin.Begin);
                if (header.backwardCompatibility < 6)
                {
                    switch (flags)
                    {
                        case 0x23: writer.Write(peek(input) + header.dataOffset); break; //Texture
                        case 0x25: writer.Write(peek(input) + header.dataOffset); break; //Vertex
                        case 0x26: writer.Write(((peek(input) + header.dataOffset) & 0x7fffffff) | 0x80000000); break; //Index 16 bits mode
                        case 0x27: writer.Write((peek(input) + header.dataOffset) & 0x7fffffff); break; //Index 8 bits mode
                    }
                }
                else if (header.backwardCompatibility < 8)
                {
                    switch (flags)
                    {
                        case 0x24: writer.Write(peek(input) + header.dataOffset); break; //Texture
                        case 0x26: writer.Write(peek(input) + header.dataOffset); break; //Vertex
                        case 0x27: writer.Write(((peek(input) + header.dataOffset) & 0x7fffffff) | 0x80000000); break; //Index 16 bits mode
                        case 0x28: writer.Write((peek(input) + header.dataOffset) & 0x7fffffff); break; //Index 8 bits mode
                    }
                }
                else if (header.backwardCompatibility < 0x21)
                {
                    switch (flags)
                    {
                        case 0x25: writer.Write(peek(input) + header.dataOffset); break; //Texture
                        case 0x27: writer.Write(peek(input) + header.dataOffset); break; //Vertex
                        case 0x28: writer.Write(((peek(input) + header.dataOffset) & 0x7fffffff) | 0x80000000); break; //Index 16 bits mode
                        case 0x29: writer.Write((peek(input) + header.dataOffset) & 0x7fffffff); break; //Index 8 bits mode
                    }
                }
                else
                {
                    switch (flags)
                    {
                        case 0x25: writer.Write(peek(input) + header.dataOffset); break; //Texture
                        case 0x26: writer.Write(peek(input) + header.dataOffset); break; //Vertex relative to Data Offset
                        case 0x27: writer.Write(((peek(input) + header.dataOffset) & 0x7fffffff) | 0x80000000); break; //Index 16 bits mode relative to Data Offset
                        case 0x28: writer.Write((peek(input) + header.dataOffset) & 0x7fffffff); break; //Index 8 bits mode relative to Data Offset
                        case 0x2b: writer.Write(peek(input) + header.dataExtendedOffset); break; //Vertex relative to Data Extended Offset
                        case 0x2c: writer.Write(((peek(input) + header.dataExtendedOffset) & 0x7fffffff) | 0x80000000); break; //Index 16 bits mode relative to Data Extended Offset
                        case 0x2d: writer.Write((peek(input) + header.dataExtendedOffset) & 0x7fffffff); break; //Index 8 bits mode relative to Data Extended Offset
                    }
                }
            }
        }
        public class bchContentHeader
        {
            public uint modelsPointerTableOffset;
            public uint modelsPointerTableEntries;
            public uint modelsNameOffset;
            public uint materialsPointerTableOffset;
            public uint materialsPointerTableEntries;
            public uint materialsNameOffset;
            public uint shadersPointerTableOffset;
            public uint shadersPointerTableEntries;
            public uint shadersNameOffset;
            public uint texturesPointerTableOffset;
            public uint texturesPointerTableEntries;
            public uint texturesNameOffset;
            public uint materialsLUTPointerTableOffset;
            public uint materialsLUTPointerTableEntries;
            public uint materialsLUTNameOffset;
            public uint lightsPointerTableOffset;
            public uint lightsPointerTableEntries;
            public uint lightsNameOffset;
            public uint camerasPointerTableOffset;
            public uint camerasPointerTableEntries;
            public uint camerasNameOffset;
            public uint fogsPointerTableOffset;
            public uint fogsPointerTableEntries;
            public uint fogsNameOffset;
            public uint skeletalAnimationsPointerTableOffset;
            public uint skeletalAnimationsPointerTableEntries;
            public uint skeletalAnimationsNameOffset;
            public uint materialAnimationsPointerTableOffset;
            public uint materialAnimationsPointerTableEntries;
            public uint materialAnimationsNameOffset;
            public uint visibilityAnimationsPointerTableOffset;
            public uint visibilityAnimationsPointerTableEntries;
            public uint visibilityAnimationsNameOffset;
            public uint lightAnimationsPointerTableOffset;
            public uint lightAnimationsPointerTableEntries;
            public uint lightAnimationsNameOffset;
            public uint cameraAnimationsPointerTableOffset;
            public uint cameraAnimationsPointerTableEntries;
            public uint cameraAnimationsNameOffset;
            public uint fogAnimationsPointerTableOffset;
            public uint fogAnimationsPointerTableEntries;
            public uint fogAnimationsNameOffset;
            public uint scenePointerTableOffset;
            public uint scenePointerTableEntries;
            public uint sceneNameOffset;
            public void Read(MemoryStream data)
            {
                BinaryReader input = new BinaryReader(data);
                modelsPointerTableOffset = input.ReadUInt32();
                modelsPointerTableEntries = input.ReadUInt32();
                modelsNameOffset = input.ReadUInt32();
                materialsPointerTableOffset = input.ReadUInt32();
                materialsPointerTableEntries = input.ReadUInt32();
                materialsNameOffset = input.ReadUInt32();
                shadersPointerTableOffset = input.ReadUInt32();
                shadersPointerTableEntries = input.ReadUInt32();
                shadersNameOffset = input.ReadUInt32();
                texturesPointerTableOffset = input.ReadUInt32();
                texturesPointerTableEntries = input.ReadUInt32();
                texturesNameOffset = input.ReadUInt32();
                materialsLUTPointerTableOffset = input.ReadUInt32();
                materialsLUTPointerTableEntries = input.ReadUInt32();
                materialsLUTNameOffset = input.ReadUInt32();
                lightsPointerTableOffset = input.ReadUInt32();
                lightsPointerTableEntries = input.ReadUInt32();
                lightsNameOffset = input.ReadUInt32();
                camerasPointerTableOffset = input.ReadUInt32();
                camerasPointerTableEntries = input.ReadUInt32();
                camerasNameOffset = input.ReadUInt32();
                fogsPointerTableOffset = input.ReadUInt32();
                fogsPointerTableEntries = input.ReadUInt32();
                fogsNameOffset = input.ReadUInt32();
                skeletalAnimationsPointerTableOffset = input.ReadUInt32();
                skeletalAnimationsPointerTableEntries = input.ReadUInt32();
                skeletalAnimationsNameOffset = input.ReadUInt32();
                materialAnimationsPointerTableOffset = input.ReadUInt32();
                materialAnimationsPointerTableEntries = input.ReadUInt32();
                materialAnimationsNameOffset = input.ReadUInt32();
                visibilityAnimationsPointerTableOffset = input.ReadUInt32();
                visibilityAnimationsPointerTableEntries = input.ReadUInt32();
                visibilityAnimationsNameOffset = input.ReadUInt32();
                lightAnimationsPointerTableOffset = input.ReadUInt32();
                lightAnimationsPointerTableEntries = input.ReadUInt32();
                lightAnimationsNameOffset = input.ReadUInt32();
                cameraAnimationsPointerTableOffset = input.ReadUInt32();
                cameraAnimationsPointerTableEntries = input.ReadUInt32();
                cameraAnimationsNameOffset = input.ReadUInt32();
                fogAnimationsPointerTableOffset = input.ReadUInt32();
                fogAnimationsPointerTableEntries = input.ReadUInt32();
                fogAnimationsNameOffset = input.ReadUInt32();
                scenePointerTableOffset = input.ReadUInt32();
                scenePointerTableEntries = input.ReadUInt32();
                sceneNameOffset = input.ReadUInt32();
            }
           }



        /// <summary>
        ///     Reads a UInt without advancing the position on the Stream.
        /// </summary>
        /// <param name="input">The BinaryReader of the stream</param>
        /// <returns></returns>
        private static uint peek(BinaryReader input)
        {
            uint value = input.ReadUInt32();
            input.BaseStream.Seek(-4, SeekOrigin.Current);
            return value;
        }
    }
}
