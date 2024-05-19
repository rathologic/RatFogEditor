using System;
using System.Drawing.Text;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;

namespace RatFogEditor
{
    public partial class EditorMain : Form
    {
        Stream openFile = null;
        // Block headers for the two different versions
        int[] classichd = { 1867, 2252, 2750, 3030, 3396, 3762 };
        int[] alpha = { 1497, 1882, 2248, 2614, 2980 };

        public EditorMain() {
            InitializeComponent();

            Stream template = new MemoryStream(RatFogEditor.Properties.Resources.classichd_template);
            openFile = template;
            loadFile(template, "Classic HD template");
        }

        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            if (loadFileDialog.ShowDialog() == DialogResult.OK) {
                string filePath = loadFileDialog.FileName;
                loadFileWrapper(filePath);
            }
        }

        private void setColor(PictureBox box) {
            colorDialog1.Color = box.BackColor;
            if (colorDialog1.ShowDialog() == DialogResult.OK) {
                assignColor(box, colorDialog1.Color);
            }
        }

        private void assignColor(PictureBox box, Color color) {
            box.BackColor = color;
        }

        private void assignFloat(NumericUpDown num, float value)
        {
            num.Value = (decimal)value;
        }


        private void loadFileWrapper(string file) {
            // Close file stream if one is already open
            try { 
                if (openFile != null) { openFile.Close(); openFile = null; }
                // Attempt to open file
                FileStream str = System.IO.File.Open(file, FileMode.Open);
                openFile = str;
                loadFile(str, Path.GetFileName(file));
            } catch (Exception e) {
                labelDebugMsg.Text = "Failed to load " + Path.GetFileName(file) + ": " + e.Message;
            }
        }

        public void loadFile(Stream str, string filename) {
            // Block offset list, starting at ambient color head (after 16 00)
            int[] game;

            try {
                BinaryReader reader = new BinaryReader(str);
                long fileLen = reader.BaseStream.Length;

                System.Diagnostics.Debug.WriteLine("Opened file " + filename);

                // Detect which version of the game this is for
                switch (fileLen) {
                    case 8219:
                        // Classic HD and 2005 have the same format
                        game = classichd;
                        setNightControls(true);
                        break;
                    case 4607:
                        game = alpha;
                        setNightControls(false);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("File length "+fileLen+" not recognized; assuming Classic HD");
                        game = classichd;
                        setNightControls(true);
                        break;
                }

                // Read each block, 1 at a time
                for (int i = 0; i < game.Length; i++) {
                    readBlock(reader, game[i], i + 1);
                }

                // If not alpha, load plague district colors
                if (game == classichd) {
                    readPlagueColors(reader);
                }

                // Fix not-applicable numeric displays for dawn
                numActive1.Value = 6;
                numEnd1.Value = 0;

                // Update display message
                labelDebugMsg.Text = "Successfully loaded " +filename;

            } catch (Exception e) {
                // File didn't open
                labelDebugMsg.Text = "Failed to load " +filename+": " +e.Message;
            }
        }

        public void saveFile(string file) {
            try
            {
                // Precondition: openFile is not null
                using (BinaryWriter writer = new BinaryWriter(new FileStream(file, FileMode.Create, FileAccess.Write)))
                {
                    int[] game;
                    int fileLen = (int)openFile.Length;
                    BinaryReader reader = new BinaryReader(openFile);

                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    writer.Write(reader.ReadBytes(fileLen));

                    // Switch fileLen to determine format
                    switch (fileLen) {
                        case 8219:
                            game = classichd;
                            break;
                        case 4607:
                            game = alpha;
                            break;
                        default:
                            game = classichd;
                            break;
                    }

                    for (int i = 0; i < game.Length; i++) {
                        writeBlock(writer, game[i], i + 1);
                    }

                    // If not alpha, write plague district colors
                    if (game == classichd) {
                        writePlagueColors(writer);
                    }

                    labelDebugMsg.Text = "Successfully wrote " + file;

                }
            }
            catch (Exception e)
            {
                // Unable to open output file
                labelDebugMsg.Text = "Unable to save file: " + e.Message;
            }
        }

