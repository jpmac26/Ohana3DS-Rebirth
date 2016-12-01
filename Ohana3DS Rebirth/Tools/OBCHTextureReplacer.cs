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

        private struct loadedMaterial
        {
            public string texture0;
            public string texture1;
            public string texture2;
            public uint gpuCommandsOffset;
            public uint gpuCommandsWordCount;
        }

        private class loadedBCH
        {
            public uint mainHeaderOffset;
            public uint gpuCommandsOffset;
            public uint dataOffset;
            public uint relocationTableOffset;
            public uint relocationTableLength;
            public List<loadedTexture> textures;
            public List<loadedMaterial> materials;

            public loadedBCH()
            {
                textures = new List<loadedTexture>();
                materials = new List<loadedMaterial>();
            }
        }

        loadedBCH bch;

        public OBCHTextureReplacer(FrmMain parent)
        {
            InitializeComponent();
            parentForm = parent;
            TopMenu.Renderer = new OMenuStrip();
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
                openDlg.Filter = "All supported files|*.bch;*.ctpk";

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

        private bool open(string fileName)
        {
            using (FileStream data = new FileStream(fileName, FileMode.Open))
            {
                BinaryReader input = new BinaryReader(data);

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
                        switch (fmt)
                        {
                            case RenderBase.OTextureFormat.rgba8: tex.length = (textureSize.Width * textureSize.Height) * 4; break;
                            case RenderBase.OTextureFormat.rgb8: tex.length = (textureSize.Width * textureSize.Height) * 3; break;
                            case RenderBase.OTextureFormat.rgba5551: tex.length = (textureSize.Width * textureSize.Height) * 2; break;
                            case RenderBase.OTextureFormat.rgb565: tex.length = (textureSize.Width * textureSize.Height) * 2; break;
                            case RenderBase.OTextureFormat.rgba4: tex.length = (textureSize.Width * textureSize.Height) * 2; break;
                            case RenderBase.OTextureFormat.la8: tex.length = (textureSize.Width * textureSize.Height) * 2; break;
                            case RenderBase.OTextureFormat.hilo8: tex.length = (textureSize.Width * textureSize.Height) * 2; break;
                            case RenderBase.OTextureFormat.l8: tex.length = textureSize.Width * textureSize.Height; break;
                            case RenderBase.OTextureFormat.a8: tex.length = textureSize.Width * textureSize.Height; break;
                            case RenderBase.OTextureFormat.la4: tex.length = textureSize.Width * textureSize.Height; break;
                            case RenderBase.OTextureFormat.l4: tex.length = (textureSize.Width * textureSize.Height) >> 1; break;
                            case RenderBase.OTextureFormat.a4: tex.length = (textureSize.Width * textureSize.Height) >> 1; break;
                            case RenderBase.OTextureFormat.etc1: tex.length = (textureSize.Width * textureSize.Height) >> 1; break;
                            case RenderBase.OTextureFormat.etc1a4: tex.length = textureSize.Width * textureSize.Height; break;
                            default: throw new Exception("OBCHTextureReplacer: Invalid texture format on BCH!");
                        }

                        while ((tex.length & 0x7f) > 0) tex.length++;

                        data.Seek(tex.offset, SeekOrigin.Begin);
                        byte[] buffer = new byte[textureSize.Width * textureSize.Height * 4];
                        input.Read(buffer, 0, buffer.Length);
                        Bitmap texture = TextureCodec.decode(
                            buffer,
                            textureSize.Width,
                            textureSize.Height,
                            fmt);

                        tex.texture = new RenderBase.OTexture(texture, textureName);

                        bch.textures.Add(tex);
                    }

                    bch.mainHeaderOffset = mainHeaderOffset;
                    bch.gpuCommandsOffset = gpuCommandsOffset;
                    bch.dataOffset = dataOffset;
                    bch.relocationTableOffset = relocationTableOffset;
                    bch.relocationTableLength = relocationTableLength;
                }
                else if(magic == "CTPK\u0001")
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

                    for (int i = 0; i < numTexture; i++)
                    {
                        data.Seek(0x20 * (i + 1), SeekOrigin.Begin);
                        loadedTexture tex;
                        tex.modified = false;

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

                        tex.gpuCommandsOffset = 0;
                        tex.gpuCommandsWordCount = 0;

                        bch.textures.Add(tex);
                    }
                }
            }

            updateTexturesList();
            return true;
        }

        private void updateTexturesList()
        {
            TextureList.flush();
            PicPreview.Image = null;
            foreach (loadedTexture tex in bch.textures) TextureList.addItem(tex.texture.name);
            TextureList.Refresh();
        }

        private void TextureList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            PicPreview.Image = bch.textures[TextureList.SelectedIndex].texture.texture;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (TextureList.SelectedIndex == -1) return;

            using (SaveFileDialog saveDlg = new SaveFileDialog())
            {
                saveDlg.Filter = "Image|*.png";
                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    bch.textures[TextureList.SelectedIndex].texture.texture.Save(saveDlg.FileName);
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
                    foreach (loadedTexture tex in bch.textures)
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
                    loadedTexture tex = bch.textures[TextureList.SelectedIndex];
                    bch.textures.RemoveAt(TextureList.SelectedIndex);
                    Bitmap newTexture = new Bitmap(openDlg.FileName);
                    tex.texture.texture = newTexture;
                    tex.modified = true;
                    bch.textures.Insert(TextureList.SelectedIndex, tex);
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
                    for (int i = 0; i < bch.textures.Count; i++)
                    {
                        loadedTexture tex = bch.textures[i];

                        foreach (string file in files)
                        {
                            string name = Path.GetFileNameWithoutExtension(file);

                            if (string.Compare(name, tex.texture.name, StringComparison.InvariantCultureIgnoreCase) == 0)
                            {
                                loadedTexture newTex = tex;
                                bch.textures.RemoveAt(i);
                                Bitmap newTexture = new Bitmap(file);
                                tex.texture.texture = newTexture;
                                tex.modified = true;
                                bch.textures.Insert(i, tex);
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

                for (int i = 0; i < bch.textures.Count; i++)
                {
                    loadedTexture tex = bch.textures[i];

                    if (tex.modified)
                    {
                        byte[] buffer = align(TextureCodec.encode(tex.texture.texture, tex.type));
                        int diff = buffer.Length - tex.length;

                        replaceData(data, tex.offset, tex.length, buffer);

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
            bch.textures.RemoveAt(index);
            bch.textures.Insert(index, newTex);
        }
    }
}
