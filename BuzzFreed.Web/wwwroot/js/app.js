// Discord SDK initialization and authentication
let discordSdk;
let auth;
let userInfo = null;
let guildId = null;

// Initialize Discord SDK
async function initDiscord() {
    try {
        console.log('Initializing Discord SDK...');

        // Create Discord SDK instance
        // TODO: Replace with your actual Discord Client ID from environment
        const clientId = 'YOUR_DISCORD_CLIENT_ID';

        discordSdk = new window.DiscordSDK(clientId);

        // Wait for Discord to be ready
        await discordSdk.ready();
        console.log('Discord SDK ready');

        // Authorize with Discord
        const { code } = await discordSdk.commands.authorize({
            client_id: clientId,
            response_type: 'code',
            state: '',
            prompt: 'none',
            scope: ['identify', 'guilds']
        });

        console.log('Authorization code received');

        // Exchange code for access token via our backend
        const response = await fetch('/api/auth/token', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ code })
        });

        if (!response.ok) {
            throw new Error('Token exchange failed');
        }

        const { access_token } = await response.json();
        console.log('Access token received');

        // Authenticate with Discord
        auth = await discordSdk.commands.authenticate({
            access_token
        });

        console.log('Authenticated with Discord');

        // Get user info
        userInfo = {
            id: auth.user.id,
            username: auth.user.username,
            discriminator: auth.user.discriminator,
            avatar: auth.user.avatar
        };

        // Get guild ID from instance context
        const instanceContext = await discordSdk.commands.getInstanceConnectedParticipants();
        guildId = discordSdk.guildId || 'dm';

        console.log('User info:', userInfo);
        console.log('Guild ID:', guildId);

        // Discord is ready, hide loading screen
        hideScreen('loading-screen');
        showScreen('start-screen');

        // Initialize the quiz app
        window.initQuizApp(userInfo.id, guildId);

    } catch (error) {
        console.error('Discord initialization error:', error);
        document.getElementById('loading-screen').innerHTML = `
            <div class="error">
                <h2>Failed to connect to Discord</h2>
                <p>${error.message}</p>
                <p>Please reload the activity.</p>
            </div>
        `;
    }
}

// Screen management helpers
function showScreen(screenId) {
    document.getElementById(screenId).classList.add('active');
}

function hideScreen(screenId) {
    document.getElementById(screenId).classList.remove('active');
}

function switchScreen(fromScreenId, toScreenId) {
    hideScreen(fromScreenId);
    showScreen(toScreenId);
}

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initDiscord);
} else {
    initDiscord();
}