        private void setNightControls(bool value) {
            // Turns off unused rows when editing alpha file
            controlsNight.Enabled = value;
            controlsInfected.Enabled = value;
            controlsBurned.Enabled = value;
            controlsGen.Enabled = value;
            assignColor(colorAmb6, Color.Transparent);
            assignColor(colorSun6, Color.Transparent);
            assignColor(colorFog6, Color.Transparent);
            assignColor(colorRain6, Color.Transparent);
            assignColor(colorAmbP, Color.Transparent);
            assignColor(colorSunP, Color.Transparent);
            assignColor(colorAmbB, Color.Transparent);
            assignColor(colorSunB, Color.Transparent);
            assignColor(colUnknown1, Color.Transparent);
            assignColor(colUnknown2, Color.Transparent);
            assignColor(colGeneralTint, Color.Transparent);
        }

        private void readBlock(BinaryReader r, int offset, int block) {
            // For each of the 8 block values, read it & set GUI elements
            PictureBox[] boxes = getBoxes(block);
            NumericUpDown[] numbers = getNumbers(block);

            r.BaseStream.Seek(offset, SeekOrigin.Begin);
            // ambient color is at head of block
            assignColor(boxes[0], readColor(r));

            // space of 2 before 12B sun col starts
            r.BaseStream.Seek(2, SeekOrigin.Current);
            assignColor(boxes[1], readColor(r));

            // space of 2 before 4B fog distance
            r.BaseStream.Seek(2, SeekOrigin.Current);
            assignFloat(numbers[0], readFloat(r));

            // space of 2 before 4B render distance
            r.BaseStream.Seek(2, SeekOrigin.Current);
            assignFloat(numbers[1], readFloat(r));

            // space of 2 before 12B fog color
            r.BaseStream.Seek(2, SeekOrigin.Current);
            assignColor(boxes[2], readColor(r));

            // space of 2 before 12B rain color
            r.BaseStream.Seek(2, SeekOrigin.Current);
            assignColor(boxes[3], readColor(r));

            // space of 15 before 4B active time
            // trying -2
            r.BaseStream.Seek(15, SeekOrigin.Current);
            assignFloat(numbers[2], readFloat(r));

            // space of 156 before 4B end time
            r.BaseStream.Seek(156, SeekOrigin.Current);
            assignFloat(numbers[3], readFloat(r));
        }

        private void writeBlock(BinaryWriter r, int offset, int block)
        {
            // For each of the 8 block values, read it from GUI + set
            PictureBox[] boxes = getBoxes(block);
            NumericUpDown[] numbers = getNumbers(block);

            r.BaseStream.Seek(offset, SeekOrigin.Begin);
            // ambient color is at head of block
            writeColor(boxes[0].BackColor, r, 255);

            // space of 2 before 12B sun col starts
            r.BaseStream.Seek(2, SeekOrigin.Current);
            writeColor(boxes[1].BackColor, r, 255);

            // space of 2 before 4B fog distance
            r.BaseStream.Seek(2, SeekOrigin.Current);
            writeFloat(numbers[0], r);

            // space of 2 before 4B render distance
            r.BaseStream.Seek(2, SeekOrigin.Current);
            writeFloat(numbers[1], r);

            // space of 2 before 12B fog color
            r.BaseStream.Seek(2, SeekOrigin.Current);
            writeColor(boxes[2].BackColor, r, 255);

            // space of 2 before 12B rain color
            r.BaseStream.Seek(2, SeekOrigin.Current);
            writeColor(boxes[3].BackColor, r, 255);

            if (block != 1) {
                // space of 15 before 4B active time
                r.BaseStream.Seek(15, SeekOrigin.Current);
                writeFloat(numbers[2], r);
                //r.BaseStream.Seek(4, SeekOrigin.Current);

                // space of 156 before 4B end time
                r.BaseStream.Seek(156, SeekOrigin.Current);
                writeFloat(numbers[3], r);
            }
        }

        private PictureBox[] getBoxes(int block) {
            switch (block) {
                case 1:
                    return new PictureBox[] { colorAmb1, colorSun1, colorFog1, colorRain1 };
                case 2:
                    return new PictureBox[] { colorAmb2, colorSun2, colorFog2, colorRain2 };
                case 3:
                    return new PictureBox[] { colorAmb3, colorSun3, colorFog3, colorRain3 };
                case 4:
                    return new PictureBox[] { colorAmb4, colorSun4, colorFog4, colorRain4 };
                case 5:
                    return new PictureBox[] { colorAmb5, colorSun5, colorFog5, colorRain5 };
                default: // case 6
                    return new PictureBox[] { colorAmb6, colorSun6, colorFog6, colorRain6 };
            }
        }

