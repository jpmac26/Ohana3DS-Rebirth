using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Ohana3DS_Rebirth.GUI;
using Ohana3DS_Rebirth.Ohana;
using Ohana3DS_Rebirth.Ohana.Models.PICA200;

namespace Ohana3DS_Rebirth.Tools
{

    public partial class OBCHTextureReplacer : OForm
    {
        string currentFile;
        FrmMain parentForm;
        int mipSel = 0;

        private struct loadedTexture
        {
            public bool modified;
            public uint gpuCommandsOffset;
            public uint gpuCommandsWordCount;
            public uint offset;
            public int length;
            public RenderBase.OTexture texture;
            public RenderBase.OTextureFormat type;
        }


        private class loadedBCH
        {
            public bool isBCH;
            public uint mainHeaderOffset;
            public uint gpuCommandsOffset;
            public uint dataOffset;
            public uint relocationTableOffset;
            public uint relocationTableLength;
            public List<MIPlayer> mips;

            public loadedBCH()
            {
                mips = new List<MIPlayer>();
            }
        }
        private class MIPlayer
        {
            public List<loadedTexture> textures;
            public MIPlayer()
            {
                textures = new List<loadedTexture>();
            }
        }

        loadedBCH bch;

        public OBCHTextureReplacer(FrmMain parent)
        {
            InitializeComponent();
            parentForm = parent;
            TopMenu.Renderer = new OMenuStrip();
            TextureTypeDrop.Items.AddRange(new string[] { "rgba8", "rgb8", "rgba5551", "rgb565", "rgba4", "la8", "hilo8", "l8", "a8", "la4", "l4", "a4", "etc1", "etc1a4" });
            MipSelect.Items.AddRange(new string[] { "1", "2", "3", "4" });
        }

