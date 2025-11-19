/**
 * EventHandler.js - Real-time event handling via Discord SDK
 *
 * RESPONSIBILITIES:
 * - Listen for real-time events from Discord SDK
 * - Parse event data
 * - Update GameState accordingly
 * - Trigger UI updates
 * - Handle event queue during reconnection
 *
 * EVENT TYPES:
 * Room Events:
 * - ROOM_UPDATED: Room settings changed
 * - PLAYER_JOINED: Player entered room
 * - PLAYER_LEFT: Player left room
 * - PLAYER_READY: Player ready state changed
 * - TEAM_ASSIGNED: Player assigned to team
 * - GAME_STARTING: Game about to start
 *
 * Session Events:
 * - SESSION_CREATED: Game session started
 * - TURN_STARTED: New turn began
 * - TURN_ENDED: Turn completed
 * - ANSWER_SUBMITTED: Player submitted answer
 * - GAME_ENDED: Game completed
 *
 * Interaction Events:
 * - REACTION_RECEIVED: Player reacted
 * - SUGGESTION_RECEIVED: Anonymous suggestion
 * - PREDICTION_RECEIVED: Player predicted
 * - CHAT_MESSAGE: Team/global chat
 *
 * DISCORD SDK INTEGRATION:
 * - Uses Discord Activity SDK for real-time messaging
 * - Commands sent as embedded app messages
 * - Events received via SDK event handlers
 * - State synchronized across all clients
 *
 * TODO: Add event buffering (queue events during processing)
 * TODO: Add event deduplication (prevent duplicate processing)
 * TODO: Add event recovery (request missed events after reconnect)
 * TODO: Add event compression (reduce bandwidth)
 */

import gameState from './GameState.js';

class EventHandler {
    constructor() {
        this.discordSdk = null;
        this.isInitialized = false;
        this.eventQueue = [];
        this.isProcessing = false;

        console.log('[EventHandler] Initialized');
    }

    /**
     * Initialize with Discord SDK instance
     */
    initialize(discordSdk) {
        if (this.isInitialized) {
            console.warn('[EventHandler] Already initialized');
            return;
        }

        this.discordSdk = discordSdk;
        this.setupEventListeners();
        this.isInitialized = true;

        console.log('[EventHandler] Discord SDK connected');
    }

    /**
     * Setup Discord SDK event listeners
     */
    setupEventListeners() {
        if (!this.discordSdk) return;

        // TODO: Setup actual Discord SDK event listeners
        // This is a placeholder for the actual implementation

        console.log('[EventHandler] Event listeners setup');

        // Example:
        // this.discordSdk.subscribe('ACTIVITY_INSTANCE_PARTICIPANTS_UPDATE', this.handleParticipantsUpdate.bind(this));
        // this.discordSdk.subscribe('CUSTOM_EVENT', this.handleCustomEvent.bind(this));
    }

    // ==========================================
    // ROOM EVENTS
    // ==========================================

    /**
     * Handle room updated event
     */
    handleRoomUpdated(event) {
        console.log('[EventHandler] Room updated:', event);

        const { roomId, updates } = event;

        if (!gameState.room || gameState.room.roomId !== roomId) {
            console.warn('[EventHandler] Room ID mismatch');
            return;
        }

        gameState.updateRoom(updates);

        // TODO: Show update notification
        // TODO: Play update sound
    }

    /**
     * Handle player joined event
     */
    handlePlayerJoined(event) {
        console.log('[EventHandler] Player joined:', event);

        const { player } = event;

        gameState.addPlayer(player);

        // TODO: Show join notification
        // TODO: Play join sound
        // TODO: Animate player list
    }

    /**
     * Handle player left event
     */
    handlePlayerLeft(event) {
        console.log('[EventHandler] Player left:', event);

        const { playerId } = event;

        gameState.removePlayer(playerId);

        // TODO: Show leave notification
        // TODO: Update team assignments
    }

    /**
     * Handle player ready state changed
     */
    handlePlayerReady(event) {
        console.log('[EventHandler] Player ready:', event);

        const { playerId, isReady } = event;

        if (!gameState.room) return;

        const player = gameState.room.players.find(p => p.userId === playerId);
        if (!player) return;

        player.isReady = isReady;

        gameState.emit('roomUpdate', { room: gameState.room });

        // TODO: Check if all players ready
        // TODO: Enable start button if host
    }