        private NumericUpDown[] getNumbers(int block)
        {
            switch (block)
            {
                case 1:
                    return new NumericUpDown[] { numFog1, numDraw1, numActive1, numEnd1 };
                case 2:
                    return new NumericUpDown[] { numFog2, numDraw2, numActive2, numEnd2 };
                case 3:
                    return new NumericUpDown[] { numFog3, numDraw3, numActive3, numEnd3 };
                case 4:
                    return new NumericUpDown[] { numFog4, numDraw4, numActive4, numEnd4 };
                case 5:
                    return new NumericUpDown[] { numFog5, numDraw5, numActive5, numEnd5 };
                default: // case 6
                    return new NumericUpDown[] { numFog6, numDraw6, numActive6, numEnd6 };
            }
        }

        private Color readColor(BinaryReader r) {
            // Reads a 12-byte color from binary file.
            // Each sequence is a vector of 3 four-byte
            // float numbers (0-255 divided by 255).

            long start = r.BaseStream.Position;

            int red = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 255);
            int green = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 255);
            int blue = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 255);

            long end = r.BaseStream.Position;

            return System.Drawing.Color.FromArgb(red, green, blue);
        }

        private void writeColor(Color c, BinaryWriter r, int mult)
        {
            // Writes a 12-byte color to binary file.
            // Each sequence is a vector of 3 four-byte
            // float numbers (0-255 divided by 255 or 128).

            long start = r.BaseStream.Position;

            byte[] red = BitConverter.GetBytes((Single)c.R / mult);
            byte[] green = BitConverter.GetBytes((Single)c.G / mult);
            byte[] blue = BitConverter.GetBytes((Single)c.B / mult);

            List<byte> col = new List<byte>();
            for (int i = 0; i < 4; i++) {
                col.Add(red[i]);
            } for (int i = 0; i < 4; i++) {
                col.Add(green[i]);
            } for (int i = 0; i < 4; i++) {
                col.Add(blue[i]);
            }

            byte[] bcol = col.ToArray();

            r.Write(bcol, 0, 12);

            long end = r.BaseStream.Position;
        }

        private void readPlagueColors(BinaryReader reader)
        {
            // generic tint for normal districts
            reader.BaseStream.Seek(6912, SeekOrigin.Begin);
            assignColor(colGeneralTint, readTintColor(reader));

            // burned ambient (sky) color
            reader.BaseStream.Seek(7085, SeekOrigin.Begin);
            assignColor(colorAmbB, readTintColor(reader));

            // burned district sun (ground) color
            reader.BaseStream.Seek(7171, SeekOrigin.Begin);
            assignColor(colorSunB, readTintColor(reader));

            // don't know what this is
            reader.BaseStream.Seek(7257, SeekOrigin.Begin);
            assignColor(colUnknown1, readTintColor(reader));

            // infected ambient (sky) color
            reader.BaseStream.Seek(7403, SeekOrigin.Begin);
            assignColor(colorAmbP, readTintColor(reader));

            // infected sun (ground) color
            reader.BaseStream.Seek(7489, SeekOrigin.Begin);
            assignColor(colorSunP, readTintColor(reader));

            // don't know this one either
            reader.BaseStream.Seek(7575, SeekOrigin.Begin);
            assignColor(colUnknown2, readTintColor(reader));
        }

        private void writePlagueColors(BinaryWriter reader) {
            // generic tint for normal districts
            reader.BaseStream.Seek(6912, SeekOrigin.Begin);
            writeColor(colGeneralTint.BackColor, reader, 128);

            // burned ambient (sky) color
            reader.BaseStream.Seek(7085, SeekOrigin.Begin);
            writeColor(colorAmbB.BackColor, reader, 128);

            // burned district sun (ground) color
            reader.BaseStream.Seek(7171, SeekOrigin.Begin);
            writeColor(colorSunB.BackColor, reader, 128);

            // don't know what this is
            reader.BaseStream.Seek(7257, SeekOrigin.Begin);
            writeColor(colUnknown1.BackColor, reader, 128);

            // infected ambient (sky) color
            reader.BaseStream.Seek(7403, SeekOrigin.Begin);
            writeColor(colorAmbP.BackColor, reader, 128);

            // infected sun (ground) color
            reader.BaseStream.Seek(7489, SeekOrigin.Begin);
            writeColor(colorSunP.BackColor, reader, 128);

            // don't know this one either
            reader.BaseStream.Seek(7575, SeekOrigin.Begin);
            writeColor(colUnknown2.BackColor, reader, 128);
        }

        private Color readTintColor(BinaryReader r)
        {
            // Reads a 12-byte color from binary file.
            // Each sequence is a vector of 3 four-byte
            // float numbers (0-255 divided by 128).

            int red = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 128);
            int green = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 128);
            int blue = (int)(BitConverter.ToSingle(r.ReadBytes(4), 0) * 128);

            return System.Drawing.Color.FromArgb(red, green, blue);
        }

        private float readFloat(BinaryReader r)
        {
            // Reads a 4-byte float from file.
            float time = BitConverter.ToSingle(r.ReadBytes(4), 0);
            return time;
        }

        private void writeFloat(NumericUpDown n, BinaryWriter r)
        {
            // Writes a 4-byte float to file.
            Single num = (Single)n.Value;
            byte[] numb = BitConverter.GetBytes(num);
            r.Write(numb);
        }

        private void btnRain1_Click(object sender, EventArgs e) { setColor(colorRain1); }
        private void btnRain2_Click(object sender, EventArgs e) { setColor(colorRain2); }
        private void btnRain3_Click(object sender, EventArgs e) { setColor(colorRain3); }
        private void btnRain4_Click(object sender, EventArgs e) { setColor(colorRain4); }
        private void btnRain5_Click(object sender, EventArgs e) { setColor(colorRain5); }
        private void btnRain6_Click(object sender, EventArgs e) { setColor(colorRain6); }
        private void btnPickCol1_Click(object sender, EventArgs e) { setColor(colorFog1); }
        private void btnFog2_Click(object sender, EventArgs e) { setColor(colorFog2); }
        private void btnFog3_Click(object sender, EventArgs e) { setColor(colorFog3); }
        private void btnFog4_Click(object sender, EventArgs e) { setColor(colorFog4); }
        private void btnFog5_Click(object sender, EventArgs e) { setColor(colorFog5); }
        private void btnFog6_Click(object sender, EventArgs e) { setColor(colorFog6); }
        private void btnSun1_Click(object sender, EventArgs e) { setColor(colorSun1); }
        private void btnSun_Click(object sender, EventArgs e) { setColor(colorSun2); }
        private void btnSun3_Click(object sender, EventArgs e) { setColor(colorSun3); }
        private void btnSun4_Click(object sender, EventArgs e) { setColor(colorSun4); }
        private void btnSun5_Click(object sender, EventArgs e) { setColor(colorSun5); }
        private void btnSun6_Click(object sender, EventArgs e) { setColor(colorSun6); }
        private void btnAmb1_Click(object sender, EventArgs e) { setColor(colorAmb1); }
        private void btnAmb2_Click(object sender, EventArgs e) { setColor(colorAmb2); }
        private void btnAmb3_Click(object sender, EventArgs e) { setColor(colorAmb3); }
        private void btnAmb4_Click(object sender, EventArgs e) { setColor(colorAmb4); }
        private void btnAmb5_Click(object sender, EventArgs e) { setColor(colorAmb5); }
        private void btnAmb6_Click(object sender, EventArgs e) { setColor(colorAmb6); }
        private void button5_Click(object sender, EventArgs e) { setColor(colorAmbP); }
        private void btnSunP_Click(object sender, EventArgs e) { setColor(colorSunP); }
        private void btnAmbB_Click(object sender, EventArgs e) { setColor(colorAmbB); }
        private void btnSunB_Click(object sender, EventArgs e) { setColor(colorSunB); }
        private void btnGeneralTint_Click(object sender, EventArgs e) { setColor(colGeneralTint); }
        private void btnUnknownP_Click(object sender, EventArgs e) { setColor(colUnknown2); }
        private void btnUnknownB_Click(object sender, EventArgs e) { setColor(colUnknown1); }

        private void btnSaveFile_Click(object sender, EventArgs e)
        {
            if (openFile == null) {
                // TODO: implement defaults
                labelDebugMsg.Text = "You must open a file first!";
                return;
            }
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = saveFileDialog.FileName;
                saveFile(filePath);
            }
        }
    }
}