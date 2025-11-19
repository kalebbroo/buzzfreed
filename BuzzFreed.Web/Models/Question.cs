namespace BuzzFreed.Web.Models
{
    public class Question
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>();
        public string CorrectAnswer { get; set; } = string.Empty; // For personality mapping (A, B, C, or D)
    }
}
