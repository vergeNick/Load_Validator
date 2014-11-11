using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Diagnostics;

/*
 *  Validates data sent by client to ensure proper formatting. 
 *  See Norm's verge_enhance_staff_import in vergedev/visualstudioprojects
 *  Start with Demographics tab
 *  See Delivery Information tab for more detail.
 *  
 *  TODO:
 *      enable/disable buttons 
 *      reset validation tags for new validation session. 
 *      test
 *      layout
 *      output file
 *      
 * */

namespace Load_Validator
{
    class Validator
    {
        private String required_header = "staff_id|last_name|first_name|middle_name|suffix|localid|npi|upin|ssn|" + 
                                "gender|date_of_birth|marital_status|maiden_name|organization|department|" +
                                "title|average_days_worked|average_hours_worked|ft_indicator|citizenship|" + 
                                "visa_classification|visa_number|visa_issued|visa_expires|office_email|" + 
                                "office_phone|home_address|home_address2|home_city|home_county|home_state|" + 
                                "home_zipcode|home_email|home_phone|supervisor_local_id|delete";

        private const int cols = 36;
        public static string error_out_file;

        private Criteria[] columns = new Criteria [cols + 1]; // indice == column #
        private Form form;
        private bool validation_completed = false;

        public static ArrayList STAFF_IDS = new ArrayList();
        public static StreamWriter OUTPUT_WRITER;
        public static int row_counter = 0;
        public static Button validate_button;
        public static ArrayList labels_list;

        // stream reference to input data
        private Stream data;
        private Label filetarget;

        public static void writeError(string line, string column, string field)
        {
            using (OUTPUT_WRITER = File.AppendText(error_out_file))
            {
                string text = "Error in line " + row_counter + " of input file. " +
                                " Column \'" + column +
                                "\' has invalid value: \'" + field + "\'";
                                
                OUTPUT_WRITER.WriteLine(text);
                OUTPUT_WRITER.WriteLine("Full row data: " + line);
                OUTPUT_WRITER.WriteLine("-----------------------------------------------------");
            }
        }

        public void resetFlags()
        {
            if (validation_completed)
            {
                form.Controls.Clear();
                this.addControls(this);
                validation_completed = false;
            }
        }

        private void openFileDialog(object sender, System.EventArgs e)
        {
            Stream stream = null;

            OpenFileDialog opener = new OpenFileDialog();
            opener.InitialDirectory = Directory.GetCurrentDirectory();

            if (opener.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((stream = opener.OpenFile()) != null) // successfully open file
                    {

                        resetFlags();

                        this.filetarget.Text = opener.FileName;
                        this.filetarget.Refresh();
                        this.data = stream;

                        string[] path = opener.FileName.Split('\\');
                        string file_name = path[path.Length - 1];
                        file_name = file_name.Split('.')[0];

                        // build an error file path and set the writer
                        StringBuilder error_path = new StringBuilder();
                        error_path.Append(opener.InitialDirectory + "\\");
                        error_path.Append(file_name + "_error_out.txt");
                        File.CreateText(error_path.ToString()).Close();
                        error_out_file = error_path.ToString();

                        validate_button.Enabled = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Something went wrong..." + ex.Message);
                }
            }
        }

        private void validate(object sender, System.EventArgs e)
        {
            StreamReader reader = File.OpenText(this.filetarget.Text);
            StreamReader endline_reader = File.OpenText(this.filetarget.Text);
            String line = reader.ReadLine();
            String [] fields = new String[cols];
            char[] separator = new char[1] { '|' };
            row_counter += 1;

            // First check for perfect header match:
            if (!this.required_header.Equals(line))
            {
                flagLabels.header.fail();
                writeError(line, "Header", "Header does not match the required format.");
            }

            // Check for CRLF before moving to next line.
            // Must use Read() to identify CR and LF
            // Move Read() pointer to end of line
            for (int i = 1; i <= line.Length; i++)
            {
                endline_reader.Read();
            }
            if (!(endline_reader.Read().Equals('\r') & (endline_reader.Read().Equals('\n'))))
            {
                flagLabels.newlines.fail();
                writeError(line, "End of Line", "Line did not terminate with CRLF.");
            }

            // Now start loop for normal rows
            while ((line = reader.ReadLine()) != null)
            {
                row_counter += 1;
                fields = line.Split(separator, StringSplitOptions.None);

                if (fields.Length != cols)
                {
                    flagLabels.field_count.fail();
                    writeError(line, "Number of Columns", "The number of columns does not match the specifcation (" + cols + ")");
                }

                // check if staff id is unique
                if (STAFF_IDS.Contains(fields[0]))
                {
                    flagLabels.staff_ids.fail();
                    writeError(line, "Unique Staff Id", "The staff id is not unique.");
                }
                STAFF_IDS.Add(fields[0]);

                for (int i = 0; i < cols; i++)
                {
                    //Debug.WriteLine("index: " + i + " " + this.columns[i+1].column_name.ToString() + " " + fields[i].ToString());
                    if (fields[i] == null)
                    {
                        fields[i] = "";
                    }

                    // if a field fails, write the row to error file with a message
                   if (!this.columns[i+1].validate(fields[i]))
                   {
                       writeError(line, this.columns[i + 1].column_name, fields[i]);
                   }
                }


                // TODO: check CRLF at end of each line (see code above)
                for (int i = 1; i <= line.Length; i++)
                {
                    endline_reader.Read();
                }
                if (!(endline_reader.Read().Equals('\r') & (endline_reader.Read().Equals('\n'))))
                {
                    flagLabels.newlines.fail();
                    writeError(line, "End of Line", "Line did not terminate with CRLF.");
                }
            }

            validation_completed = true;
            this.setFlags();
        }

