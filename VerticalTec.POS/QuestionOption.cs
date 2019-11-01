namespace VerticalTec.POS
{
    public class QuestionOption
    {
        public int QuestionID { get; set; }
        public int QuestionType { get; set; }
        public int IsRequire { get; set; }
        public int OptionID { get; set; }
        public string OptionName { get; set; }
        public string QuestionText { get; set; }
        public decimal QuestionValue { get; set; }
        public bool Selected { get; set; }
    }
}