    /**
     * Handle team assignment
     */
    handleTeamAssigned(event) {
        console.log('[EventHandler] Team assigned:', event);

        const { playerId, teamId } = event;

        if (!gameState.room) return;

        const player = gameState.room.players.find(p => p.userId === playerId);
        if (player) {
            player.teamId = teamId;
        }

        gameState.emit('roomUpdate', { room: gameState.room });

        // TODO: Update team roster UI
        // TODO: Check team balance
    }

    /**
     * Handle game starting
     */
    handleGameStarting(event) {
        console.log('[EventHandler] Game starting:', event);

        const { sessionId } = event;

        // Transition to loading screen
        gameState.uiState.currentScreen = 'loading';
        gameState.uiState.isLoading = true;

        // TODO: Show loading screen
        // TODO: Preload assets
        // TODO: Fetch session data
    }

    // ==========================================
    // SESSION EVENTS
    // ==========================================

    /**
     * Handle session created
     */
    handleSessionCreated(event) {
        console.log('[EventHandler] Session created:', event);

        const { session } = event;

        gameState.setSession(session);

        // TODO: Initialize game UI
        // TODO: Load quiz
        // TODO: Setup turn timer
    }

    /**
     * Handle turn started
     */
    handleTurnStarted(event) {
        console.log('[EventHandler] Turn started:', event);

        const { turn, question } = event;

        if (!gameState.session) return;

        gameState.session.currentTurn = turn;

        gameState.emit('turnUpdate', { turn });

        // TODO: Show question UI
        // TODO: Start timer
        // TODO: Enable/disable answer buttons based on active player
        // TODO: Clear previous interactions
    }

    /**
     * Handle turn ended
     */
    handleTurnEnded(event) {
        console.log('[EventHandler] Turn ended:', event);

        const { turnResults } = event;

        if (!gameState.session || !gameState.session.currentTurn) return;

        // Update turn phase
        gameState.session.currentTurn.phase = 'Results';

        gameState.emit('turnUpdate', { turn: gameState.session.currentTurn });

        // TODO: Show results screen
        // TODO: Display score breakdown
        // TODO: Reveal suggestions and predictions
        // TODO: Auto-advance to next turn after delay
    }

    /**
     * Handle answer submitted
     */
    handleAnswerSubmitted(event) {
        console.log('[EventHandler] Answer submitted:', event);

        const { playerId, answerIndex, isCorrect } = event;

        if (!gameState.session || !gameState.session.currentTurn) return;

        // Add response to turn
        gameState.session.currentTurn.responses.push({
            playerId,
            answerIndex,
            isCorrect,
            timestamp: new Date().toISOString()
        });

        gameState.emit('turnUpdate', { turn: gameState.session.currentTurn });

        // TODO: Show answer feedback
        // TODO: Update turn phase if appropriate
    }

    /**
     * Handle game ended
     */
    handleGameEnded(event) {
        console.log('[EventHandler] Game ended:', event);

        const { results, leaderboard } = event;

        if (!gameState.session) return;

        gameState.session.state = 'Completed';

        // TODO: Show final results screen
        // TODO: Display leaderboard
        // TODO: Show statistics
        // TODO: Show highlights
        // TODO: Enable return to lobby button
    }

    // ==========================================
    // INTERACTION EVENTS
    // ==========================================

    /**
     * Handle reaction received
     */
    handleReactionReceived(event) {
        console.log('[EventHandler] Reaction received:', event);

        const { reaction } = event;

        gameState.addInteraction({ ...reaction, type: 'reaction' });

        // TODO: Show reaction animation
        // TODO: Play reaction sound
        // TODO: Update reaction count
    }

    /**
     * Handle suggestion received (shown to active player as count only)
     */
    handleSuggestionReceived(event) {
        console.log('[EventHandler] Suggestion received');

        if (!gameState.session || !gameState.session.currentTurn) return;

        // Increment suggestion count (don't reveal content yet)
        const suggestionCount = (gameState.session.currentTurn.suggestions?.length || 0) + 1;

        // TODO: Show suggestion count indicator
        // TODO: Suggestions revealed after answer
    }

    /**
     * Handle suggestions revealed (after answer submitted)
     */
    handleSuggestionsRevealed(event) {
        console.log('[EventHandler] Suggestions revealed:', event);

        const { suggestions } = event;

        if (!gameState.session || !gameState.session.currentTurn) return;

        gameState.session.currentTurn.suggestions = suggestions;

        gameState.emit('turnUpdate', { turn: gameState.session.currentTurn });

        // TODO: Show suggestions in results
        // TODO: Highlight followed suggestions
        // TODO: Award suggestion bonuses
    }