        public void setFlags()
        {
            bool errs = false;
            foreach(flagLabel label in labels_list)
            {
                if (!label.valid)
                {
                    label.Text = "Failed";
                    label.BackColor = Color.Red;
                    errs = true;
                }
                else
                {
                    label.Text = "Passed";
                    label.BackColor = Color.Green;
                }
            }

            if (errs)
            {
                Label error_message = new Label();
                error_message.Text = "See \'" + error_out_file + "\' for error information.";
                error_message.Location = new Point(120, 320);
                error_message.Size = new Size(405, 100);
                form.Controls.Add(error_message);
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Validator v = new Validator();
            v.form = new Form();
            v.form.Size = new Size(600, 400);
            v.form.Text = "Load File Validator";

            v.addControls(v);
            setCriteria(v);

            v.form.ShowDialog();
            Application.Run(v.form);
        }

        

        private void addControls(Validator v)
        {
            labels_list = new ArrayList();

            flagLabels.required_field = new flagLabel("Required Fields"); labels_list.Add(flagLabels.required_field);
            flagLabels.field_count = new flagLabel("Field Count"); labels_list.Add(flagLabels.field_count);
            flagLabels.header = new flagLabel("Header"); labels_list.Add(flagLabels.header);
            flagLabels.newlines = new flagLabel("New Lines"); labels_list.Add(flagLabels.newlines);
            flagLabels.decimals = new flagLabel("Decimals"); labels_list.Add(flagLabels.decimals);
            flagLabels.bits = new flagLabel("Bits"); labels_list.Add(flagLabels.bits);
            flagLabels.dates = new flagLabel("Dates"); labels_list.Add(flagLabels.dates);
            flagLabels.lengths = new flagLabel("Lengths"); labels_list.Add(flagLabels.lengths);
            flagLabels.enums = new flagLabel("Accepted Values"); labels_list.Add(flagLabels.enums);
            flagLabels.staff_ids = new flagLabel("Staff IDs"); labels_list.Add(flagLabels.staff_ids);

            int y = 20;
            foreach (flagLabel flag in labels_list)
            {
                flag.Text = flag.Name + "....";
                flag.Location = new Point(470, y);
                flag.BorderStyle = BorderStyle.FixedSingle;
                flag.TextAlign = ContentAlignment.MiddleCenter;
                flag.BackColor = Color.Gray;
                
                v.form.Controls.Add(flag);

                Label title = new Label();
                title.Text = flag.name;
                title.Location = new Point(310, y);
                title.TextAlign = ContentAlignment.MiddleLeft;
                title.Font = new Font("Sans Serif", 9);
                
                v.form.Controls.Add(title);

                y += 30;
            }

            v.filetarget = new Label();
            v.filetarget.Location = new Point(10, 120);
            v.filetarget.Size = new Size(400, 40);
            v.filetarget.Text = "(none)";

            // remove this when done testing. 
            //v.filename.Text = "C:\\Users\\Nick\\Documents\\Verge_Dev\\Visual Studio Projects\\staff_import_test_2.txt";

            PictureBox logo = new PictureBox();
            logo.Image = Load_Validator.Properties.Resources.vergesolutions;
            logo.Size = new Size(272, 120);
            logo.Location  = new Point(0,0);

            Button pick_file_button = new Button();
            pick_file_button.Text = "Select Data File";
            pick_file_button.Size = new Size(100, 50);
            pick_file_button.Location = new Point(30, 165);
            pick_file_button.Click += new EventHandler(v.openFileDialog);

            validate_button = new Button();
            validate_button.Text = "Validate Input File";
            validate_button.Location = new Point(135, 165);
            validate_button.Size = new Size(100, 50);
            validate_button.Click += new EventHandler(v.validate);
            validate_button.Enabled = false;

            Button quit_button = new Button();
            quit_button.Text = "Quit";
            quit_button.Size = new Size(100, 50);
            quit_button.Location = new Point(10, 305);
            quit_button.Click += new EventHandler(v.appQuit);

            Label filename_label = new Label();
            filename_label.Location = new Point(10, 100);
            filename_label.Font = new Font(filename_label.Font, FontStyle.Bold);
            filename_label.Text = "Input File:";

            v.form.Controls.Add(pick_file_button);
            v.form.Controls.Add(v.filetarget);
            v.form.Controls.Add(filename_label);
            v.form.Controls.Add(validate_button);
            v.form.Controls.Add(quit_button);
            v.form.Controls.Add(logo);
        }

        static void setCriteria(Validator v)
        {
            string [] suffix = new string [7] {"Sr.", "Jr.", "II", "III", "IV", "V", ""};
            string [] gender = new string [3] {"M", "F", ""};
            string [] marital_status = new string [3] {"Married", "Not Married", ""};
            string [] bit = new string [3] {"0", "1", ""};

            /*
            *   public Criteria(int column, string column_name, int len, 
                bool required, bool isDate, bool isDec, string [] accepted)
             * */

            v.columns[1] = new Criteria(1, "staff_id", 50, true, false, false, null);
            v.columns[2] = new Criteria(2, "last_name", 50, true, false, false, null);
            v.columns[3] = new Criteria(3, "first_name", 50, true, false, false, null);
            v.columns[4] = new Criteria(4, "middle_name", 50, false, false, false, null);
            v.columns[5] = new Criteria(5, "suffix", 3, false, false, false, suffix);
            v.columns[6] = new Criteria(6, "localid", 50, false, false, false, null);
            v.columns[7] = new Criteria(7, "npi", 50, false, false, false, null);
            v.columns[8] = new Criteria(8, "upin", 50, false, false, false, null);
            v.columns[9] = new Criteria(9, "ssn", 50, false, false, false, null);
            v.columns[10] = new Criteria(10, "gender", 1, false, false, false, gender);
            v.columns[11] = new Criteria(11, "date_of_birth", 0, false, true, false, null);
            v.columns[12] = new Criteria(12, "marital_status", 20, false, false, false, marital_status);
            v.columns[13] = new Criteria(13, "maiden_name", 50, false, false, false, null);
            v.columns[14] = new Criteria(14, "organization", 200, true, false, false, null);
            v.columns[15] = new Criteria(15, "department", 100, false, false, false, null);
            v.columns[16] = new Criteria(16, "title", 100, false, false, false, null);
            v.columns[17] = new Criteria(17, "average_days_worked", 0, false, false, true, null);
            v.columns[18] = new Criteria(18, "average_hours_worked", 0, false, false, true, null);
            v.columns[19] = new Criteria(19, "ft_indicator", 1, false, false, false, bit);
            v.columns[20] = new Criteria(20, "citizenship", 50, false, false, false, null);
            v.columns[21] = new Criteria(21, "visa_classification", 50, false, false, false, null);
            v.columns[22] = new Criteria(22, "visa_number", 50, false, false, false, null);
            v.columns[23] = new Criteria(23, "visa_issued", 0, false, true, false, null);
            v.columns[24] = new Criteria(24, "visa_expires", 0, false, true, false, null);
            v.columns[25] = new Criteria(25, "office_email", 100, false, false, false, null);
            v.columns[26] = new Criteria(26, "office_phone", 50, false, false, false, null);
            v.columns[27] = new Criteria(27, "home_address", 200, false, false, false, null);
            v.columns[28] = new Criteria(28, "home_address2", 200, false, false, false, null);
            v.columns[29] = new Criteria(29, "home_city", 100, false, false, false, null);
            v.columns[30] = new Criteria(30, "home_county", 100, false, false, false, null);
            v.columns[31] = new Criteria(31, "home_state", 100, false, false, false, null);
            v.columns[32] = new Criteria(32, "home_zipcode", 50, false, false, false, null);
            v.columns[33] = new Criteria(33, "home_email", 100, false, false, false, null);
            v.columns[34] = new Criteria(34, "home_phone", 20, false, false, false, null);
            v.columns[35] = new Criteria(35, "supervisor_local_id", 50, false, false, false, null);
            v.columns[36] = new Criteria(36, "delete", 1, false, false, false, bit);
        }

        private void appQuit(object sender, System.EventArgs e)
        {
            this.form.Close();
            Application.Exit();
        }
    }
	
	 /*
     * static collection of label helper class. 
     * holds both the label and bool values
     */
    public static class flagLabels
    {
        public static flagLabel field_count;
        public static flagLabel required_field;
        public static flagLabel header;
        public static flagLabel newlines;
        public static flagLabel dates;
        public static flagLabel decimals;
        public static flagLabel bits;
        public static flagLabel lengths;
        public static flagLabel enums;
        public static flagLabel staff_ids;
    }

    public class flagLabel : Label
    {
        public bool valid { get; set; }
        public string name { get; set; }

        public flagLabel(string name)
        {
            this.name = name;
            this.valid = true;
        }

        // call this to do stuff if there is a failure
        public void fail()
        {
            this.Text = this.name + " Failed";
            //this.BackColor = "Red";
            this.valid = false;
        }
    }

    public class ValidatorFailed
    {
        public string text;
    }
}
