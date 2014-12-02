using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Data.Linq;
using System.Linq;
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
 *      12/2 - Add option to choose different table
 *      12/2 - Columns array changes with chosen table (Demographic, EmployeeJobDetail, LicenseCertification)
 *      test...
 *      
 * */

namespace Load_Validator
{
    class Validator
    {
        // this array redefines as user chooses different tables (Demographic, EmployeeJobDetail, LicenseCertification)
        private Criteria[] COLUMNS; // = new Criteria [cols + 1]; // indice == column #

        private Form form;
        private bool validation_completed = false;

        public static int cols = 35;
        public static ArrayList STAFF_IDS = new ArrayList();
        public static StreamWriter OUTPUT_WRITER;
        public static int row_counter = 0;
        public static Button validate_button;
        public static Button open_errors;
        public static ArrayList labels_list;
        public static string[] versions = new string[] { "0.7", "0.6" }; // our set of versions. 
        public static ComboBox version_;
        public static Label version_label;
        public static string required_header = "staff_id|last_name|first_name|middle_name|suffix|npi|upin|ssn|" +
                        "gender|date_of_birth|marital_status|maiden_name|organization|department|" +
                        "title|average_days_worked|average_hours_worked|ft_indicator|citizenship|" +
                        "visa_classification|visa_number|visa_issued|visa_expires|office_email|" +
                        "office_phone|home_address|home_address2|home_city|home_county|home_state|" +
                        "home_zipcode|home_email|home_phone|supervisor_local_id|delete";

        // stream reference to input data
        private Stream data;
        private Label filetarget;
        public static string error_out_file;

        public static void writeError(string line, string column, string field)
        {
            using (OUTPUT_WRITER = File.AppendText(error_out_file))
            {
                string text = "Error in line " + row_counter + " of input file. " +
                                " Column \'" + column +
                                "\' has invalid value: \'" + field + "\'";
                                
                OUTPUT_WRITER.WriteLine(text);
                OUTPUT_WRITER.WriteLine();
                OUTPUT_WRITER.WriteLine("Full row data: " + line);
                OUTPUT_WRITER.WriteLine("--------------------------------------------------------------------------------------------------");
            }
        }

        // overload error message to include spec.
        public static void writeError(string line, string column, string field, string spec)
        {
            using (OUTPUT_WRITER = File.AppendText(error_out_file))
            {
                string text = "Error in line " + row_counter + " of input file. " +
                                " Column \'" + column +
                                "\' has invalid value: \'" + field + "\'";

                OUTPUT_WRITER.WriteLine(text);
                OUTPUT_WRITER.WriteLine();
                OUTPUT_WRITER.WriteLine(spec);
                OUTPUT_WRITER.WriteLine();
                OUTPUT_WRITER.WriteLine("Full row data: " + line);
                OUTPUT_WRITER.WriteLine("--------------------------------------------------------------------------------------------------");
            }
        }

        public void resetFlags()
        {
            if (validation_completed)
            {
                row_counter = 0;
                form.Controls.Clear();
                STAFF_IDS.Clear();
                this.addControls(this);
                validation_completed = false;
            }
        }

