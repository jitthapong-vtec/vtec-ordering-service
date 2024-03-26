using System.Collections.Generic;

namespace VerticalTec.POS
{
    public class QuestionOption
    {
        public int QuestionID { get; set; }
        public int OptionID { get; set; }
        public string QuestionText { get; set; }
        public decimal QuestionValue { get; set; }
    }
}