        private void OBCHTextureReplacer_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.O: open(); break;
                case Keys.S: if (bch != null) save(); break;
                case Keys.P: saveAndPreview(); break;
            }
        }

        private void MenuOpen_Click(object sender, EventArgs e)
        {
            open();
        }

        private void MenuSave_Click(object sender, EventArgs e)
        {
            if (bch != null) save();
        }

        private void MenuSaveAndPreview_Click(object sender, EventArgs e)
        {
            saveAndPreview();
        }

        private void MenuExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void open()
        {
            using (OpenFileDialog openDlg = new OpenFileDialog())
            {
                openDlg.Filter = "All supported files|*.bch;*.ctpk;*.pc;*.cm|All|*.*";

                if (openDlg.ShowDialog() == DialogResult.OK && File.Exists(openDlg.FileName))
                {
                    if (!open(openDlg.FileName))
                        MessageBox.Show(
                            "Unsupported file format!",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                }
            }
        }

        private void saveAndPreview()
        {
            if (bch != null)
            {
                save();
                parentForm.open(currentFile);
            }
        }
        private static uint peek(BinaryReader input)
        {
            uint value = input.ReadUInt32();
            input.BaseStream.Seek(-4, SeekOrigin.Current);
            return value;
        }
        private bool open(string fileName)
        {
            using (FileStream data = new FileStream(fileName, FileMode.Open))
            {
                BinaryReader input = new BinaryReader(data);

                if (peek(input) == 0x00010000)
                {
                    currentFile = fileName;
                    bch = new loadedBCH();
                    bch.isBCH = false;
                    bch.mips.Add(new MIPlayer());
                    packPNK(data,input,bch);
                }
                if (peek(input) == 0x15041213)
                {
                    currentFile = fileName;
                    bch = new loadedBCH();
                    bch.isBCH = false;
                    bch.mips.Add(new MIPlayer());
                    bch.mips[0].textures.Add(loadPKM(data, input));
                }
                string magic2b = getMagic(input, 2);
                if(magic2b == "PC" || magic2b == "CM")
                {
                    bch = new loadedBCH();
                    bch.isBCH = false;
                    currentFile = fileName;
                    data.Seek(2, SeekOrigin.Current);
                    ushort numEntrys = input.ReadUInt16();
                    for(int i = 0;i < (int)numEntrys; i++)
                    {
                        data.Seek(4 + (i * 4), SeekOrigin.Begin);
                        uint offset = input.ReadUInt32();
                        uint end = input.ReadUInt32();
                        uint lenth = end - offset;
                        long rtn = data.Position;
                        data.Seek(offset, SeekOrigin.Begin);
                        if (magic2b == "CM" & i == 0)
                        {
                            packPNK(data, input, bch);
                        }
                        if (lenth > 4) {
                            if(magic2b == "PC") { 
                                if (peek(input) == 0x15041213)
                        {
                            bch.mips[0].textures.Add(loadPKM(data, input));
                        }
                            }
                        }
                    }
                }
                string magic = IOUtils.readString(input, 0);
                if (magic == "BCH")
                {

                    currentFile = fileName;
                    data.Seek(4, SeekOrigin.Current);
                    byte backwardCompatibility = input.ReadByte();
                    byte forwardCompatibility = input.ReadByte();
                    ushort version = input.ReadUInt16();

                    uint mainHeaderOffset = input.ReadUInt32();
                    uint stringTableOffset = input.ReadUInt32();
                    uint gpuCommandsOffset = input.ReadUInt32();
                    uint dataOffset = input.ReadUInt32();
                    uint dataExtendedOffset = backwardCompatibility > 0x20 ? input.ReadUInt32() : 0;
                    uint relocationTableOffset = input.ReadUInt32();

                    uint mainHeaderLength = input.ReadUInt32();
                    uint stringTableLength = input.ReadUInt32();
                    uint gpuCommandsLength = input.ReadUInt32();
                    uint dataLength = input.ReadUInt32();
                    uint dataExtendedLength = backwardCompatibility > 0x20 ? input.ReadUInt32() : 0;
                    uint relocationTableLength = input.ReadUInt32();

                    data.Seek(mainHeaderOffset, SeekOrigin.Begin);
                    uint modelsPointerTableOffset = input.ReadUInt32() + mainHeaderOffset;
                    uint modelsPointerTableEntries = input.ReadUInt32();

                    data.Seek(mainHeaderOffset + 0x24, SeekOrigin.Begin);
                    uint texturesPointerTableOffset = input.ReadUInt32() + mainHeaderOffset;
                    uint texturesPointerTableEntries = input.ReadUInt32();

                    bch = new loadedBCH();
                    bch.isBCH = true;
                    for(int i = 0;i<4;i++) bch.mips.Add(new MIPlayer());
                    MipSelect.Enabled = true;

                    //Textures
                    for (int index = 0; index < texturesPointerTableEntries; index++)
                    {
                        data.Seek(texturesPointerTableOffset + (index * 4), SeekOrigin.Begin);
                        data.Seek(input.ReadUInt32() + mainHeaderOffset, SeekOrigin.Begin);

                        loadedTexture tex;
                        tex.modified = false;
                        tex.gpuCommandsOffset = input.ReadUInt32() + gpuCommandsOffset;
                        tex.gpuCommandsWordCount = input.ReadUInt32();
                        data.Seek(0x14, SeekOrigin.Current);
                        uint textureNameOffset = input.ReadUInt32();
                        string textureName = IOUtils.readString(input, textureNameOffset + stringTableOffset);

                        data.Seek(tex.gpuCommandsOffset, SeekOrigin.Begin);
                        PICACommandReader textureCommands = new PICACommandReader(data, tex.gpuCommandsWordCount);

                        tex.offset = textureCommands.getTexUnit0Address() + dataOffset;
                        RenderBase.OTextureFormat fmt = textureCommands.getTexUnit0Format();
                        Size textureSize = textureCommands.getTexUnit0Size();
                        tex.type = fmt;

                        int OGW = textureSize.Width;
                        int OGH = textureSize.Height;
                        for (int i = 0; i < 4; i++) {
                            textureSize.Width = OGW / Convert.ToInt32(Math.Pow(2, i));
                            textureSize.Height = OGH / Convert.ToInt32(Math.Pow(2, i));
                            tex.length = returnSize(fmt, textureSize.Width, textureSize.Height);

                            if(textureSize.Height > 4 & textureSize.Width > 4) { 
                        data.Seek(tex.offset, SeekOrigin.Begin);
                        byte[] buffer = new byte[tex.length];

                        //data.Seek(tex.length + returnSize(fmt, textureSize.Width / 2, textureSize.Height / 2), SeekOrigin.Current);
                        tex.offset = (uint)data.Position;
                        input.Read(buffer, 0, tex.length);
                        Bitmap texture = TextureCodec.decode(
                            buffer,
                            textureSize.Width,
                            textureSize.Height,
                            fmt);

                        tex.texture = new RenderBase.OTexture(texture, textureName);

                                bch.mips[i].textures.Add(tex);
                                tex.offset = (uint)data.Position;

                            }
                        }
                    }

                    bch.mainHeaderOffset = mainHeaderOffset;
                    bch.gpuCommandsOffset = gpuCommandsOffset;
                    bch.dataOffset = dataOffset;
                    bch.relocationTableOffset = relocationTableOffset;
                    bch.relocationTableLength = relocationTableLength;
                }
                else if (magic == "CTPK\u0001")
                {
                    currentFile = fileName;
                    data.Seek(4, SeekOrigin.Current);
                    ushort ver = input.ReadUInt16();
                    ushort numTexture = input.ReadUInt16();
                    uint TextureSectionOffset = input.ReadUInt32();
                    uint TextureSectionSize = input.ReadUInt32();
                    uint HashSectionOffset = input.ReadUInt32();
                    uint TextureInfoSection = input.ReadUInt32();

                    bch = new loadedBCH();
                    bch.isBCH = false;
                    bch.mips.Add(new MIPlayer());
                    for (int i = 0; i < numTexture; i++)
                    {
                        data.Seek(0x20 * (i + 1), SeekOrigin.Begin);
                        loadedTexture tex;
                        tex.modified = false;
                        tex.gpuCommandsOffset = (uint)(0x20 * (i + 1));
                        uint textureNameOffset = input.ReadUInt32();
                        string textureName = IOUtils.readString(input, textureNameOffset);
                        tex.length = input.ReadInt32();
                        tex.offset = input.ReadUInt32() + TextureSectionOffset;
                        tex.type = (RenderBase.OTextureFormat)input.ReadUInt32();
                        ushort Width = input.ReadUInt16();
                        ushort Height = input.ReadUInt16();
                        data.Seek(tex.offset, SeekOrigin.Begin);
                        byte[] buffer = new byte[tex.length];

                        input.Read(buffer, 0, buffer.Length);
                        Bitmap texture = TextureCodec.decode(
                            buffer,
                            Width,
                            Height,
                            tex.type);

                        tex.texture = new RenderBase.OTexture(texture, textureName);

                        tex.gpuCommandsWordCount = 0;

                        bch.mips[0].textures.Add(tex);
                    }
                }
            }

            updateTexturesList();
            return true;
        }
        private void packPNK(FileStream data, BinaryReader input, loadedBCH bch)
        {
            uint veryBase = (uint)data.Position;
            input.ReadUInt32();



            uint[] sectionsCnt = new uint[5];

            for (int i = 0; i < 5; i++)
            {
                sectionsCnt[i] = input.ReadUInt32(); //Count for each section on the file (total 5)
            }
            uint baseAddr = (uint)data.Position;

            for (int sect = 0; sect < 5; sect++)
            {
                uint count = sectionsCnt[sect];

                for (int i = 0; i < count; i++)
                {
                    data.Seek(baseAddr + i * 4, SeekOrigin.Begin);
                    data.Seek(input.ReadUInt32()+veryBase, SeekOrigin.Begin);

                    byte nameStrLen = input.ReadByte();
                    string name = IOUtils.readStringWithLength(input, nameStrLen);
                    uint descAddress = input.ReadUInt32() + veryBase;

                    data.Seek(descAddress, SeekOrigin.Begin);

                    if (sect == 1)
                    {
                        bch.mips[0].textures.Add(loadPKM(data, input));
                    }

                }
                baseAddr += count * 4;
            }
        }
        private int returnSize(RenderBase.OTextureFormat fmt,int w,int h)
        {
            switch (fmt)
            {
                case RenderBase.OTextureFormat.rgba8: return (w * h) * 4;
                case RenderBase.OTextureFormat.rgb8: return (w * h) * 3;
                case RenderBase.OTextureFormat.rgba5551: return (w * h) * 2;
                case RenderBase.OTextureFormat.rgb565: return (w * h) * 2;
                case RenderBase.OTextureFormat.rgba4: return (w * h) * 2;
                case RenderBase.OTextureFormat.la8: return (w * h) * 2;
                case RenderBase.OTextureFormat.hilo8: return (w * h) * 2;
                case RenderBase.OTextureFormat.l8: return (w * h);
                case RenderBase.OTextureFormat.a8: return (w * h);
                case RenderBase.OTextureFormat.la4: return (w * h);
                case RenderBase.OTextureFormat.l4: return (w * h) >> 1;
                case RenderBase.OTextureFormat.a4: return (w * h) >> 1;
                case RenderBase.OTextureFormat.etc1: return (w * h) >> 1;
                case RenderBase.OTextureFormat.etc1a4: return (w * h);
                default: throw new Exception("OBCHTextureReplacer: Invalid texture format on BCH!");
            }
        }
        private loadedTexture loadPKM(FileStream data, BinaryReader input)
        {
            loadedTexture tex;
            tex.modified = false;
            long descAddress2 = data.Position;

            data.Seek(descAddress2 + 0x18, SeekOrigin.Begin);
            int texLength = input.ReadInt32();
            data.Seek(descAddress2 + 0x28, SeekOrigin.Begin);
            string textureName = IOUtils.readStringWithLength(input, 0x40);

            data.Seek(descAddress2 + 0x68, SeekOrigin.Begin);
            ushort width = input.ReadUInt16();
            ushort height = input.ReadUInt16();
            ushort texFormat = input.ReadUInt16();
            ushort texMipMaps = input.ReadUInt16();

            data.Seek(0x10, SeekOrigin.Current);
            tex.offset = (uint)data.Position;
            byte[] texBuffer = input.ReadBytes(texLength);

            RenderBase.OTextureFormat fmt = RenderBase.OTextureFormat.dontCare;

            switch (texFormat)
            {
                case 0x2: fmt = RenderBase.OTextureFormat.rgb565; break;
                case 0x3: fmt = RenderBase.OTextureFormat.rgb8; break;
                case 0x4: fmt = RenderBase.OTextureFormat.rgba8; break;
                case 0x17: fmt = RenderBase.OTextureFormat.rgba5551; break;
                case 0x23: fmt = RenderBase.OTextureFormat.la8; break;
                case 0x24: fmt = RenderBase.OTextureFormat.hilo8; break;
                case 0x25: fmt = RenderBase.OTextureFormat.l8; break;
                case 0x26: fmt = RenderBase.OTextureFormat.a8; break;
                case 0x27: fmt = RenderBase.OTextureFormat.la4; break;
                case 0x28: fmt = RenderBase.OTextureFormat.l4; break;
                case 0x29: fmt = RenderBase.OTextureFormat.a4; break;
                case 0x2a: fmt = RenderBase.OTextureFormat.etc1; break;
                case 0x2b: fmt = RenderBase.OTextureFormat.etc1a4; break;
            }

            Bitmap texture = TextureCodec.decode(texBuffer, width, height, fmt);
            tex.texture = new RenderBase.OTexture(texture, textureName);
            tex.type = fmt;
            tex.gpuCommandsOffset = 0;
            tex.gpuCommandsWordCount = 0;
            tex.length = texLength;
            return tex;
        }
        private void updateTexturesList()
        {
            TextureList.flush();
            PicPreview.Image = null;
            foreach (loadedTexture tex in bch.mips[mipSel].textures) TextureList.addItem(tex.texture.name);
            TextureList.Refresh();
        }

        private void TextureList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            PicPreview.Image = bch.mips[mipSel].textures[TextureList.SelectedIndex].texture.texture;
            TextureTypeDrop.Text = bch.mips[mipSel].textures[TextureList.SelectedIndex].type.ToString();
            /*if(!bch.isBCH)
                TextureTypeDrop.Enabled = true;
            else*/
                TextureTypeDrop.Enabled = false;
        }
        private void TextureTypeDrop_Change(object sender, EventArgs e)
        {
            loadedTexture tex = bch.mips[mipSel].textures[TextureList.SelectedIndex];
            bch.mips[mipSel].textures.RemoveAt(TextureList.SelectedIndex);
            tex.type = findType();
            bch.mips[mipSel].textures.Insert(TextureList.SelectedIndex, tex);
        }
        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            using (SaveFileDialog saveDlg = new SaveFileDialog())
            {
                saveDlg.Filter = "Image|*.png";
                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    bch.mips[mipSel].textures[TextureList.SelectedIndex].texture.texture.Save(saveDlg.FileName);
                }
            }
        }

        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            using (FolderBrowserDialog browserDlg = new FolderBrowserDialog())
            {
                if (browserDlg.ShowDialog() == DialogResult.OK)
                {
                    foreach (loadedTexture tex in bch.mips[mipSel].textures)
                    {
                        string outFile = Path.Combine(browserDlg.SelectedPath, tex.texture.name);
                        tex.texture.texture.Save(outFile + ".png");
                    }
                }
            }
        }

        private void BtnReplace_Click(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            using (OpenFileDialog openDlg = new OpenFileDialog())
            {
                openDlg.Filter = "Image|*.png";
                if (openDlg.ShowDialog() == DialogResult.OK)
                {
                    loadedTexture tex = bch.mips[mipSel].textures[TextureList.SelectedIndex];
                    bch.mips[mipSel].textures.RemoveAt(TextureList.SelectedIndex);
                    Bitmap newTexture = new Bitmap(openDlg.FileName);
                    tex.texture.texture = newTexture;
                    tex.modified = true;
                    bch.mips[mipSel].textures.Insert(TextureList.SelectedIndex, tex);
                    PicPreview.Image = newTexture;
                }
            }
        }

        private void BtnReplaceAll_Click(object sender, EventArgs e)
        {
            if (bch == null) return;

            using (FolderBrowserDialog browserDlg = new FolderBrowserDialog())
            {
                if (browserDlg.ShowDialog() == DialogResult.OK)
                {
                    string[] files = Directory.GetFiles(browserDlg.SelectedPath);
                    for (int i = 0; i < bch.mips[mipSel].textures.Count; i++)
                    {
                        loadedTexture tex = bch.mips[mipSel].textures[i];

                        foreach (string file in files)
                        {
                            string name = Path.GetFileNameWithoutExtension(file);

                            if (string.Compare(name, tex.texture.name, StringComparison.InvariantCultureIgnoreCase) == 0)
                            {
                                loadedTexture newTex = tex;
                                bch.mips[mipSel].textures.RemoveAt(i);
                                Bitmap newTexture = new Bitmap(file);
                                tex.texture.texture = newTexture;
                                tex.modified = true;
                                bch.mips[mipSel].textures.Insert(i, tex);
                                if (TextureList.SelectedIndex == i) PicPreview.Image = newTexture;
                            }
                        }
                    }
                }
            }
        }

        private void save()
        {
            using (FileStream data = new FileStream(currentFile, FileMode.Open))
            {
                BinaryReader input = new BinaryReader(data);
                BinaryWriter output = new BinaryWriter(data);

                for (int i = 0; i < bch.mips[mipSel].textures.Count; i++)
                {
                    loadedTexture tex = bch.mips[mipSel].textures[i];

                    if (tex.modified)
                    {
                        byte[] buffer = align(TextureCodec.encode(tex.texture.texture, tex.type));
                        int diff = buffer.Length - tex.length;
                        replaceData(data, tex.offset, returnSize(tex.type,tex.texture.texture.Width, tex.texture.texture.Height), buffer);

                        tex.modified = false;
                        updateTexture(i, tex);

                    }
                }
            }

            MessageBox.Show("Done!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private byte[] align(byte[] input)
        {
            int length = input.Length;
            while ((length & 0x7f) > 0) length++;
            byte[] output = new byte[length];
            Buffer.BlockCopy(input, 0, output, 0, input.Length);
            return output;
        }
        private static string getMagic(BinaryReader input, uint length)
        {
            string magic = IOUtils.readString(input, 0, length);
            input.BaseStream.Seek(0, SeekOrigin.Begin);
            return magic;
        }
        private void replaceCommand(Stream data, BinaryWriter output, uint newVal)
        {
            data.Seek(-8, SeekOrigin.Current);
            output.Write(newVal);
            data.Seek(4, SeekOrigin.Current);
        }

        private void replaceData(Stream data, uint offset, int length, byte[] newData)
        {
            data.Seek(offset, SeekOrigin.Begin);
            data.Write(newData, 0, length);
        }

        private void updateAddress(Stream data, BinaryReader input, BinaryWriter output, int diff)
        {
            uint offset = input.ReadUInt32();
            offset = (uint)(offset + diff);
            data.Seek(-4, SeekOrigin.Current);
            output.Write(offset);
        }

        private void updateTexture(int index, loadedTexture newTex)
        {
            bch.mips[mipSel].textures.RemoveAt(index);
            bch.mips[mipSel].textures.Insert(index, newTex);
        }
        private RenderBase.OTextureFormat findType()
        {
            return (RenderBase.OTextureFormat)System.Enum.Parse(typeof(RenderBase.OTextureFormat), TextureTypeDrop.Text);
        }

        private void MipLayerChanged(object sender, EventArgs e)
        {
            mipSel = Int32.Parse(MipSelect.Text) - 1;
            updateTexturesList();
        }
    }
}
