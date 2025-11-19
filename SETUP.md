# 🚀 BuzzFreed Setup & Next Steps

## ✅ What's Been Completed

The complete Discord Activity application has been scaffolded! Here's what's ready:

### Backend (ASP.NET Core)
- ✅ **Project structure** created in `BuzzFreed.Web/`
- ✅ **Services layer** with:
  - `DatabaseService.cs` - SQLite database operations
  - `OpenAIService.cs` - AI quiz generation using GPT-4o-mini
  - `QuizService.cs` - Quiz orchestration and session management
- ✅ **API Controllers**:
  - `QuizController.cs` - Quiz operations (generate, answer, result, history)
  - `AuthController.cs` - Discord OAuth token exchange
- ✅ **Models** - Quiz, Question, QuizResult, QuizSession
- ✅ **Configuration** - Program.cs with DI and CORS setup

### Frontend
- ✅ **HTML** - Complete multi-screen UI (index.html)
- ✅ **JavaScript**:
  - `app.js` - Discord SDK initialization and authentication
  - `quiz-app.js` - Complete quiz flow logic
- ✅ **CSS** - BuzzFeed-style responsive design
- ✅ **Discord SDK** integration ready

### Configuration
- ✅ `.env.example` - Template for environment variables
- ✅ `appsettings.json` - ASP.NET configuration
- ✅ `.gitignore` - Updated for .NET projects
- ✅ `README.md` - Complete documentation

## 🎯 Next Steps to Launch

### 1. Configuration (5 minutes)

1. **Create `.env` file** from template:
   ```bash
   cp .env.example .env
   ```

2. **Add your Discord credentials** to `.env`:
   - Get from: https://discord.com/developers/applications
   - Add `DISCORD_CLIENT_ID` and `DISCORD_CLIENT_SECRET`

3. **Add OpenAI API key** to `.env`:
   - Get from: https://platform.openai.com/api-keys
   - Add `OPENAI_API_KEY`

4. **Update JavaScript with Discord Client ID**:
   - Edit `BuzzFreed.Web/wwwroot/js/app.js` line 10
   - Replace `'YOUR_DISCORD_CLIENT_ID'` with your actual client ID

### 2. Discord Developer Portal Setup (10 minutes)

1. Go to your Discord application settings
2. Enable **Embedded App SDK** in Activities section
3. Set Activity URL mapping:
   - For local dev: Use cloudflared or ngrok tunnel URL
   - For production: Your deployed app URL
4. Configure OAuth2:
   - Add redirect: `https://your-url.com/api/auth/token`
   - Enable scopes: `identify`, `guilds`

### 3. Local Development (2 minutes)

```bash
cd BuzzFreed.Web
dotnet restore
dotnet run
```

App runs at: `https://localhost:5001`

To test with Discord, use cloudflared tunnel:
```bash
cloudflared tunnel --url https://localhost:5001
```

### 4. Deployment Options

**Recommended: Azure App Service**
- Native .NET support
- Auto HTTPS
- Easy GitHub integration
- Environment variable management

**Alternatives:**
- Railway (simple, free tier)
- Render (Docker support)
- Fly.io (good performance)

### 5. Testing Checklist

Once deployed and configured:

- [ ] Discord Activity launches in Discord
- [ ] Authentication works (Discord SDK connects)
- [ ] Quiz generates with AI
- [ ] Questions display correctly
- [ ] Answers can be submitted
- [ ] Results show with AI-generated description
- [ ] Quiz history loads
- [ ] Retake quiz works

## 🐛 Common Issues & Solutions

### "Discord credentials not configured"
- Check `.env` file exists in project root
- Verify environment variables are set correctly
- Restart the app after changing `.env`

### "OpenAI API error"
- Verify API key is valid and has credits
- Check you're using the right model (gpt-4o-mini)
- Review logs for specific error messages

### "CORS errors in browser"
- Ensure app is served over HTTPS
- Check CORS policy in Program.cs includes Discord origins
- Verify Activity URL in Discord portal matches deployment URL

### "Session not found"
- Sessions are in-memory and cleared on app restart
- For production, consider using Redis for distributed cache
- Session expires when quiz is completed

## 📁 Project Structure

```
buzzfreed/
├── BuzzFreed/              # Old Discord bot (can be archived)
├── BuzzFreed.Web/          # 🆕 NEW: Discord Activity web app
│   ├── Controllers/        # API endpoints
│   ├── Services/          # Business logic
│   ├── Models/            # Data models
│   ├── wwwroot/           # Static files (HTML/CSS/JS)
│   ├── Program.cs         # App startup
│   └── README.md          # Detailed documentation
├── .env.example           # Environment template
└── SETUP.md              # This file
```

## 🎨 Customization Ideas

Want to make it your own? Here are some ideas:

1. **Custom Quiz Topics**
   - Modify `OpenAIService.cs` to use specific topic categories
   - Add topic selection UI

2. **More Questions**
   - Change `numberOfQuestions` parameter (default: 6)
   - Adjust in `QuizService.cs` line 47

3. **Different AI Model**
   - Use `gpt-4` for higher quality (more expensive)
   - Use `gpt-3.5-turbo` for cheaper option
   - Change in `OpenAIService.cs` line 13

4. **Custom Styling**
   - Edit `wwwroot/css/quiz-styles.css`
   - Change colors in CSS `:root` variables

5. **Add Images**
   - Integrate DALL-E API for quiz result images
   - Add image generation in `OpenAIService.cs`

## 🚢 Production Considerations

Before going live:

1. **Environment Variables**
   - Never commit `.env` file
   - Set environment variables in hosting platform
   - Use different keys for dev/prod

2. **Database**
   - SQLite works for small scale
   - For production, consider PostgreSQL or SQL Server
   - Database is stored in `database/buzzfreed.db`

3. **Session Storage**
   - Current: In-memory (clears on restart)
   - Recommended: Redis for distributed sessions
   - Needed for multi-instance deployments

4. **Rate Limiting**
   - Add rate limiting to prevent API abuse
   - Limit quiz generation per user
   - Throttle OpenAI calls

5. **Monitoring**
   - Add Application Insights or similar
   - Monitor OpenAI API costs
   - Track error rates

## 📞 Need Help?

- Check `BuzzFreed.Web/README.md` for detailed docs
- Review Discord Embedded App SDK docs
- Check OpenAI API documentation
- Open an issue on GitHub

## 🎉 You're Ready!

Everything is set up and ready to go. Just complete the configuration steps and you'll have a fully functional AI-powered quiz activity in Discord!
