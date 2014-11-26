using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace Load_Validator
{
    class Criteria
    {
        int column { get; set; }
        public string column_name { get; set; }
        int len { get; set; }
        bool required { get; set; }
        bool isDate;
        bool isDec;
        string[] accepted;

        public Criteria(int column, string column_name, int len, 
                        bool required, bool isDate, bool isDec, string [] accepted)
        {
            this.column = column;
            this.column_name = column_name;
            this.len = len;
            this.required = required;
            this.isDate = isDate;
            this.isDec = isDec;
            this.accepted = accepted;
        }

        /*
         *  void function runs through validations, and sets static booleans 
         * */
        public bool validate(string input)
        {
            // first check if required field and is blank - trim off whitespace. 
            if (required & input.Trim().Length == 0)
            {
                flagLabels.required_field.fail();
                return false;
            }

            if (accepted != null)
            {
                if (!this.accepted.Contains(input)) { flagLabels.enums.fail(); return false; }
            }

            // decimals are not required, so only validate if length > 0
            if (isDec & input.Length > 0)
            {
                bool succeed;
                decimal d;
                succeed = Decimal.TryParse(input, out d);

                string[] n = new string[2];
                n = input.Split('.');

                if (n.Length > 1)
                {
                    if (n[0].Length > 8 | n[1].Length > 4 | !succeed)
                    {
                        Debug.WriteLine(">" + input);

                        flagLabels.decimals.fail();
                        return false;
                    }
                }
                else if (n.Length == 1) //failing
                {
                    if (n[0].Length > 8 | !succeed)
                    {
                        Debug.WriteLine("}" + input);
                        flagLabels.decimals.fail();
                        return false;
                    }
                }
            }

            // no dates are required, only validate if length > 0
            if (isDate & input.Length > 0)
            {
                try
                {
                    DateTime dt = DateTime.Parse(input);
                }
                catch
                {
                    flagLabels.dates.fail();
                    return false;
                }
            }

            if (len != 0 & input.Length > this.len) 
            {
                Debug.WriteLine(column);
                flagLabels.lengths.fail();
                return false;
            }

            return true;
        }

        public string spec()
        {
            string len;

            if (this.len > 0)
            {
                len = "  (Length must be <= " + this.len.ToString() + " characters.)";
            }
            else
            {
                len = "";
            }

            string req = (this.required == true) ? "  (This field is required.)" : "";
            string date = (this.isDate == true) ? "  (This field must be a valid date : YYYY-MM-DD.)" : "";
            string dec = (this.isDec == true) ? "  (This field must be a valid decimal : \'12,4 DEC\'.)" : "";

            string accepted;
            if (this.accepted != null)
            {
                accepted = (this.accepted.Length > 0) ? "  (Value must be in: " + string.Join(",", getAcceptedValues()) + ")" : "";
            }
            else
            {
                accepted = "";
            }
            string s = "Field Requirments:" + req + len + date + dec + accepted;

            return s;
        }

        private List<string> getAcceptedValues()
        {
            List<string> list = new List<string>();

            foreach (string s in this.accepted)
            {
                list.Add(s);
            }

            return list;
        }
    }
}
