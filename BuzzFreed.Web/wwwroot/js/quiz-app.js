// Quiz application state
let currentUserId = null;
let currentGuildId = null;
let currentSessionId = null;
let currentQuestionNumber = 1;
let totalQuestions = 6;
let firstQuestionData = null; // Store first question from generate API

// Initialize quiz app (called from app.js after Discord auth)
window.initQuizApp = function(userId, guildId) {
    currentUserId = userId;
    currentGuildId = guildId;

    console.log('Initializing quiz app for user:', userId);

    // Generate a new quiz
    generateQuiz();

    // Setup event listeners
    setupEventListeners();
};

// Setup all event listeners
function setupEventListeners() {
    document.getElementById('start-btn').addEventListener('click', startQuiz);
    document.getElementById('retake-btn').addEventListener('click', retakeQuiz);
    document.getElementById('history-btn').addEventListener('click', showHistory);
    document.getElementById('back-btn').addEventListener('click', () => {
        switchScreen('history-screen', 'start-screen');
        generateQuiz();
    });
}

// Generate a new quiz
async function generateQuiz() {
    try {
        console.log('Generating quiz...');

        const topicDisplay = document.getElementById('topic-display');
        topicDisplay.innerHTML = '<p class="generating">✨ Generating your quiz...</p>';

        const startBtn = document.getElementById('start-btn');
        startBtn.disabled = true;

        const response = await fetch('/api/quiz/generate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                userId: currentUserId,
                customTopic: null // Let AI generate the topic
            })
        });

        if (!response.ok) {
            throw new Error('Failed to generate quiz');
        }

        const data = await response.json();
        console.log('Quiz generated:', data);

        // Store session ID and first question
        currentSessionId = data.sessionId;
        totalQuestions = data.totalQuestions;
        firstQuestionData = data.firstQuestion;

        // Update UI
        topicDisplay.innerHTML = `
            <h2 class="topic-title">${data.topic}</h2>
            <p class="topic-subtitle">Are you ready to discover your personality?</p>
        `;

        startBtn.disabled = false;

    } catch (error) {
        console.error('Error generating quiz:', error);
        document.getElementById('topic-display').innerHTML = `
            <p class="error">Failed to generate quiz. Please try again.</p>
        `;
    }
}

// Start the quiz
function startQuiz() {
    currentQuestionNumber = 1;
    switchScreen('start-screen', 'question-screen');

    // Display the first question that we got from generate API
    if (firstQuestionData) {
        displayQuestion(firstQuestionData);
    } else {
        console.error('First question data not available');
    }
}

// Display a question
function displayQuestion(questionData) {
    // Update progress
    updateProgress(questionData.questionNumber, totalQuestions);

    // Update question text
    document.getElementById('question-text').textContent = questionData.text;

    // Create answer buttons
    const optionsContainer = document.getElementById('answer-options');
    optionsContainer.innerHTML = '';

    questionData.options.forEach((option, index) => {
        const letter = String.fromCharCode(65 + index); // A, B, C, D
        const button = document.createElement('button');
        button.className = 'answer-btn';
        button.setAttribute('data-answer', letter);
        button.innerHTML = `
            <span class="answer-letter">${letter}</span>
            <span class="answer-text">${option.replace(/^[A-D]\)\s*/, '')}</span>
        `;
        button.addEventListener('click', () => submitAnswer(letter));
        optionsContainer.appendChild(button);
    });
}

// Update progress bar
function updateProgress(current, total) {
    currentQuestionNumber = current;
    const percentage = (current / total) * 100;

    document.getElementById('current-q').textContent = current;
    document.getElementById('total-q').textContent = total;
    document.getElementById('progress-fill').style.width = `${percentage}%`;
}

// Submit an answer
async function submitAnswer(answer) {
    try {
        console.log('Submitting answer:', answer);

        // Disable all buttons to prevent double-click
        const buttons = document.querySelectorAll('.answer-btn');
        buttons.forEach(btn => btn.disabled = true);

        const response = await fetch('/api/quiz/answer', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                sessionId: currentSessionId,
                answer: answer
            })
        });

        if (!response.ok) {
            throw new Error('Failed to submit answer');
        }

        const data = await response.json();
        console.log('Answer response:', data);

        // Check if quiz is completed
        if (data.isCompleted) {
            // Show result
            showResult();
        } else {
            // Display next question
            setTimeout(() => {
                displayQuestion(data.nextQuestion);
            }, 300);
        }

    } catch (error) {
        console.error('Error submitting answer:', error);
        // Re-enable buttons on error
        const buttons = document.querySelectorAll('.answer-btn');
        buttons.forEach(btn => btn.disabled = false);
    }
}

// Show quiz result
async function showResult() {
    try {
        console.log('Calculating result...');

        // Show loading in result screen
        switchScreen('question-screen', 'result-screen');
        document.getElementById('result-personality').textContent = 'Calculating your result...';
        document.getElementById('result-description').textContent = '';

        const response = await fetch('/api/quiz/result', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                sessionId: currentSessionId,
                userId: currentUserId,
                guildId: currentGuildId
            })
        });

        if (!response.ok) {
            throw new Error('Failed to get result');
        }

        const result = await response.json();
        console.log('Result:', result);

        // Display result
        document.getElementById('result-personality').textContent = result.resultPersonality;
        document.getElementById('result-description').textContent = result.resultDescription;

    } catch (error) {
        console.error('Error getting result:', error);
        document.getElementById('result-personality').textContent = 'Error';
        document.getElementById('result-description').textContent = 'Failed to calculate result. Please try again.';
    }
}

// Retake quiz
function retakeQuiz() {
    switchScreen('result-screen', 'start-screen');
    generateQuiz();
}

// Show quiz history
async function showHistory() {
    try {
        console.log('Loading quiz history...');

        switchScreen('result-screen', 'history-screen');

        const historyList = document.getElementById('history-list');
        historyList.innerHTML = '<p class="loading">Loading your quiz history...</p>';

        const response = await fetch(`/api/quiz/history/${currentUserId}/${currentGuildId}`);

        if (!response.ok) {
            throw new Error('Failed to load history');
        }

        const history = await response.json();
        console.log('History:', history);

        if (history.length === 0) {
            historyList.innerHTML = '<p class="empty">No quiz history yet. Take a quiz to get started!</p>';
            return;
        }

        // Display history items
        historyList.innerHTML = history.map(item => `
            <div class="history-item">
                <div class="history-header">
                    <h3>${item.quizTopic}</h3>
                    <span class="history-date">${formatDate(item.timestamp)}</span>
                </div>
                <div class="history-result">
                    <span class="result-badge">${item.resultPersonality}</span>
                    <p>${item.resultDescription}</p>
                </div>
            </div>
        `).join('');

    } catch (error) {
        console.error('Error loading history:', error);
        document.getElementById('history-list').innerHTML = `
            <p class="error">Failed to load quiz history.</p>
        `;
    }
}

// Format date for display
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric'
    });
}

// Expose displayQuestion globally so we can call it with the first question
window.displayQuestion = displayQuestion;
