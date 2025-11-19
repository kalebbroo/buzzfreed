namespace BuzzFreed.Web.Models
{
    public class Quiz
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Topic { get; set; } = string.Empty;
        public List<Question> Questions { get; set; } = new List<Question>();
        public Dictionary<string, string> ResultPersonalities { get; set; } = new Dictionary<string, string>();
        // ResultPersonalities maps answer letter (A, B, C, D) to personality type
        // e.g., {"A": "Espresso", "B": "Latte", "C": "Cappuccino", "D": "Mocha"}
    }
}