        private void versionChanged(object sender, System.EventArgs e)
        {
            if (version_.SelectedValue == "0.6") // version 0.6
            {
                Debug.WriteLine("box unchecked");
                cols = 36;
                required_header = "staff_id|last_name|first_name|middle_name|suffix|localid|npi|upin|ssn|" +
                        "gender|date_of_birth|marital_status|maiden_name|organization|department|" +
                        "title|average_days_worked|average_hours_worked|ft_indicator|citizenship|" +
                        "visa_classification|visa_number|visa_issued|visa_expires|office_email|" +
                        "office_phone|home_address|home_address2|home_city|home_county|home_state|" +
                        "home_zipcode|home_email|home_phone|supervisor_local_id|delete";
            }
            else if (version_.SelectedValue == "0.7") // version 0.7
            {
                Debug.WriteLine("box checked");
                cols = 35;
                required_header = "staff_id|last_name|first_name|middle_name|suffix|npi|upin|ssn|" +
                        "gender|date_of_birth|marital_status|maiden_name|organization|department|" +
                        "title|average_days_worked|average_hours_worked|ft_indicator|citizenship|" +
                        "visa_classification|visa_number|visa_issued|visa_expires|office_email|" +
                        "office_phone|home_address|home_address2|home_city|home_county|home_state|" +
                        "home_zipcode|home_email|home_phone|supervisor_local_id|delete";
            }
            setCriteria(this);
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
            if (!required_header.Equals(line))
            {
                flagLabels.header.fail();
                writeError(line, "Header", "Header does not match the required format.");
                open_errors.Enabled = true;
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
                open_errors.Enabled = true;
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
                    open_errors.Enabled = true;
                }
                STAFF_IDS.Add(fields[0]);

                for (int i = 0; i < cols; i++)
                {
                    try
                    {
                        // if a field fails, write the row to error file with a message
                        if (!this.COLUMNS[i + 1].validate(fields[i]))
                        {
                            writeError(line, this.COLUMNS[i + 1].column_name, fields[i], this.COLUMNS[i + 1].spec());
                            open_errors.Enabled = true;
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        flagLabels.field_count.fail();
                    }
                }

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
            Label error_message = new Label();
            error_message.Location = new Point(10, 240);
            error_message.Size = new Size(300, 100);
            form.Controls.Add(error_message);

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
                version_.Visible = false;
                version_label.Visible = false;
                error_message.Text = "See \'" + error_out_file + "\' for error information.";
            }
            else
            {
                error_message.Text = "Data file has no errors!";
                version_.Visible = false;
                version_label.Visible = false;
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Validator v = new Validator();
            v.form = new Form();
            v.form.Size = new Size(600, 400);
            v.form.Text = "Load File Validator";
            v.form.Icon = new Icon("verge_icon.ico");

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
            v.filetarget.Size = new Size(300, 60);
            v.filetarget.Text = "(none)";

            Label filename_label = new Label();
            filename_label.Location = new Point(10, 100);
            filename_label.Font = new Font(filename_label.Font, FontStyle.Bold);
            filename_label.Text = "Input File:";

            version_label = new Label();
            version_label.Location = new Point(20, 242);
            version_label.Size = new Size(100, 15);
            version_label.Font = new Font(version_label.Font, FontStyle.Bold);
            version_label.TextAlign = ContentAlignment.MiddleLeft;
            version_label.Text = "Header Version: ";

            version_ = new ComboBox();
            version_.DropDownStyle = ComboBoxStyle.DropDownList;
            version_.Location = new Point(120, 240);
            version_.Size = new Size(150, 30);
            version_.DataSource = versions;
            version_.SelectedIndexChanged += new System.EventHandler(versionChanged);
            //version_.SelectedValue = versions[0];
            version_.SelectionStart = 0;


            // remove this when done testing. 
            //v.filetarget.Text = "C:\\Users\\Nick\\Documents\\Verge_Dev\\Visual Studio Projects\\Load_Validator\\Load_Validator\\bin\\Debug\\staff_import_fail_test.txt";
            //error_out_file = "C:\\Users\\Nick\\Documents\\Verge_Dev\\Visual Studio Projects\\Load_Validator\\Load_Validator\\bin\\Debug\\staff_import_fail_test_error_out.txt";

            PictureBox logo = new PictureBox();
            logo.Image = Load_Validator.Properties.Resources.vergesolutions;
            logo.Size = new Size(272, 120);
            logo.Location  = new Point(0,0);

            Button pick_file_button = new Button();
            pick_file_button.Text = "Select Data File";
            pick_file_button.Size = new Size(80, 50);
            pick_file_button.Location = new Point(20, 185);
            pick_file_button.Click += new EventHandler(v.openFileDialog);

            validate_button = new Button();
            validate_button.Text = "Validate Data File";
            validate_button.Size = new Size(80, 50);
            validate_button.Location = new Point(105, 185);
            validate_button.Click += new EventHandler(v.validate);
            validate_button.Enabled = false;

            open_errors = new Button();
            open_errors.Text = "Open Error File";
            open_errors.Size = new Size(80, 50);
            open_errors.Location = new Point(190, 185);
            open_errors.Click += new EventHandler(v.openErrorFile);
            open_errors.Enabled = false;

            Button quit_button = new Button();
            quit_button.Text = "Quit";
            quit_button.Size = new Size(100, 50);
            quit_button.Location = new Point(10, 305);
            quit_button.Click += new EventHandler(v.appQuit);

            v.form.Controls.Add(version_label);
            v.form.Controls.Add(version_);
            v.form.Controls.Add(pick_file_button);
            v.form.Controls.Add(v.filetarget);
            v.form.Controls.Add(filename_label);
            v.form.Controls.Add(validate_button);
            v.form.Controls.Add(quit_button);
            v.form.Controls.Add(logo);
            v.form.Controls.Add(open_errors);
        }

        static void setCriteria(Validator v)
        {
            v.COLUMNS = new Criteria[cols + 1]; // indice == column #

            string [] suffix = new string [7] {"Sr.", "Jr.", "II", "III", "IV", "V", ""};
            string [] gender = new string [3] {"M", "F", ""};
            string [] marital_status = new string [3] {"Married", "Not Married", ""};
            string [] bit = new string [3] {"0", "1", ""};

            /*
             * Criteria constructor:
             * 
            *   public Criteria(int column, string column_name, int len, 
                bool required, bool isDate, bool isDec, string [] accepted)
             * */
            if ((string)version_.SelectedValue == "0.7") // format/header does not include localid
            {
                v.COLUMNS[1] = new Criteria(1, "staff_id", 50, true, false, false, null);
                v.COLUMNS[2] = new Criteria(2, "last_name", 50, true, false, false, null);
                v.COLUMNS[3] = new Criteria(3, "first_name", 50, true, false, false, null);
                v.COLUMNS[4] = new Criteria(4, "middle_name", 50, false, false, false, null);
                v.COLUMNS[5] = new Criteria(5, "suffix", 3, false, false, false, suffix);
                v.COLUMNS[6] = new Criteria(6, "npi", 50, false, false, false, null);
                v.COLUMNS[7] = new Criteria(7, "upin", 50, false, false, false, null);
                v.COLUMNS[8] = new Criteria(8, "ssn", 50, false, false, false, null);
                v.COLUMNS[9] = new Criteria(9, "gender", 1, false, false, false, gender);
                v.COLUMNS[10] = new Criteria(10, "date_of_birth", 0, false, true, false, null);
                v.COLUMNS[11] = new Criteria(11, "marital_status", 20, false, false, false, marital_status);
                v.COLUMNS[12] = new Criteria(12, "maiden_name", 50, false, false, false, null);
                v.COLUMNS[13] = new Criteria(13, "organization", 200, true, false, false, null);
                v.COLUMNS[14] = new Criteria(14, "department", 100, false, false, false, null);
                v.COLUMNS[15] = new Criteria(15, "title", 100, false, false, false, null);
                v.COLUMNS[16] = new Criteria(16, "average_days_worked", 0, false, false, true, null);
                v.COLUMNS[17] = new Criteria(17, "average_hours_worked", 0, false, false, true, null);
                v.COLUMNS[18] = new Criteria(18, "ft_indicator", 1, false, false, false, bit);
                v.COLUMNS[19] = new Criteria(19, "citizenship", 50, false, false, false, null);
                v.COLUMNS[20] = new Criteria(20, "visa_classification", 50, false, false, false, null);
                v.COLUMNS[21] = new Criteria(21, "visa_number", 50, false, false, false, null);
                v.COLUMNS[22] = new Criteria(22, "visa_issued", 0, false, true, false, null);
                v.COLUMNS[23] = new Criteria(23, "visa_expires", 0, false, true, false, null);
                v.COLUMNS[24] = new Criteria(24, "office_email", 100, false, false, false, null);
                v.COLUMNS[25] = new Criteria(25, "office_phone", 50, false, false, false, null);
                v.COLUMNS[26] = new Criteria(26, "home_address", 200, false, false, false, null);
                v.COLUMNS[27] = new Criteria(27, "home_address2", 200, false, false, false, null);
                v.COLUMNS[28] = new Criteria(28, "home_city", 100, false, false, false, null);
                v.COLUMNS[29] = new Criteria(29, "home_country", 100, false, false, false, null);
                v.COLUMNS[30] = new Criteria(30, "home_state", 100, false, false, false, null);
                v.COLUMNS[31] = new Criteria(31, "home_zipcode", 50, false, false, false, null);
                v.COLUMNS[32] = new Criteria(32, "home_email", 100, false, false, false, null);
                v.COLUMNS[33] = new Criteria(33, "home_phone", 20, false, false, false, null);
                v.COLUMNS[34] = new Criteria(34, "supervisor_local_id", 50, false, false, false, null);
                v.COLUMNS[35] = new Criteria(35, "delete", 1, false, false, false, bit);
            }
            else if((string)version_.SelectedValue == "0.6") // older version which includes localid
            {
                v.COLUMNS[1] = new Criteria(1, "staff_id", 50, true, false, false, null);
                v.COLUMNS[2] = new Criteria(2, "last_name", 50, true, false, false, null);
                v.COLUMNS[3] = new Criteria(3, "first_name", 50, true, false, false, null);
                v.COLUMNS[4] = new Criteria(4, "middle_name", 50, false, false, false, null);
                v.COLUMNS[5] = new Criteria(5, "suffix", 3, false, false, false, suffix);
                v.COLUMNS[6] = new Criteria(6, "localid", 50, false, false, false, null);
                v.COLUMNS[7] = new Criteria(7, "npi", 50, false, false, false, null);
                v.COLUMNS[8] = new Criteria(8, "upin", 50, false, false, false, null);
                v.COLUMNS[9] = new Criteria(9, "ssn", 50, false, false, false, null);
                v.COLUMNS[10] = new Criteria(10, "gender", 1, false, false, false, gender);
                v.COLUMNS[11] = new Criteria(11, "date_of_birth", 0, false, true, false, null);
                v.COLUMNS[12] = new Criteria(12, "marital_status", 20, false, false, false, marital_status);
                v.COLUMNS[13] = new Criteria(13, "maiden_name", 50, false, false, false, null);
                v.COLUMNS[14] = new Criteria(14, "organization", 200, true, false, false, null);
                v.COLUMNS[15] = new Criteria(15, "department", 100, false, false, false, null);
                v.COLUMNS[16] = new Criteria(16, "title", 100, false, false, false, null);
                v.COLUMNS[17] = new Criteria(17, "average_days_worked", 0, false, false, true, null);
                v.COLUMNS[18] = new Criteria(18, "average_hours_worked", 0, false, false, true, null);
                v.COLUMNS[19] = new Criteria(19, "ft_indicator", 1, false, false, false, bit);
                v.COLUMNS[20] = new Criteria(20, "citizenship", 50, false, false, false, null);
                v.COLUMNS[21] = new Criteria(21, "visa_classification", 50, false, false, false, null);
                v.COLUMNS[22] = new Criteria(22, "visa_number", 50, false, false, false, null);
                v.COLUMNS[23] = new Criteria(23, "visa_issued", 0, false, true, false, null);
                v.COLUMNS[24] = new Criteria(24, "visa_expires", 0, false, true, false, null);
                v.COLUMNS[25] = new Criteria(25, "office_email", 100, false, false, false, null);
                v.COLUMNS[26] = new Criteria(26, "office_phone", 50, false, false, false, null);
                v.COLUMNS[27] = new Criteria(27, "home_address", 200, false, false, false, null);
                v.COLUMNS[28] = new Criteria(28, "home_address2", 200, false, false, false, null);
                v.COLUMNS[29] = new Criteria(29, "home_city", 100, false, false, false, null);
                v.COLUMNS[30] = new Criteria(30, "home_country", 100, false, false, false, null);
                v.COLUMNS[31] = new Criteria(31, "home_state", 100, false, false, false, null);
                v.COLUMNS[32] = new Criteria(32, "home_zipcode", 50, false, false, false, null);
                v.COLUMNS[33] = new Criteria(33, "home_email", 100, false, false, false, null);
                v.COLUMNS[34] = new Criteria(34, "home_phone", 20, false, false, false, null);
                v.COLUMNS[35] = new Criteria(35, "supervisor_local_id", 50, false, false, false, null);
                v.COLUMNS[36] = new Criteria(36, "delete", 1, false, false, false, bit);
            }
        }

        private void openErrorFile(object sender, System.EventArgs e)
        {
            Process.Start("notepad.exe", error_out_file);
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

}
