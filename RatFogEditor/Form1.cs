using System;
using System.IO;
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;

namespace RatFogEditor
{
    public partial class EditorMain : Form
    {
        FileStream openFile = null;

        public EditorMain()
        {
            InitializeComponent();
        }

        private void openFileDialog1_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            if (loadFileDialog.ShowDialog() == DialogResult.OK) {
                string filePath = loadFileDialog.FileName;
                loadFile(filePath);
            }
        }

        private void btnPickCol1_Click(object sender, EventArgs e) { setColor(colorFog1);  }
        
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

        public void loadFile(string file) {
            // Block offset list, starting at ambient color head (after 16 00)
            int[] classichd = {1867, 2252, 2750, 3030, 3396, 3762};
            int[] alpha = {1497, 1882, 2248, 2614, 2980};
            int[] game;

            try {
                // Close file stream if one is already open
                if (openFile != null) { openFile.Close(); }
                // Attempt to open file
                FileStream str = System.IO.File.Open(file, FileMode.Open);
                openFile = str;
                BinaryReader reader = new BinaryReader(str);
                long fileLen = reader.BaseStream.Length;

                System.Diagnostics.Debug.WriteLine("Opened file " + file);

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
                    System.Diagnostics.Debug.WriteLine("Now loading Block " + (i + 1));
                    readBlock(reader, game[i], i + 1);
                }

                // Fix not-applicable numeric displays for dawn
                numActive1.Value = 6;
                numEnd1.Value = 0;

                // Update display message
                labelDebugMsg.Text = "Successfully loaded " + Path.GetFileName(file);



            } catch (Exception e) {
                // File didn't open
                labelDebugMsg.Text = "Failed to load " + Path.GetFileName(file)+ ": " +e.Message;
            }
        }

        private void setNightControls(bool value) {
            // Turns off unused rows when editing alpha file
            btnAmb6.Enabled = value;
            btnSun6.Enabled = value;
            btnFog6.Enabled = value;
            btnRain6.Enabled = value;
            numFog6.Enabled = value;
            numDraw6.Enabled = value;
            numActive6.Enabled = value;
            numEnd6.Enabled = value;
            assignColor(colorAmb6, Color.Transparent);
            assignColor(colorSun6, Color.Transparent);
            assignColor(colorFog6, Color.Transparent);
            assignColor(colorRain6, Color.Transparent);
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

            System.Diagnostics.Debug.WriteLine("Read " + (end - start) + " bytes (from " + start + " to " + end + ")");
            System.Diagnostics.Debug.WriteLine("Loaded color " +red+", "+green+", "+blue);

            return System.Drawing.Color.FromArgb(red, green, blue);
        }

        private float readFloat(BinaryReader r)
        {
            // Reads a 4-byte float from file.
            float time = BitConverter.ToSingle(r.ReadBytes(4), 0);
            System.Diagnostics.Debug.WriteLine("Loaded float " + time);
            return time;
        }

        public void saveFile(string file) {
            try {
                BinaryWriter outputWriter = new BinaryWriter(new FileStream(file, FileMode.Create));
                outputWriter.Write("Wee");
                // Write can take integers; use this!
                Console.WriteLine("Finished writing file");

                // NOTE: Assume alpha is 63 for everything

            } catch (Exception e) {
                // Unable to open output file
                Console.WriteLine("Unable to save file: " + e.Message);
            }
        }

        private void label4_Click_1(object sender, EventArgs e)
        {

        }

        private void EditorMain_Load(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void btnRain1_Click_1(object sender, EventArgs e)
        {
            setColor(colorRain1);
        }

        private void btnRain2_Click(object sender, EventArgs e)
        {
            setColor(colorRain2);
        }

        private void btnRain3_Click(object sender, EventArgs e)
        {
            setColor(colorRain3);
        }

        private void btnRain4_Click(object sender, EventArgs e)
        {
            setColor(colorRain4);
        }

        private void btnRain5_Click(object sender, EventArgs e)
        {
            setColor(colorRain5);
        }

        private void btnRain6_Click(object sender, EventArgs e)
        {
            setColor(colorRain6);
        }

        private void btnFog2_Click(object sender, EventArgs e)
        {
            setColor(colorFog2);
        }

        private void btnFog3_Click(object sender, EventArgs e)
        {
            setColor(colorFog3);
        }

        private void btnFog4_Click(object sender, EventArgs e)
        {
            setColor(colorFog4);
        }

        private void btnFog5_Click(object sender, EventArgs e)
        {
            setColor(colorFog5);
        }

        private void btnFog6_Click(object sender, EventArgs e)
        {
            setColor(colorFog6);
        }

        private void btnSun1_Click(object sender, EventArgs e)
        {
            setColor(colorSun1);
        }

        private void btnSun_Click(object sender, EventArgs e)
        {
            setColor(colorSun2);
        }

        private void btnSun3_Click(object sender, EventArgs e)
        {
            setColor(colorSun3);
        }

        private void btnSun4_Click(object sender, EventArgs e)
        {
            setColor(colorSun4);
        }

        private void btnSun5_Click(object sender, EventArgs e)
        {
            setColor(colorSun5);
        }

        private void btnSun6_Click(object sender, EventArgs e)
        {
            setColor(colorSun6);
        }

        private void btnAmb1_Click(object sender, EventArgs e)
        {
            setColor(colorAmb1);
        }

        private void btnAmb2_Click(object sender, EventArgs e)
        {
            setColor(colorAmb2);
        }

        private void btnAmb3_Click(object sender, EventArgs e)
        {
            setColor(colorAmb3);
        }

        private void btnAmb4_Click(object sender, EventArgs e)
        {
            setColor(colorAmb4);
        }

        private void btnAmb5_Click(object sender, EventArgs e)
        {
            setColor(colorAmb5);
        }

        private void btnAmb6_Click(object sender, EventArgs e)
        {
            setColor(colorAmb6);
        }
    }
}