    /**
     * Handle prediction received
     */
    handlePredictionReceived(event) {
        console.log('[EventHandler] Prediction received');

        if (!gameState.session || !gameState.session.currentTurn) return;

        // Increment prediction count
        const predictionCount = (gameState.session.currentTurn.predictions?.length || 0) + 1;

        // TODO: Show prediction count indicator
    }

    /**
     * Handle chat message
     */
    handleChatMessage(event) {
        console.log('[EventHandler] Chat message:', event);

        const { message } = event;

        // TODO: Display chat message
        // TODO: Filter by team if team chat
        // TODO: Apply profanity filter
        // TODO: Play message sound
    }

    // ==========================================
    // SCORE UPDATES
    // ==========================================

    /**
     * Handle score update
     */
    handleScoreUpdate(event) {
        console.log('[EventHandler] Score update:', event);

        const { scores } = event;

        if (!gameState.session) return;

        gameState.session.scores = scores;

        gameState.emit('sessionUpdate', { session: gameState.session });

        // TODO: Animate score changes
        // TODO: Update leaderboard
        // TODO: Check for leader changes
    }

    /**
     * Handle leaderboard update
     */
    handleLeaderboardUpdate(event) {
        console.log('[EventHandler] Leaderboard update:', event);

        const { leaderboard } = event;

        gameState.cache.leaderboard = leaderboard;

        // TODO: Update leaderboard UI
        // TODO: Highlight current player
        // TODO: Show rank changes
    }

    // ==========================================
    // EVENT QUEUE
    // ==========================================

    /**
     * Queue event for processing
     * Used during heavy processing or reconnection
     */
    queueEvent(eventType, eventData) {
        this.eventQueue.push({ type: eventType, data: eventData });

        if (!this.isProcessing) {
            this.processEventQueue();
        }
    }

    /**
     * Process queued events
     */
    async processEventQueue() {
        if (this.isProcessing) return;

        this.isProcessing = true;

        while (this.eventQueue.length > 0) {
            const event = this.eventQueue.shift();

            try {
                await this.handleEvent(event.type, event.data);
            } catch (error) {
                console.error('[EventHandler] Error processing event:', error);
            }
        }

        this.isProcessing = false;
    }

    /**
     * Route event to appropriate handler
     */
    async handleEvent(eventType, eventData) {
        switch (eventType) {
            // Room events
            case 'ROOM_UPDATED':
                this.handleRoomUpdated(eventData);
                break;
            case 'PLAYER_JOINED':
                this.handlePlayerJoined(eventData);
                break;
            case 'PLAYER_LEFT':
                this.handlePlayerLeft(eventData);
                break;
            case 'PLAYER_READY':
                this.handlePlayerReady(eventData);
                break;
            case 'TEAM_ASSIGNED':
                this.handleTeamAssigned(eventData);
                break;
            case 'GAME_STARTING':
                this.handleGameStarting(eventData);
                break;

            // Session events
            case 'SESSION_CREATED':
                this.handleSessionCreated(eventData);
                break;
            case 'TURN_STARTED':
                this.handleTurnStarted(eventData);
                break;
            case 'TURN_ENDED':
                this.handleTurnEnded(eventData);
                break;
            case 'ANSWER_SUBMITTED':
                this.handleAnswerSubmitted(eventData);
                break;
            case 'GAME_ENDED':
                this.handleGameEnded(eventData);
                break;

            // Interaction events
            case 'REACTION_RECEIVED':
                this.handleReactionReceived(eventData);
                break;
            case 'SUGGESTION_RECEIVED':
                this.handleSuggestionReceived(eventData);
                break;
            case 'SUGGESTIONS_REVEALED':
                this.handleSuggestionsRevealed(eventData);
                break;
            case 'PREDICTION_RECEIVED':
                this.handlePredictionReceived(eventData);
                break;
            case 'CHAT_MESSAGE':
                this.handleChatMessage(eventData);
                break;

            // Score events
            case 'SCORE_UPDATE':
                this.handleScoreUpdate(eventData);
                break;
            case 'LEADERBOARD_UPDATE':
                this.handleLeaderboardUpdate(eventData);
                break;

            default:
                console.warn('[EventHandler] Unknown event type:', eventType);
        }
    }

    // TODO: Add event recovery (request missed events)
    // TODO: Add event deduplication
    // TODO: Add event batching (process multiple events together)
    // TODO: Add event priority (process important events first)
}

// Export singleton instance
const eventHandler = new EventHandler();
export default eventHandler;
