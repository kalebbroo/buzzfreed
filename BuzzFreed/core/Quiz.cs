using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BuzzFreed
{
    public class Quiz
    {
        private string? _quizTopic;
        private List<string> _questions;
        private List<string> _answers;
        private List<string> _userAnswers;

        public Quiz()
        {
            _questions = new List<string>();
            _answers = new List<string>();
            _userAnswers = new List<string>();
            _quizTopic = null;  // Initialize to null to satisfy non-nullable warning
        }

        public async Task ChooseQuiz()
        {
            _quizTopic = "Which Coffee Are You?";
            await BuildQuiz();  // Now we're using await, to get rid of the second warning
        }

        public async Task BuildQuiz()
        {
            _questions.Add("How do you like to start your day?");
            _answers.Add("A) With a workout; B) Reading the news; C) Snoozing the alarm; D) Meditation");
            // Simulating some async work here to get rid of the warning
            await Task.Delay(10);
        }

        public string AskQuestion(int index)
        {
            if (index >= 0 && index < _questions.Count)
            {
                return _questions[index];
            }
            else
            {
                return "Invalid question index";
            }
        }

        public void RecordAnswer(int questionIndex, string answer)
        {
            _userAnswers.Add(answer);
        }

        public string GetResults()
        {
            return "You are an Espresso!";
        }

        // New method to use _quizTopic and eliminate the warning
        public string GetQuizTopic()
        {
            return _quizTopic ?? "No quiz topic set";
        }
    }
}