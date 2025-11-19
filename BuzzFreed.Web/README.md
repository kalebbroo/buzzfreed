# 🎯 BuzzFreed - AI-Powered Discord Quiz Activity

A BuzzFeed-style personality quiz application built as a Discord Activity using the Embedded App SDK and powered by AI.

## 🌟 Features

- **AI-Generated Quizzes**: Uses OpenAI's GPT-4o-mini to generate unique quiz topics, questions, and results
- **BuzzFeed-Style UI**: Fun, colorful, and engaging interface
- **Discord Integration**: Runs as an embedded activity inside Discord
- **Personality Results**: Get personalized results based on your answers
- **Quiz History**: Track all your past quiz results
- **Multiplayer Ready**: Multiple users can take quizzes simultaneously

## 🏗️ Architecture

```
┌─────────────────────────────────────┐
│   Discord Client (Desktop/Web)      │
│  ┌───────────────────────────────┐  │
│  │   BuzzFreed Web App (iframe)  │  │
│  │   React-style JS + CSS        │  │
│  │   + Discord SDK              │◄─┼──┼─ PostMessage
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
         ▲
         │ HTTPS
         ▼
┌─────────────────────────────────────┐
│  ASP.NET Core Web App               │
│  ├── Static Files (HTML/CSS/JS)     │
│  ├── Web API (REST endpoints)       │
│  ├── OpenAI Service (GPT-4o-mini)   │
│  └── SQLite Database                │
└─────────────────────────────────────┘
```

## 📋 Prerequisites

- **.NET 7.0 SDK** or higher
- **Discord Application** with Embedded App SDK enabled
- **OpenAI API Key** (GPT-4o-mini or GPT-3.5-turbo)
- A way to serve the app over HTTPS (required for Discord Activities)

## 🚀 Setup Instructions

### 1. Discord Developer Portal Setup

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Create a new application or use an existing one
3. Navigate to **OAuth2** section:
   - Copy your **Client ID**
   - Copy your **Client Secret**
   - Add redirect URL: `https://your-app-url.com/api/auth/token`
4. Navigate to **Activities** section:
   - Enable **Embedded App SDK**
   - Set your Activity URL mapping (e.g., `/` -> `https://your-app-url.com`)
5. Under **OAuth2** > **Scopes**, ensure these are enabled:
   - `identify`
   - `guilds`

### 2. OpenAI API Setup

1. Go to [OpenAI Platform](https://platform.openai.com/api-keys)
2. Create a new API key
3. Copy the key (starts with `sk-`)

### 3. Local Development Setup

1. **Clone the repository**
   ```bash
   cd buzzfreed/BuzzFreed.Web
   ```

2. **Create `.env` file** (copy from `.env.example`)
   ```bash
   cp ../.env.example ../.env
   ```

3. **Edit `.env` file** and add your credentials:
   ```env
   DISCORD_CLIENT_ID=your_discord_client_id_here
   DISCORD_CLIENT_SECRET=your_discord_client_secret_here
   OPENAI_API_KEY=your_openai_api_key_here
   ```

4. **Update Discord Client ID in JavaScript**

   Edit `wwwroot/js/app.js` and replace the placeholder:
   ```javascript
   const clientId = 'YOUR_DISCORD_CLIENT_ID'; // Line 10
   ```
   With your actual Discord Client ID:
   ```javascript
   const clientId = '1234567890123456789'; // Your actual client ID
   ```

5. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

6. **Run the application**
   ```bash
   dotnet run
   ```

   The app will start at:
   - HTTPS: `https://localhost:5001`
   - HTTP: `http://localhost:5000`

### 4. Testing Locally with Discord

For local development, you'll need to expose your localhost to the internet so Discord can reach it. Use one of these tools:

**Option A: Using Cloudflare Tunnel (Recommended)**
```bash
# Install cloudflared
# Then run:
cloudflared tunnel --url https://localhost:5001
```

**Option B: Using ngrok**
```bash
ngrok http https://localhost:5001
```

Then update your Discord Activity URL to the tunnel URL provided.

## 🌐 Deployment

### Deploying to Production

The app needs to be deployed to a server with HTTPS support. Recommended options:

1. **Azure App Service**
   - Supports ASP.NET Core natively
   - Easy deployment from GitHub
   - Auto HTTPS with custom domains

2. **Railway**
   - Simple deployment
   - Auto HTTPS
   - Good for small projects

3. **Render**
   - Free tier available
   - Auto HTTPS
   - Docker support

### Environment Variables for Production

Make sure to set these environment variables in your hosting platform:

- `DISCORD_CLIENT_ID`
- `DISCORD_CLIENT_SECRET`
- `OPENAI_API_KEY`
- `ASPNETCORE_ENVIRONMENT=Production`

## 📁 Project Structure

```
BuzzFreed.Web/
├── Controllers/
│   ├── AuthController.cs       # Discord OAuth token exchange
│   └── QuizController.cs       # Quiz API endpoints
├── Services/
│   ├── DatabaseService.cs      # SQLite database operations
│   ├── OpenAIService.cs        # OpenAI GPT integration
│   └── QuizService.cs          # Quiz generation & management
├── Models/
│   ├── Quiz.cs                 # Quiz data model
│   ├── Question.cs             # Question data model
│   ├── QuizResult.cs           # Result data model
│   └── QuizSession.cs          # Active session tracking
├── wwwroot/
│   ├── index.html              # Main HTML
│   ├── css/
│   │   └── quiz-styles.css     # BuzzFeed-style CSS
│   └── js/
│       ├── app.js              # Discord SDK initialization
│       └── quiz-app.js         # Quiz UI logic
├── Program.cs                  # ASP.NET startup
├── appsettings.json            # Configuration
└── BuzzFreed.Web.csproj        # Project file
```

## 🔌 API Endpoints

### Quiz Endpoints

- `POST /api/quiz/generate` - Generate a new quiz
- `POST /api/quiz/answer` - Submit an answer
- `POST /api/quiz/result` - Get quiz result
- `GET /api/quiz/history/{userId}/{guildId}` - Get user's quiz history
- `GET /api/quiz/session/{sessionId}` - Get session state

### Auth Endpoints

- `POST /api/auth/token` - Exchange Discord OAuth code for token

## 🎨 Customization

### Changing Quiz Parameters

Edit `BuzzFreed.Web/Services/OpenAIService.cs`:

```csharp
// Change number of questions (default: 6)
var questions = await _openAIService.GenerateQuestionsAsync(topic, numberOfQuestions: 8);

// Change AI model (gpt-4o-mini, gpt-3.5-turbo, gpt-4)
private const string Model = "gpt-4o-mini";
```

### Styling

Edit `wwwroot/css/quiz-styles.css` to customize colors and appearance:

```css
:root {
    --primary-color: #EE3F58;      /* Main brand color */
    --secondary-color: #FF9F1C;     /* Accent color */
    --accent-color: #2EC4B6;        /* Highlight color */
}
```

## 🐛 Troubleshooting

### Common Issues

**"Discord credentials not configured"**
- Make sure `.env` file exists and contains valid Discord credentials
- Check that environment variables are loaded (Program.cs loads them via DotNetEnv)

**"OpenAI API key not configured"**
- Verify your OpenAI API key is set in `.env`
- Ensure the key starts with `sk-`

**"Session not found"**
- Sessions are stored in memory and cleared after completion
- Sessions expire if the app restarts

**CORS errors**
- Make sure your app is served over HTTPS
- Check that Discord origin is allowed in Program.cs CORS policy

## 📝 License

This project is licensed under the MIT License.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For issues or questions, please open an issue on GitHub.
