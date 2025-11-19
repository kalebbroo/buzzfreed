using Microsoft.AspNetCore.Mvc;
using BuzzFreed.Web.Models;
using BuzzFreed.Web.Services;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController(QuizService quizService) : ControllerBase
    {
        public readonly QuizService QuizService = quizService;

        /// <summary>
        /// Generate a new quiz
        /// POST /api/quiz/generate
        /// </summary>
        [HttpPost("generate")]
        public async Task<ActionResult<QuizGenerateResponse>> GenerateQuiz([FromBody] QuizGenerateRequest request)
        {
            try
            {
                Logs.Info($"Generating quiz for user {request.UserId}");

                QuizSession session = await QuizService.GenerateQuizAsync(request.UserId, request.CustomTopic);

                return Ok(new QuizGenerateResponse
                {
                    SessionId = session.SessionId,
                    Topic = session.Quiz.Topic,
                    TotalQuestions = session.Quiz.Questions.Count,
                    FirstQuestion = new QuestionResponse
                    {
                        QuestionNumber = 1,
                        Text = session.Quiz.Questions[0].Text,
                        Options = session.Quiz.Questions[0].Options
                    }
                });
            }
            catch (Exception ex)
            {
                Logs.Error($"Error generating quiz: {ex.Message}");
                return StatusCode(500, new { error = "Failed to generate quiz" });
            }
        }

        /// <summary>
        /// Submit an answer to the current question
        /// POST /api/quiz/answer
        /// </summary>
        [HttpPost("answer")]
        public ActionResult<AnswerResponse> SubmitAnswer([FromBody] AnswerRequest request)
        {
            try
            {
                QuizSession? session = QuizService.SubmitAnswer(request.SessionId, request.Answer);

                if (session == null)
                {
                    return NotFound(new { error = "Session not found" });
                }

                // Check if quiz is completed
                if (session.IsCompleted)
                {
                    return Ok(new AnswerResponse
                    {
                        IsCompleted = true,
                        QuestionNumber = session.CurrentQuestionIndex,
                        TotalQuestions = session.Quiz.Questions.Count
                    });
                }

                // Return next question
                Question nextQuestion = session.Quiz.Questions[session.CurrentQuestionIndex];
                return Ok(new AnswerResponse
                {
                    IsCompleted = false,
                    QuestionNumber = session.CurrentQuestionIndex + 1,
                    TotalQuestions = session.Quiz.Questions.Count,
                    NextQuestion = new QuestionResponse
                    {
                        QuestionNumber = session.CurrentQuestionIndex + 1,
                        Text = nextQuestion.Text,
                        Options = nextQuestion.Options
                    }
                });
            }
            catch (Exception ex)
            {
                Logs.Error($"Error submitting answer: {ex.Message}");
                return StatusCode(500, new { error = "Failed to submit answer" });
            }
        }

        /// <summary>
        /// Get quiz result after completion
        /// POST /api/quiz/result
        /// </summary>
        [HttpPost("result")]
        public async Task<ActionResult<QuizResult>> GetResult([FromBody] ResultRequest request)
        {
            try
            {
                QuizResult result = await QuizService.CalculateResultAsync(
                    request.SessionId,
                    request.UserId,
                    request.GuildId
                );

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Logs.Error($"Error calculating result: {ex.Message}");
                return StatusCode(500, new { error = "Failed to calculate result" });
            }
        }

        /// <summary>
        /// Get user's quiz history
        /// GET /api/quiz/history/{userId}/{guildId}
        /// </summary>
        [HttpGet("history/{userId}/{guildId}")]
        public async Task<ActionResult<List<QuizResult>>> GetHistory(string userId, string guildId)
        {
            try
            {
                List<QuizResult> history = await QuizService.GetUserHistoryAsync(userId, guildId);
                return Ok(history);
            }
            catch (Exception ex)
            {
                Logs.Error($"Error retrieving quiz history: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve quiz history" });
            }
        }

        /// <summary>
        /// Get current session state
        /// GET /api/quiz/session/{sessionId}
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public ActionResult<QuizSessionResponse> GetSession(string sessionId)
        {
            QuizSession? session = QuizService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            return Ok(new QuizSessionResponse
            {
                SessionId = session.SessionId,
                Topic = session.Quiz.Topic,
                CurrentQuestionIndex = session.CurrentQuestionIndex,
                TotalQuestions = session.Quiz.Questions.Count,
                IsCompleted = session.IsCompleted
            });
        }
    }

    // Request/Response DTOs
    public class QuizGenerateRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? CustomTopic { get; set; }
    }

    public class QuizGenerateResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public QuestionResponse FirstQuestion { get; set; } = new();
    }

    public class QuestionResponse
    {
        public int QuestionNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>();
    }

    public class AnswerRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty; // A, B, C, or D
    }

    public class AnswerResponse
    {
        public bool IsCompleted { get; set; }
        public int QuestionNumber { get; set; }
        public int TotalQuestions { get; set; }
        public QuestionResponse? NextQuestion { get; set; }
    }

    public class ResultRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
    }

    public class QuizSessionResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int CurrentQuestionIndex { get; set; }
        public int TotalQuestions { get; set; }
        public bool IsCompleted { get; set; }
    }
}
