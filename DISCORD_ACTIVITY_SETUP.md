# Discord Embedded App SDK Integration Guide

Complete guide for integrating Discord's Embedded App SDK to make BuzzFreed appear as a playable Activity in Discord.

## Table of Contents
- [Overview](#overview)
- [What is a Discord Activity?](#what-is-a-discord-activity)
- [How the Embedded App SDK Works](#how-the-embedded-app-sdk-works)
- [Step-by-Step Setup](#step-by-step-setup)
- [SDK Integration Details](#sdk-integration-details)
- [Authentication Flow](#authentication-flow)
- [Common Issues](#common-issues)
- [Testing Your Activity](#testing-your-activity)
- [Production Deployment](#production-deployment)

## Overview

Discord Activities are multiplayer games and social experiences that run directly inside Discord. They're web applications that:
- Run in an **iframe** within the Discord client
- Work on **desktop, web, and mobile**
- Require **no installation** for users
- Use the **Embedded App SDK** to communicate with Discord

### Current Status in BuzzFreed

✅ **Already Implemented:**
- Discord SDK script loaded in `index.html` (line 86)
- Basic initialization code in `app.js`
- Loading screen with "Connecting to Discord..." message
- Authentication structure ready

⚠️ **Needs Configuration:**
- Discord Client ID (currently placeholder)
- Discord Developer Portal Activity setup
- Backend OAuth2 token exchange endpoint
- HTTPS deployment URL

## What is a Discord Activity?

An Activity is a web app hosted in an iframe that can:
- Access Discord user information (with permission)
- Know which server/channel it's running in
- See which users are participating
- Update Rich Presence status
- Handle multiplayer state

**Key Concept:** Your web app runs in an iframe, and the SDK handles all communication between your app and Discord through secure postMessage API.

## How the Embedded App SDK Works

### Architecture

```
┌─────────────────────────────────────────┐
│         Discord Client                  │
│  ┌───────────────────────────────────┐  │
│  │   Your Activity (iframe)          │  │
│  │                                   │  │
│  │  ┌─────────────────────────┐     │  │
│  │  │  Embedded App SDK       │     │  │
│  │  │  (discord.js)           │     │  │
│  │  └──────────┬──────────────┘     │  │
│  │             │                     │  │
│  │             │ postMessage API     │  │
│  │             │                     │  │
│  └─────────────┼─────────────────────┘  │
│                │                         │
│       ┌────────▼────────┐                │
│       │  Discord API    │                │
│       └─────────────────┘                │
└─────────────────────────────────────────┘
```

### Communication Flow

1. **SDK Initialization**: Your app creates a `DiscordSDK` instance
2. **Ready Event**: SDK confirms communication channel is established
3. **Authorization**: SDK requests OAuth2 authorization from Discord
4. **Token Exchange**: Your backend exchanges auth code for access token
5. **Authentication**: SDK authenticates user with the access token
6. **Commands & Events**: Your app can now use SDK commands and listen to events

## Step-by-Step Setup

### 1. Discord Developer Portal Configuration

#### Create/Configure Your Application

1. Go to https://discord.com/developers/applications
2. Select your application or create a new one
3. Note your **Application ID** (this is your Client ID)

#### Enable Embedded App SDK

1. Navigate to **Activities** section in left sidebar
2. Toggle **Enable Embedded App SDK**
3. Click **Save Changes**

#### Configure Activity URL Mapping

This tells Discord where your web app is hosted:

**For Development:**
```
Default Mapping: https://your-tunnel-url.trycloudflare.com
```
Use tunneling services like:
- Cloudflared: `cloudflared tunnel --url https://localhost:5001`
- ngrok: `ngrok http 5001`

**For Production:**
```
Default Mapping: https://your-app.azurewebsites.net
```

⚠️ **Important:** Your app MUST be served over HTTPS, even in development!

#### Configure OAuth2

1. Go to **OAuth2** section
2. Add **Redirect URL**: `https://your-domain/api/auth/token`
3. Under **OAuth2 Scopes**, ensure these are available:
   - `identify` - Get user information
   - `guilds` - Get server information

#### Get Your Credentials

Copy these values (you'll need them later):
- **Application ID** (Client ID)
- **Client Secret** (from OAuth2 section)

### 2. Backend Configuration

#### Update .env File

```bash
# Copy example if you haven't
cp .env.example .env
```

Edit `.env`:
```env
# Discord OAuth2 Configuration
DISCORD_CLIENT_ID=your_application_id_here
DISCORD_CLIENT_SECRET=your_client_secret_here

# OpenAI API Configuration
OPENAI_API_KEY=your_openai_key_here
```

#### Update appsettings.json

Edit `BuzzFreed.Web/appsettings.json`:
```json
{
  "Discord": {
    "ClientId": "YOUR_DISCORD_CLIENT_ID",
    "ClientSecret": "YOUR_DISCORD_CLIENT_SECRET"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  }
}
```

**Note:** In production, use environment variables instead of hardcoding in appsettings.json

### 3. Frontend Configuration

#### Update Client ID in JavaScript

Edit `BuzzFreed.Web/wwwroot/js/app.js` line 14:

```javascript
// BEFORE:
const clientId = 'YOUR_DISCORD_CLIENT_ID';

// AFTER:
const clientId = '1234567890123456789'; // Your actual Application ID
```

**Security Note:** The Client ID is public and safe to expose in frontend code.

### 4. Verify Backend API Endpoint

Ensure `BuzzFreed.Web/Controllers/AuthController.cs` has the token exchange endpoint:

```csharp
[HttpPost("token")]
public async Task<IActionResult> ExchangeToken([FromBody] TokenRequest request)
{
    // Exchanges Discord auth code for access token
    // See BuzzFreed.Web/Controllers/AuthController.cs for implementation
}
```

This endpoint is called by the frontend during authentication (see `app.js` line 34).

## SDK Integration Details

### Current Implementation (app.js)

#### 1. SDK Loading
```html
<!-- index.html line 86 -->
<script src="https://cdn.jsdelivr.net/npm/@discord/embedded-app-sdk@latest/dist/discord.js"></script>
```

**Alternative (NPM):**
```bash
npm install @discord/embedded-app-sdk
```

#### 2. SDK Initialization
```javascript
// app.js lines 8-20
async function initDiscord() {
    const clientId = 'YOUR_DISCORD_CLIENT_ID';

    // Create SDK instance
    discordSdk = new window.DiscordSDK(clientId);

    // Wait for ready
    await discordSdk.ready();

    // Continue with authorization...
}
```

#### 3. Authorization Flow
```javascript
// app.js lines 23-29
const { code } = await discordSdk.commands.authorize({
    client_id: clientId,
    response_type: 'code',
    state: '',
    prompt: 'none',
    scope: ['identify', 'guilds']
});
```

**Scopes Explained:**
- `identify` - Get user ID, username, avatar
- `guilds` - Get server/guild information

#### 4. Token Exchange
```javascript
// app.js lines 33-46
const response = await fetch('/api/auth/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code })
});

const { access_token } = await response.json();
```

**Why Backend Exchange?**
- Client Secret must stay secret (can't be in frontend)
- Backend validates and exchanges the code securely
- Returns access token to frontend

#### 5. Authentication
```javascript
// app.js lines 50-52
auth = await discordSdk.commands.authenticate({
    access_token
});
```

After this, `auth.user` contains user information.

#### 6. Get Context Information
```javascript
// app.js lines 57-66
// User info
userInfo = {
    id: auth.user.id,
    username: auth.user.username,
    discriminator: auth.user.discriminator,
    avatar: auth.user.avatar
};

// Server/guild ID
guildId = discordSdk.guildId || 'dm';
```

### Available SDK Commands

#### Get Instance Participants
```javascript
const participants = await discordSdk.commands.getInstanceConnectedParticipants();
// Returns list of users in current Activity instance
```

#### Set Activity Status
```javascript
await discordSdk.commands.setActivity({
    details: "Playing BuzzFreed Quiz",
    state: "Question 3 of 6",
    instance: true
});
```

#### Get Channel Info
```javascript
const channelId = discordSdk.channelId;
const guildId = discordSdk.guildId;
```

#### Open External Link
```javascript
await discordSdk.commands.openExternalLink({
    url: 'https://example.com'
});
```

### SDK Events

You can subscribe to events:

```javascript
// When user voice state changes
discordSdk.subscribe('VOICE_STATE_UPDATE', (data) => {
    console.log('Voice state updated:', data);
});

// When speaking state changes
discordSdk.subscribe('SPEAKING_START', (data) => {
    console.log('User started speaking:', data);
});

// When speaking stops
discordSdk.subscribe('SPEAKING_STOP', (data) => {
    console.log('User stopped speaking:', data);
});
```

## Authentication Flow

### Complete OAuth2 Flow Diagram

```
┌─────────────┐         ┌──────────────┐         ┌──────────────┐         ┌─────────────┐
│   Your App  │         │  Discord SDK │         │  Your Backend│         │ Discord API │
│  (Frontend) │         │              │         │              │         │             │
└──────┬──────┘         └──────┬───────┘         └──────┬───────┘         └──────┬──────┘
       │                       │                        │                        │
       │ 1. new DiscordSDK()   │                        │                        │
       │──────────────────────>│                        │                        │
       │                       │                        │                        │
       │ 2. ready()            │                        │                        │
       │<──────────────────────│                        │                        │
       │                       │                        │                        │
       │ 3. authorize()        │                        │                        │
       │──────────────────────>│                        │                        │
       │                       │                        │                        │
       │                       │ 4. Request auth code   │                        │
       │                       │────────────────────────┼───────────────────────>│
       │                       │                        │                        │
       │                       │ 5. Return code         │                        │
       │                       │<───────────────────────┼────────────────────────│
       │                       │                        │                        │
       │ 6. { code }           │                        │                        │
       │<──────────────────────│                        │                        │
       │                       │                        │                        │
       │ 7. POST /api/auth/token with code              │                        │
       │────────────────────────────────────────────────>│                        │
       │                       │                        │                        │
       │                       │                        │ 8. Exchange code       │
       │                       │                        │───────────────────────>│
       │                       │                        │                        │
       │                       │                        │ 9. access_token        │
       │                       │                        │<───────────────────────│
       │                       │                        │                        │
       │ 10. { access_token }  │                        │                        │
       │<────────────────────────────────────────────────│                        │
       │                       │                        │                        │
       │ 11. authenticate({ access_token })             │                        │
       │──────────────────────>│                        │                        │
       │                       │                        │                        │
       │ 12. { user }          │                        │                        │
       │<──────────────────────│                        │                        │
       │                       │                        │                        │
```

### Why This Flow?

1. **Code Grant Flow** - More secure than implicit flow
2. **Backend Exchange** - Client secret never exposed to frontend
3. **Access Token** - Used for authenticated SDK commands
4. **User Data** - Returned after successful authentication

## Common Issues

### 1. "Failed to connect to Discord"

**Symptoms:**
- Loading screen stays indefinitely
- Console error: "Discord SDK failed to initialize"

**Solutions:**
- Verify app is running over HTTPS (even localhost)
- Check Client ID is correct in `app.js`
- Ensure Activity URL mapping is set in Discord Portal
- Verify you're testing inside Discord (Activity won't work in regular browser)

### 2. "Authorization failed"

**Symptoms:**
- Error during authorize() call
- Console error: "Authorization error"

**Solutions:**
- Check OAuth2 scopes are configured in Discord Portal
- Verify redirect URL matches your backend endpoint
- Ensure user has granted permissions

### 3. "Token exchange failed"

**Symptoms:**
- Error after authorization
- Backend returns 400/500 error

**Solutions:**
- Verify `.env` file has correct Client ID and Secret
- Check backend AuthController is implemented correctly
- Ensure backend endpoint `/api/auth/token` is accessible
- Verify CORS is configured to allow Discord origins

### 4. "App works locally but not in Discord"

**Solutions:**
- Ensure tunnel URL is set as Activity URL in Discord Portal
- Check HTTPS certificate is valid (no self-signed certs)
- Verify CORS headers allow Discord domains
- Test in Discord by launching Activity from server

### 5. CORS Errors

**Symptoms:**
- Browser console shows CORS errors
- Fetch requests fail

**Solutions:**
Update `Program.cs` to allow Discord origins:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
            .WithOrigins(
                "https://discord.com",
                "https://*.discord.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

## Testing Your Activity

### Local Development Testing

1. **Start your app:**
   ```bash
   cd BuzzFreed.Web
   dotnet run
   ```

2. **Start tunnel:**
   ```bash
   cloudflared tunnel --url https://localhost:5001
   ```
   Copy the generated URL (e.g., `https://abc123.trycloudflare.com`)

3. **Update Discord Portal:**
   - Go to Activities section
   - Set Default Mapping to your tunnel URL
   - Save changes

4. **Test in Discord:**
   - Open Discord
   - Go to any server where your app is installed
   - Click the rocket icon 🚀 in the channel
   - Select your Activity from the list
   - Click "Launch"

### What to Test

- [ ] Activity launches and shows loading screen
- [ ] Loading screen disappears after successful connection
- [ ] Start screen appears with quiz topic
- [ ] User can start quiz and answer questions
- [ ] Results display correctly
- [ ] History works
- [ ] Retake quiz generates new questions
- [ ] No console errors

### Debugging Tips

**Enable Verbose Logging:**
```javascript
// Add at top of app.js
discordSdk.commands.setLogLevel('debug');
```

**Check SDK State:**
```javascript
console.log('SDK State:', {
    instanceId: discordSdk.instanceId,
    channelId: discordSdk.channelId,
    guildId: discordSdk.guildId,
    userId: discordSdk.userId
});
```

**Monitor Network:**
- Open DevTools (F12) in Discord
- Go to Network tab
- Filter for `/api/` to see backend calls
- Check for failed requests

## Production Deployment

### Pre-Deployment Checklist

- [ ] Environment variables set in hosting platform
- [ ] Discord Client ID updated in production build
- [ ] Activity URL in Discord Portal points to production domain
- [ ] HTTPS certificate is valid
- [ ] CORS configured for Discord domains
- [ ] Database connection configured
- [ ] OpenAI API key with sufficient credits

### Recommended Hosting

**Azure App Service** (Best for .NET):
```bash
# Deploy to Azure
az webapp up --name buzzfreed-app --resource-group myResourceGroup
```

**Environment Variables in Azure:**
```bash
az webapp config appsettings set \
    --name buzzfreed-app \
    --resource-group myResourceGroup \
    --settings \
    DISCORD_CLIENT_ID="your_id" \
    DISCORD_CLIENT_SECRET="your_secret" \
    OPENAI_API_KEY="your_key"
```

### Post-Deployment Steps

1. **Update Activity URL in Discord Portal:**
   - Production URL: `https://buzzfreed-app.azurewebsites.net`
   - Save changes

2. **Update OAuth2 Redirect:**
   - Add: `https://buzzfreed-app.azurewebsites.net/api/auth/token`

3. **Test Production Activity:**
   - Launch Activity in Discord
   - Verify all functionality works
   - Check no CORS errors

4. **Monitor:**
   - Application logs for errors
   - OpenAI API usage
   - User engagement metrics

## Next Steps

### Enhancement Ideas

1. **Multiplayer Features:**
   ```javascript
   // See who else is playing
   const participants = await discordSdk.commands.getInstanceConnectedParticipants();
   ```

2. **Rich Presence:**
   ```javascript
   // Show what users are doing
   await discordSdk.commands.setActivity({
       details: "Playing BuzzFreed",
       state: `Question ${currentQ} of ${totalQ}`,
       instance: true
   });
   ```

3. **Voice Integration:**
   ```javascript
   // React to voice events
   discordSdk.subscribe('SPEAKING_START', handleSpeaking);
   ```

4. **Leaderboards:**
   - Track scores across servers
   - Show top performers
   - Weekly/monthly rankings

### Resources

- **Discord SDK Docs:** https://discord.com/developers/docs/developer-tools/embedded-app-sdk
- **Discord SDK GitHub:** https://github.com/discord/embedded-app-sdk
- **SDK Examples:** https://github.com/discord/embedded-app-sdk-examples
- **Discord Dev Portal:** https://discord.com/developers/applications
- **NPM Package:** https://www.npmjs.com/package/@discord/embedded-app-sdk

### Support

- Discord Developer Server: https://discord.gg/discord-developers
- GitHub Issues: Report SDK issues on GitHub
- Documentation: Check official docs first

## Summary

Your BuzzFreed app is already set up with the Discord Embedded App SDK! Here's what you have:

✅ **Working Implementation:**
- SDK loaded and ready
- Authentication flow implemented
- Screen management working
- Error handling in place

🔧 **Just Need to Configure:**
- Discord Client ID in code
- Developer Portal Activity settings
- Environment variables
- Deployment URL

Follow this guide step-by-step and you'll have a fully functional Discord Activity! 🚀
